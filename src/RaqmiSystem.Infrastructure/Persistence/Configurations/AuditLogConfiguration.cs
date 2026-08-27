using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Audit;

namespace RaqmiSystem.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs", "audit");

        builder.HasKey(auditLog => auditLog.Id);

        builder.Property(auditLog => auditLog.UserName)
            .HasMaxLength(160);

        builder.Property(auditLog => auditLog.Action)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(auditLog => auditLog.EntityName)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(auditLog => auditLog.EntityId)
            .HasMaxLength(120);

        builder.Property(auditLog => auditLog.IpAddress)
            .HasMaxLength(80);

        builder.Property(auditLog => auditLog.DetailsJson)
            .HasColumnType("jsonb");

        builder.HasIndex(auditLog => auditLog.OccurredAt);
        builder.HasIndex(auditLog => auditLog.UserId);
        builder.HasIndex(auditLog => auditLog.Action);
    }
}
