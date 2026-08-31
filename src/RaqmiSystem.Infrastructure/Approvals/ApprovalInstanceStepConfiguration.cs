using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Approvals;

namespace RaqmiSystem.Infrastructure.Approvals;

public sealed class ApprovalInstanceStepConfiguration : IEntityTypeConfiguration<ApprovalInstanceStep>
{
    public void Configure(EntityTypeBuilder<ApprovalInstanceStep> builder)
    {
        builder.ToTable("approval_instance_steps", "approvals", table =>
        {
            table.HasCheckConstraint(
                "ck_approval_instance_steps_rank_positive",
                "rank >= 1");
        });

        builder.HasKey(step => step.Id);

        // Same self-assigned-Id convention as the other child entities of this repository
        // (BudgetLine, JournalEntryLine, FolioCharge, ApprovalStep, ApprovalDecision). These
        // snapshot steps are only ever created together with their instance, so change tracking
        // reaches them through an Added root today; declaring the truth about the key keeps that
        // independent of the order in which a future caller builds the graph.
        builder.Property(step => step.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(step => step.InstanceId)
            .HasColumnName("instance_id")
            .IsRequired();

        builder.Property(step => step.Rank)
            .HasColumnName("rank");

        builder.Property(step => step.Label)
            .HasColumnName("label")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(step => step.RequiredRole)
            .HasColumnName("required_role")
            .HasMaxLength(80)
            .IsRequired();

        builder.HasIndex(step => step.InstanceId)
            .HasDatabaseName("ix_approval_instance_steps_instance_id");

        // Snapshot steps are written once at opening and never replaced, so the unique index is
        // safe here (no delete-then-insert rewrite can trip it, unlike circuit steps).
        builder.HasIndex(step => new { step.InstanceId, step.Rank })
            .IsUnique()
            .HasDatabaseName("ux_approval_instance_steps_instance_rank");
    }
}
