using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Audit;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Audit;

public sealed class AuditQueryService(RaqmiDbContext dbContext) : IAuditQueryService
{
    public async Task<PagedResult<AuditLogSummary>> SearchAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? userId,
        string? action,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilters(dbContext.AuditLogs.AsNoTracking(), from, to, userId, action);

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(auditLog => auditLog.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        var items = rows.Select(Map).ToArray();

        return new PagedResult<AuditLogSummary>(items, page, pageSize, totalCount);
    }

    public async Task<int> PurgeOlderThanAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        return await dbContext.AuditLogs
            .Where(auditLog => auditLog.OccurredAt < threshold)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static IQueryable<AuditLog> ApplyFilters(
        IQueryable<AuditLog> query,
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? userId,
        string? action)
    {
        // Converted to UTC before reaching the query: Npgsql refuses a DateTimeOffset whose
        // offset is not zero against a 'timestamp with time zone' column. The instant is the
        // same, only its offset changes, so the filtered period is unchanged.
        if (from.HasValue)
        {
            var fromValue = from.Value.ToUniversalTime();
            query = query.Where(auditLog => auditLog.OccurredAt >= fromValue);
        }

        if (to.HasValue)
        {
            var toValue = to.Value.ToUniversalTime();
            query = query.Where(auditLog => auditLog.OccurredAt <= toValue);
        }

        if (userId.HasValue)
        {
            query = query.Where(auditLog => auditLog.UserId == userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalizedAction = action.Trim();
            query = query.Where(auditLog => auditLog.Action == normalizedAction);
        }

        return query;
    }

    private static AuditLogSummary Map(AuditLog auditLog)
    {
        return new AuditLogSummary(
            auditLog.Id,
            auditLog.UserId,
            auditLog.UserName,
            auditLog.Action,
            auditLog.EntityName,
            auditLog.EntityId,
            auditLog.IpAddress,
            auditLog.DetailsJson,
            auditLog.OccurredAt);
    }
}
