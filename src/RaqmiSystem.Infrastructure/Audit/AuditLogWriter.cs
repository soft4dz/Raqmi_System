using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Audit;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Audit;

public sealed class AuditLogWriter(RaqmiDbContext dbContext) : IAuditLogWriter
{
    public async Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(new AuditLog(
            entry.UserId,
            entry.UserName,
            entry.Action,
            entry.EntityName,
            entry.EntityId,
            entry.IpAddress,
            entry.DetailsJson,
            DateTimeOffset.UtcNow));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
