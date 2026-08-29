namespace RaqmiSystem.Application.Security;

public sealed record AuditPurgeResponse(int DeletedCount, DateTimeOffset Threshold);
