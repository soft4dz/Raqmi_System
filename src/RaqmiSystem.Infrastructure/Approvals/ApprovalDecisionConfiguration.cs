using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Approvals;

namespace RaqmiSystem.Infrastructure.Approvals;

public sealed class ApprovalDecisionConfiguration : IEntityTypeConfiguration<ApprovalDecision>
{
    public void Configure(EntityTypeBuilder<ApprovalDecision> builder)
    {
        builder.ToTable("approval_decisions", "approvals", table =>
        {
            table.HasCheckConstraint(
                "ck_approval_decisions_rank_positive",
                "rank >= 1");
        });

        builder.HasKey(decision => decision.Id);

        // ValueGeneratedNever is load-bearing, not decoration (same rationale as BudgetLine,
        // JournalEntryLine and FolioCharge): ApprovalDecision assigns its own Id, and a decision
        // is ALWAYS added to an already-persisted instance (DecideAsync loads the instance before
        // instance.Decide). Discovered through the navigation with its key already set, a
        // value-generated key would make change tracking classify it as an existing row
        // (Modified, an UPDATE affecting 0 rows -> DbUpdateConcurrencyException) instead of a new
        // one, failing every single decision.
        builder.Property(decision => decision.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(decision => decision.InstanceId)
            .HasColumnName("instance_id")
            .IsRequired();

        builder.Property(decision => decision.Rank)
            .HasColumnName("rank");

        builder.Property(decision => decision.StepLabel)
            .HasColumnName("step_label")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(decision => decision.DecidedBy)
            .HasColumnName("decided_by")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(decision => decision.Approved)
            .HasColumnName("approved");

        builder.Property(decision => decision.Comment)
            .HasColumnName("comment")
            .HasMaxLength(500);

        builder.Property(decision => decision.DecidedAt)
            .HasColumnName("decided_at");

        builder.HasIndex(decision => decision.InstanceId)
            .HasDatabaseName("ix_approval_decisions_instance_id");

        // One decision per step of an instance, restated at the database level: the second of
        // two concurrent decisions on the same step must fail here rather than record a
        // duplicate verdict (the service maps the violation to a clean 409).
        builder.HasIndex(decision => new { decision.InstanceId, decision.Rank })
            .IsUnique()
            .HasDatabaseName("ux_approval_decisions_instance_rank");
    }
}
