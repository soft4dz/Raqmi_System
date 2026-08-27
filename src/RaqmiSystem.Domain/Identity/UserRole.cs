namespace RaqmiSystem.Domain.Identity;

public sealed class UserRole
{
    private UserRole()
    {
    }

    public UserRole(Guid userId, Guid roleId, DateTimeOffset assignedAt)
    {
        UserId = userId;
        RoleId = roleId;
        AssignedAt = assignedAt;
    }

    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    public Guid RoleId { get; private set; }

    public Role Role { get; private set; } = null!;

    public DateTimeOffset AssignedAt { get; private set; }
}
