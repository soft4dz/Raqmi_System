namespace RaqmiSystem.Application.Security;

public sealed record AuditLogEntry(
    Guid? UserId,
    string? UserName,
    string Action,
    string EntityName,
    string? EntityId,
    string? IpAddress,
    string? DetailsJson);
