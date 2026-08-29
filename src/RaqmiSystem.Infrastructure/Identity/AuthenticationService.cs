using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Security;

namespace RaqmiSystem.Infrastructure.Identity;

public sealed class AuthenticationService(
    RaqmiDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IAuditLogWriter auditLogWriter) : IAuthenticationService
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(14);
    private const int RefreshTokenByteLength = 32;

    // Timing-attack mitigation: the "user not found/inactive" and "account locked out" branches
    // below return without ever running PBKDF2, while the "wrong password" branch always runs a
    // full 310,000-iteration PBKDF2-SHA256 verification (see Pbkdf2PasswordHasher). Without this,
    // an attacker can distinguish the three outcomes purely from response latency, which leaks
    // account existence and precisely when an account transitions into lockout. Every fast path
    // therefore also runs a Verify() against this fixed, precomputed dummy hash - its result is
    // discarded, its only purpose is to consume comparable CPU time. The hash is computed once,
    // at class load, from a fixed placeholder value that is not any real account's password.
    private static readonly string DummyPasswordHash =
        new Pbkdf2PasswordHasher().Hash("timing-parity-dummy-value-not-a-real-password");

    public async Task<LoginResponse?> SignInAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var normalizedIdentifier = Normalize(request.UserNameOrEmail);
        var now = DateTimeOffset.UtcNow;

        var user = await dbContext.Users
            .Include(currentUser => currentUser.Roles)
            .ThenInclude(userRole => userRole.Role)
            .ThenInclude(role => role.Permissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .SingleOrDefaultAsync(
                currentUser =>
                    currentUser.NormalizedUserName == normalizedIdentifier ||
                    currentUser.NormalizedEmail == normalizedIdentifier,
                cancellationToken);

        if (user is null || !user.IsActive)
        {
            passwordHasher.Verify(request.Password, DummyPasswordHash);

            await auditLogWriter.WriteAsync(
                new AuditLogEntry(null, request.UserNameOrEmail, "auth.login.failed", "security.users", null, ipAddress, "{\"reason\":\"not_found_or_inactive\"}"),
                cancellationToken);

            return null;
        }

        if (user.IsLockedOut(now))
        {
            passwordHasher.Verify(request.Password, DummyPasswordHash);

            await auditLogWriter.WriteAsync(
                new AuditLogEntry(user.Id, user.UserName, "auth.login.locked_out", "security.users", user.Id.ToString(), ipAddress, "{\"reason\":\"locked_out\"}"),
                cancellationToken);

            return null;
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            // No explicit SaveChangesAsync here: the audit write below flushes this tracked
            // change together with the new audit row in a single round trip, keeping this
            // rejection branch's DB I/O identical to the fast paths above (timing-safety).
            user.RegisterFailedLogin(now);

            await auditLogWriter.WriteAsync(
                new AuditLogEntry(user.Id, user.UserName, "auth.login.failed", "security.users", user.Id.ToString(), ipAddress, "{\"reason\":\"invalid_password\"}"),
                cancellationToken);

            return null;
        }

        var roles = ExtractRoles(user);
        var permissions = ExtractPermissions(user);

        user.RegisterSuccessfulLogin(now);

        var rawRefreshToken = IssueRefreshToken(user.Id, now);

        await auditLogWriter.WriteAsync(
            new AuditLogEntry(user.Id, user.UserName, "auth.login.success", "security.users", user.Id.ToString(), ipAddress, null),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return tokenService.CreateToken(user, roles, permissions, rawRefreshToken);
    }

    public async Task<LoginResponse?> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var tokenHash = HashToken(refreshToken);

        var existingToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (existingToken is null || !existingToken.IsActive(now))
        {
            await auditLogWriter.WriteAsync(
                new AuditLogEntry(existingToken?.UserId, null, "auth.refresh.failed", "security.refresh_tokens", existingToken?.Id.ToString(), ipAddress, null),
                cancellationToken);

            return null;
        }

        // Rotation: the presented token is single-use. Revocation must be atomic at the database
        // level - if we only flipped RevokedAt on the tracked entity and relied on SaveChanges
        // later, two concurrent requests presenting the same active token could both pass the
        // IsActive check above before either commit, and each would go on to mint a new pair from
        // a single presented token. Using a conditional ExecuteUpdateAsync makes "claim this
        // token" a single atomic statement: only the request that flips RevokedAt from null to
        // non-null wins the race. This bypasses the change tracker, so existingToken's in-memory
        // RevokedAt is intentionally left stale - it is not read again after this point.
        var revokedRowCount = await dbContext.RefreshTokens
            .Where(token => token.Id == existingToken.Id && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAt, now),
                cancellationToken);

        if (revokedRowCount == 0)
        {
            // Lost the race: another request already consumed this token between our read above
            // and our attempt to claim it. Treat it the same as an already-invalid token.
            await auditLogWriter.WriteAsync(
                new AuditLogEntry(existingToken.UserId, null, "auth.refresh.failed", "security.refresh_tokens", existingToken.Id.ToString(), ipAddress, "{\"reason\":\"token_already_rotated\"}"),
                cancellationToken);

            return null;
        }

        var user = await dbContext.Users
            .Include(currentUser => currentUser.Roles)
            .ThenInclude(userRole => userRole.Role)
            .ThenInclude(role => role.Permissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .SingleOrDefaultAsync(currentUser => currentUser.Id == existingToken.UserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            await auditLogWriter.WriteAsync(
                new AuditLogEntry(existingToken.UserId, null, "auth.refresh.failed", "security.refresh_tokens", existingToken.Id.ToString(), ipAddress, "{\"reason\":\"user_not_found_or_inactive\"}"),
                cancellationToken);

            return null;
        }

        var roles = ExtractRoles(user);
        var permissions = ExtractPermissions(user);

        var rawRefreshToken = IssueRefreshToken(user.Id, now);

        await auditLogWriter.WriteAsync(
            new AuditLogEntry(user.Id, user.UserName, "auth.refresh.success", "security.users", user.Id.ToString(), ipAddress, null),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return tokenService.CreateToken(user, roles, permissions, rawRefreshToken);
    }

    private string IssueRefreshToken(Guid userId, DateTimeOffset now)
    {
        var rawToken = GenerateRawToken();

        var refreshTokenEntity = new RefreshToken(
            userId,
            HashToken(rawToken),
            now.Add(RefreshTokenLifetime),
            now);

        dbContext.RefreshTokens.Add(refreshTokenEntity);

        return rawToken;
    }

    private static string[] ExtractRoles(User user)
    {
        return user.Roles
            .Select(userRole => userRole.Role.Name)
            .Distinct()
            .Order()
            .ToArray();
    }

    private static string[] ExtractPermissions(User user)
    {
        return user.Roles
            .SelectMany(userRole => userRole.Role.Permissions)
            .Select(rolePermission => rolePermission.Permission.Key)
            .Distinct()
            .Order()
            .ToArray();
    }

    private static string GenerateRawToken()
    {
        // 256 bits of CSPRNG entropy, base64-encoded so it can travel safely in JSON and headers.
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(RefreshTokenByteLength));
    }

    private static string HashToken(string rawToken)
    {
        // Only the SHA-256 hash is ever persisted; the raw token is never stored. The hash
        // already carries the full entropy of the raw token, so a direct database equality
        // lookup on it is an acceptable (non-timing-sensitive) comparison here.
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }
}
