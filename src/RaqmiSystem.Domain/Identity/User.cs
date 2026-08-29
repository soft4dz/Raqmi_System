using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Identity;

public sealed class User : AuditableEntity
{
    // Sliding-window lockout policy: 5 failed attempts inside a 15-minute window locks the
    // account for 15 minutes. Both windows share the same duration for simplicity, but they are
    // independent concerns (attempt counting vs. lockout length) and can be tuned separately.
    private static readonly TimeSpan FailedLoginWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private const int MaxFailedLoginAttempts = 5;

    private User()
    {
    }

    public User(
        string userName,
        string email,
        string displayName,
        string passwordHash,
        bool mustChangePassword = true)
    {
        UserName = RequireValue(userName, nameof(userName));
        NormalizedUserName = Normalize(UserName);
        Email = RequireValue(email, nameof(email));
        NormalizedEmail = Normalize(Email);
        DisplayName = RequireValue(displayName, nameof(displayName));
        PasswordHash = RequireValue(passwordHash, nameof(passwordHash));
        MustChangePassword = mustChangePassword;
        IsActive = true;
    }

    public string UserName { get; private set; } = string.Empty;

    public string NormalizedUserName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public bool MustChangePassword { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public int FailedLoginAttempts { get; private set; }

    public DateTimeOffset? FailedLoginWindowStartedAt { get; private set; }

    public DateTimeOffset? LockedOutUntil { get; private set; }

    public ICollection<UserRole> Roles { get; private set; } = new List<UserRole>();

    public void AssignRole(Role role, DateTimeOffset utcNow)
    {
        if (Roles.Any(userRole => userRole.RoleId == role.Id))
        {
            return;
        }

        Roles.Add(new UserRole(Id, role.Id, utcNow));
    }

    public void MarkLogin(DateTimeOffset utcNow)
    {
        LastLoginAt = utcNow;
    }

    public bool IsLockedOut(DateTimeOffset utcNow)
    {
        return LockedOutUntil.HasValue && LockedOutUntil.Value > utcNow;
    }

    public void RegisterFailedLogin(DateTimeOffset utcNow)
    {
        var windowExpired = FailedLoginWindowStartedAt is null
            || utcNow - FailedLoginWindowStartedAt.Value > FailedLoginWindow;

        if (windowExpired)
        {
            FailedLoginWindowStartedAt = utcNow;
            FailedLoginAttempts = 1;
        }
        else
        {
            FailedLoginAttempts++;
        }

        if (FailedLoginAttempts >= MaxFailedLoginAttempts)
        {
            LockedOutUntil = utcNow + LockoutDuration;
        }
    }

    public void RegisterSuccessfulLogin(DateTimeOffset utcNow)
    {
        FailedLoginAttempts = 0;
        FailedLoginWindowStartedAt = null;
        LockedOutUntil = null;
        MarkLogin(utcNow);
    }

    public void SetPasswordHash(string passwordHash, bool mustChangePassword)
    {
        PasswordHash = RequireValue(passwordHash, nameof(passwordHash));
        MustChangePassword = mustChangePassword;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static string RequireValue(string value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        return value.Trim();
    }
}
