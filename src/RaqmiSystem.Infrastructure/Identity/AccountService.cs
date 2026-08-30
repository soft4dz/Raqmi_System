using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Security;

namespace RaqmiSystem.Infrastructure.Identity;

/// <summary>
/// Server side of the self-service account operations (see <see cref="IAccountService"/>).
///
/// Every rule lives here rather than in the endpoint, for the same reason the anti-lockout guards
/// live in <see cref="IUserAdministrationService"/>: an HTTP client does not go through the screen
/// that would have checked them.
/// </summary>
public sealed class AccountService(
    RaqmiDbContext dbContext,
    IPasswordHasher passwordHasher,
    IAuditLogWriter auditLogWriter) : IAccountService
{
    private const string AuditEntityName = "security.users";

    /// <summary>
    /// One message for two different failures - "no such account" and "wrong current password" -
    /// on purpose. The caller is authenticated, so the account behind the token is not a secret to
    /// them; but keeping the two answers identical means a token that outlived its account cannot
    /// be told apart from a mistyped password by anyone replaying it, and it removes any temptation
    /// to grow the two branches apart later.
    /// </summary>
    private const string InvalidCurrentPassword = "Current password is incorrect.";

    private const string InactiveAccount =
        "This account is not active. Ask an administrator to reactivate it.";

    private const string BlankNewPassword = "The new password must not be blank.";

    // Built from the policy constants rather than restating the numbers, so the message a user
    // reads can never contradict the threshold actually enforced two lines below.
    private static readonly string NewPasswordTooShort =
        $"The new password must be at least {PasswordPolicy.MinimumLength} characters long.";

    private static readonly string NewPasswordTooLong =
        $"The new password must be at most {PasswordPolicy.MaximumLength} characters long.";

    private const string NewPasswordUnchanged =
        "The new password must be different from the current one.";

    // Timing-attack parity, mirroring AuthenticationService.SignInAsync: the branches that return
    // before ever calling Verify() would otherwise answer measurably faster than the wrong-password
    // branch, which always runs a full 310,000-iteration PBKDF2-SHA256. Since the "no such account"
    // and "wrong password" branches deliberately return the SAME message, letting them differ in
    // latency would hand back the distinction the message refuses to make. Each fast path therefore
    // burns comparable CPU against this fixed hash and discards the result. Computed once, at class
    // load, from a placeholder that is no real account's password.
    private static readonly string DummyPasswordHash =
        new Pbkdf2PasswordHasher().Hash("timing-parity-dummy-value-not-a-real-password");

    public async Task<ApplicationResult<ChangePasswordResponse>> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        // Tracked on purpose: the new hash is written back through this instance.
        var user = await dbContext.Users
            .SingleOrDefaultAsync(current => current.Id == userId, cancellationToken);

        if (user is null)
        {
            passwordHasher.Verify(currentPassword, DummyPasswordHash);

            return ApplicationResult<ChangePasswordResponse>.Validation(InvalidCurrentPassword);
        }

        if (!user.IsActive)
        {
            passwordHasher.Verify(currentPassword, DummyPasswordHash);

            // A distinct message here leaks nothing - the caller holds a token for this very
            // account - and telling a deactivated user that their password was wrong would send
            // them chasing a problem that is not theirs to fix.
            return ApplicationResult<ChangePasswordResponse>.Validation(InactiveAccount);
        }

        if (!passwordHasher.Verify(currentPassword, user.PasswordHash))
        {
            // Deliberately NOT counted as a failed login: this is not a sign-in attempt, and
            // wiring it into User.RegisterFailedLogin would let anyone holding a session lock the
            // account's owner out of the front door by mistyping a form field five times.
            await WriteAuditAsync(
                "security.account.password_change_failed",
                user.Id,
                context,
                new { user.UserName, Reason = "invalid_current_password" },
                cancellationToken);

            return ApplicationResult<ChangePasswordResponse>.Validation(InvalidCurrentPassword);
        }

        // Whitespace-only also lands here: it is what IPasswordHasher.Hash refuses outright, so
        // this check is what keeps that refusal from surfacing as a 500 further down.
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return ApplicationResult<ChangePasswordResponse>.Validation(BlankNewPassword);
        }

        if (newPassword.Length < PasswordPolicy.MinimumLength)
        {
            return ApplicationResult<ChangePasswordResponse>.Validation(NewPasswordTooShort);
        }

        if (newPassword.Length > PasswordPolicy.MaximumLength)
        {
            return ApplicationResult<ChangePasswordResponse>.Validation(NewPasswordTooLong);
        }

        // Ordinal: two passwords that differ by so much as a Unicode normalization form are
        // different passwords to PBKDF2, so they must be different passwords to this check too.
        if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
        {
            return ApplicationResult<ChangePasswordResponse>.Validation(NewPasswordUnchanged);
        }

        var now = DateTimeOffset.UtcNow;

        // Revocation runs BEFORE the new hash is persisted, and that ordering is the safe one: if
        // the write below fails, the account keeps its old password but every session is already
        // closed - recoverable by signing in again. The reverse ordering would risk the opposite,
        // a changed password whose old sessions quietly survive, which is precisely the failure
        // this revocation exists to prevent.
        //
        // ExecuteUpdateAsync (not a tracked loop over the tokens) for the same reason
        // AuthenticationService.RefreshAsync uses it for rotation: it is one statement, so a
        // session refreshing itself at this exact moment either loses its token to this UPDATE or
        // has already rotated it into a row this same UPDATE covers. It bypasses the change
        // tracker, hence its placement before any pending modification of the user entity.
        //
        // The WHERE deliberately does NOT narrow to unexpired tokens. It cannot: EF Core's SQLite
        // provider - the one the test harness runs on - refuses to translate ANY DateTimeOffset
        // comparison, so an "ExpiresAt > now" clause here throws at runtime instead of filtering.
        // That is why the rest of the codebase evaluates token expiry in memory (see
        // RefreshToken.IsActive) and never in SQL. Widening the statement to every unrevoked token
        // costs nothing in security terms - a token past its expiry is already inactive, so
        // stamping RevokedAt on it is a no-op - and it keeps the revocation a single atomic UPDATE,
        // which is the property that actually matters here. The trade-off is that the count below
        // includes any abandoned token that expired without ever being rotated; see
        // ChangePasswordResponse.RevokedSessionCount, whose wording matches this.
        var revokedSessionCount = await dbContext.RefreshTokens
            .Where(token => token.UserId == user.Id && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAt, now),
                cancellationToken);

        user.SetPasswordHash(passwordHasher.Hash(newPassword), mustChangePassword: false);
        user.MarkUpdated(context.UserName, now);

        // The audit trail records that the password changed, who changed it, from where, and how
        // many sessions it closed. It records NEITHER password: not the old one, not the new one,
        // not a prefix, not a length. An audit log readable by audit.read holders must never become
        // a second, weaker store of credentials.
        await WriteAuditAsync(
            "security.account.password_changed",
            user.Id,
            context,
            new { user.UserName, RevokedSessionCount = revokedSessionCount },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<ChangePasswordResponse>.Success(new ChangePasswordResponse(
            user.Id,
            user.UserName,
            user.MustChangePassword,
            revokedSessionCount,
            now));
    }

    private async Task WriteAuditAsync(
        string action,
        Guid userId,
        OperationContext context,
        object details,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                AuditEntityName,
                userId.ToString(),
                context.IpAddress,
                JsonSerializer.Serialize(details)),
            cancellationToken);
    }
}
