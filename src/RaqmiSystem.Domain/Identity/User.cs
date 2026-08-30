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

    /// <summary>
    /// Replaces the whole role set in one call. Administration screens edit a user's roles as a
    /// set, not as a stream of add/remove operations, so the entity exposes the same shape: what
    /// is not in <paramref name="roles"/> is revoked, what is new is assigned, and what was
    /// already there keeps its original <see cref="UserRole.AssignedAt"/> instead of being
    /// removed and re-added (which would rewrite history for an unchanged assignment).
    /// </summary>
    public void SetRoles(IReadOnlyCollection<Role> roles, DateTimeOffset utcNow)
    {
        var targetRoleIds = roles.Select(role => role.Id).ToHashSet();

        var revoked = Roles
            .Where(userRole => !targetRoleIds.Contains(userRole.RoleId))
            .ToArray();

        foreach (var userRole in revoked)
        {
            Roles.Remove(userRole);
        }

        foreach (var role in roles)
        {
            AssignRole(role, utcNow);
        }
    }

    /// <summary>
    /// Updates the mutable part of the identity. <see cref="UserName"/> is deliberately absent:
    /// it is the sign-in identifier (normalized into <see cref="NormalizedUserName"/>, quoted in
    /// every audit entry and carried in every issued token as the name claim), so renaming it
    /// would silently break existing sessions and make the audit trail ambiguous about who acted.
    /// A user who needs a different login gets a new account.
    /// </summary>
    public void UpdateProfile(string email, string displayName)
    {
        Email = RequireValue(email, nameof(email));
        NormalizedEmail = Normalize(Email);
        DisplayName = RequireValue(displayName, nameof(displayName));
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

    /// <summary>
    /// Administrative lift of an ongoing lockout. A successful sign-in already clears the lockout
    /// (see <see cref="RegisterSuccessfulLogin"/>), but a locked-out account cannot sign in at
    /// all - so without this the owner has to wait out the full lockout duration. Clearing
    /// <see cref="LockedOutUntil"/> alone would not be enough: the failure counter and its sliding
    /// window survive the lockout, so the very next wrong password would immediately re-lock the
    /// account. All three fields are therefore reset together, exactly as on a successful login.
    /// </summary>
    public void Unlock()
    {
        FailedLoginAttempts = 0;
        FailedLoginWindowStartedAt = null;
        LockedOutUntil = null;
    }

    public void Activate()
    {
        IsActive = true;
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
