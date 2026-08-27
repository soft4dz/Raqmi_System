using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Identity;

public sealed class User : AuditableEntity
{
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
