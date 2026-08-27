using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Identity;

public sealed class AuthenticationService(
    RaqmiDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IAuditLogWriter auditLogWriter) : IAuthenticationService
{
    public async Task<LoginResponse?> SignInAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var normalizedIdentifier = Normalize(request.UserNameOrEmail);

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
            await auditLogWriter.WriteAsync(
                new AuditLogEntry(null, request.UserNameOrEmail, "auth.login.failed", "security.users", null, ipAddress, "{\"reason\":\"not_found_or_inactive\"}"),
                cancellationToken);

            return null;
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            await auditLogWriter.WriteAsync(
                new AuditLogEntry(user.Id, user.UserName, "auth.login.failed", "security.users", user.Id.ToString(), ipAddress, "{\"reason\":\"invalid_password\"}"),
                cancellationToken);

            return null;
        }

        var roles = user.Roles
            .Select(userRole => userRole.Role.Name)
            .Distinct()
            .Order()
            .ToArray();

        var permissions = user.Roles
            .SelectMany(userRole => userRole.Role.Permissions)
            .Select(rolePermission => rolePermission.Permission.Key)
            .Distinct()
            .Order()
            .ToArray();

        user.MarkLogin(DateTimeOffset.UtcNow);

        await auditLogWriter.WriteAsync(
            new AuditLogEntry(user.Id, user.UserName, "auth.login.success", "security.users", user.Id.ToString(), ipAddress, null),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return tokenService.CreateToken(user, roles, permissions);
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }
}
