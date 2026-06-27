using Raqmi.Data.Entities;

namespace Raqmi.Data;

public class AuditService(RaqmiDbContext db)
{
    public async Task LogAsync(
        string? tenantId,
        string? userId,
        string action,
        string? moduleCode,
        string? entityType,
        string? entityId,
        string description,
        CancellationToken ct = default)
    {
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = userId,
            Action = action,
            ModuleCode = moduleCode,
            EntityType = entityType,
            EntityId = entityId,
            Description = description,
        });
        await db.SaveChangesAsync(ct);
    }
}
