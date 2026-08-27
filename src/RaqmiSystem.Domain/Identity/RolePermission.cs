namespace RaqmiSystem.Domain.Identity;

public sealed class RolePermission
{
    private RolePermission()
    {
    }

    public RolePermission(Guid roleId, Guid permissionId, DateTimeOffset grantedAt)
    {
        RoleId = roleId;
        PermissionId = permissionId;
        GrantedAt = grantedAt;
    }

    public Guid RoleId { get; private set; }

    public Role Role { get; private set; } = null!;

    public Guid PermissionId { get; private set; }

    public Permission Permission { get; private set; } = null!;

    public DateTimeOffset GrantedAt { get; private set; }
}
