namespace RaqmiSystem.Application.Security;

public interface IAuditLogWriter
{
    Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken);
}
