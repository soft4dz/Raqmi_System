using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Approvals;

namespace RaqmiSystem.Infrastructure.Approvals;

public sealed class ApprovalInstanceConfiguration : IEntityTypeConfiguration<ApprovalInstance>
{
    public void Configure(EntityTypeBuilder<ApprovalInstance> builder)
    {
        builder.ToTable("approval_instances", "approvals", table =>
        {
            table.HasCheckConstraint(
                "ck_approval_instances_status",
                "status IN ('InProgress', 'Approved', 'Rejected')");

            table.HasCheckConstraint(
                "ck_approval_instances_subject_type",
                "subject_type IN ('PaymentOrder')");
        });

        builder.HasKey(instance => instance.Id);

        builder.Property(instance => instance.Id).HasColumnName("id");
        builder.Property(instance => instance.CreatedAt).HasColumnName("created_at");
        builder.Property(instance => instance.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(instance => instance.UpdatedAt).HasColumnName("updated_at");
        builder.Property(instance => instance.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(instance => instance.SubjectType)
            .HasColumnName("subject_type")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(instance => instance.SubjectReference)
            .HasColumnName("subject_reference")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(instance => instance.CircuitCode)
            .HasColumnName("circuit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(instance => instance.CircuitLabel)
            .HasColumnName("circuit_label")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(instance => instance.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(instance => instance.ClosedAt).HasColumnName("closed_at");
        builder.Property(instance => instance.ClosedBy).HasColumnName("closed_by").HasMaxLength(160);

        builder.Ignore(instance => instance.CurrentStep);

        // The concurrency guard behind instance opening: at most ONE in-progress approval per
        // subject. Two concurrent opens race past the service's exists-check, collide here, and
        // one of them surfaces as a 409. Filtered so that a subject rejected then re-submitted
        // can accumulate several CLOSED instances over time (same filtered-unique-index pattern
        // as ux_rate_plans_default_per_unit; the literal works on PostgreSQL and SQLite alike).
        //
        // This index and the plain subject index below cover the same property pair: without an
        // explicit model-level name on each (the second HasIndex argument), EF Core would merge
        // the two calls into a single index - the same pitfall documented on
        // RatePlanConfiguration.
        builder.HasIndex(
                instance => new { instance.SubjectType, instance.SubjectReference },
                "ux_approval_instances_open_subject")
            .IsUnique()
            .HasFilter("status = 'InProgress'")
            .HasDatabaseName("ux_approval_instances_open_subject");

        // History is consulted by subject: reference lookups (the gate) and per-subject
        // timelines both start here.
        builder.HasIndex(
                instance => new { instance.SubjectType, instance.SubjectReference },
                "ix_approval_instances_subject")
            .HasDatabaseName("ix_approval_instances_subject");

        builder.HasIndex(instance => instance.Status)
            .HasDatabaseName("ix_approval_instances_status");

        builder.HasMany(instance => instance.Steps)
            .WithOne()
            .HasForeignKey(step => step.InstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(instance => instance.Decisions)
            .WithOne()
            .HasForeignKey(decision => decision.InstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Both collections are IReadOnlyCollection projections over private fields; EF must
        // mutate the fields, never the projections.
        builder.Navigation(instance => instance.Steps)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(instance => instance.Decisions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
