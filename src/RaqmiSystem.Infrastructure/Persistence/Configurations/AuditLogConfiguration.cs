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

        builder.Property(auditLog => auditLog.Id).HasColumnName("id");
        builder.Property(auditLog => auditLog.UserId).HasColumnName("user_id");

        builder.Property(auditLog => auditLog.UserName)
            .HasColumnName("user_name")
            .HasMaxLength(160);

        builder.Property(auditLog => auditLog.Action)
            .HasColumnName("action")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(auditLog => auditLog.EntityName)
            .HasColumnName("entity_name")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(auditLog => auditLog.EntityId)
            .HasColumnName("entity_id")
            .HasMaxLength(120);

        builder.Property(auditLog => auditLog.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(80);

        builder.Property(auditLog => auditLog.DetailsJson)
            .HasColumnName("details_json")
            .HasColumnType("jsonb");

        builder.Property(auditLog => auditLog.OccurredAt)
            .HasColumnName("occurred_at");

        builder.HasIndex(auditLog => auditLog.OccurredAt);
        builder.HasIndex(auditLog => auditLog.UserId);
        builder.HasIndex(auditLog => auditLog.Action);
    }
}
