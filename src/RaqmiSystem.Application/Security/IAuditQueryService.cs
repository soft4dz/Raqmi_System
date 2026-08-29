using RaqmiSystem.Application.Common;

namespace RaqmiSystem.Application.Security;

public interface IAuditQueryService
{
    Task<PagedResult<AuditLogSummary>> SearchAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? userId,
        string? action,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<int> PurgeOlderThanAsync(DateTimeOffset threshold, CancellationToken cancellationToken);
}
