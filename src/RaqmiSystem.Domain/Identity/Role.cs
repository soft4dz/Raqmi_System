using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Identity;

public sealed class Role : AuditableEntity
{
    private Role()
    {
    }

    public Role(string name, string displayName, string description, bool isSystem = false)
    {
        Name = RequireValue(name, nameof(name)).ToLowerInvariant();
        DisplayName = RequireValue(displayName, nameof(displayName));
        Description = RequireValue(description, nameof(description));
        IsSystem = isSystem;
    }

    public string Name { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public bool IsSystem { get; private set; }

    public bool IsActive { get; private set; } = true;

    public ICollection<RolePermission> Permissions { get; private set; } = new List<RolePermission>();

    public void GrantPermission(Permission permission, DateTimeOffset utcNow)
    {
        if (Permissions.Any(rolePermission => rolePermission.PermissionId == permission.Id))
        {
            return;
        }

        Permissions.Add(new RolePermission(Id, permission.Id, utcNow));
    }

    public void Deactivate()
    {
        IsActive = false;
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
