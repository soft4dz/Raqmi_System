namespace RaqmiSystem.Domain.Common;

public abstract class AuditableEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;

    public string CreatedBy { get; protected set; } = "system";

    public DateTimeOffset? UpdatedAt { get; protected set; }

    public string? UpdatedBy { get; protected set; }

    public void MarkUpdated(string userName, DateTimeOffset utcNow)
    {
        UpdatedAt = utcNow;
        UpdatedBy = userName;
    }
}
