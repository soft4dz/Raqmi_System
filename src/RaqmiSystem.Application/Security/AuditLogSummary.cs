namespace RaqmiSystem.Application.Security;

public sealed record AuditLogSummary(
    Guid Id,
    Guid? UserId,
    string? UserName,
    string Action,
    string EntityName,
    string? EntityId,
    string? IpAddress,
    string? DetailsJson,
    DateTimeOffset OccurredAt);
