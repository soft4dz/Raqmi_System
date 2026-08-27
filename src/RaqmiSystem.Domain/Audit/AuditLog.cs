namespace RaqmiSystem.Domain.Audit;

public sealed class AuditLog
{
    private AuditLog()
    {
    }

    public AuditLog(
        Guid? userId,
        string? userName,
        string action,
        string entityName,
        string? entityId,
        string? ipAddress,
        string? detailsJson,
        DateTimeOffset occurredAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        UserName = userName;
        Action = RequireValue(action, nameof(action));
        EntityName = RequireValue(entityName, nameof(entityName));
        EntityId = entityId;
        IpAddress = ipAddress;
        DetailsJson = detailsJson;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public Guid? UserId { get; private set; }

    public string? UserName { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string EntityName { get; private set; } = string.Empty;

    public string? EntityId { get; private set; }

    public string? IpAddress { get; private set; }

    public string? DetailsJson { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    private static string RequireValue(string value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        return value.Trim();
    }
}
