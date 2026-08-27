namespace RaqmiSystem.Domain.Common;

public abstract class AuditableEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;

    public string CreatedBy { get; protected set; } = "system";

    public DateTimeOffset? UpdatedAt { get; protected set; }

    public string? UpdatedBy { get; protected set; }

    public void MarkCreated(string userName, DateTimeOffset utcNow)
    {
        CreatedAt = utcNow;
        CreatedBy = RequireActor(userName);
    }

    public void MarkUpdated(string userName, DateTimeOffset utcNow)
    {
        UpdatedAt = utcNow;
        UpdatedBy = RequireActor(userName);
    }

    private static string RequireActor(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return "system";
        }

        return userName.Trim();
    }
}
