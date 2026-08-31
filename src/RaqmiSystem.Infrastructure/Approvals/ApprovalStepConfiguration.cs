using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Approvals;

namespace RaqmiSystem.Infrastructure.Approvals;

public sealed class ApprovalStepConfiguration : IEntityTypeConfiguration<ApprovalStep>
{
    public void Configure(EntityTypeBuilder<ApprovalStep> builder)
    {
        // No unique (circuit_id, rank) index on purpose: ReplaceSteps rewrites the whole
        // collection in one SaveChanges and EF Core does not guarantee deletes are flushed
        // before inserts, so a re-ordering could trip such an index transiently (same reason
        // journal_entry_lines carries no unique line-number index). Rank contiguity and
        // uniqueness are guaranteed by ApprovalCircuit.ReplaceSteps, the only writer.
        builder.ToTable("approval_steps", "approvals", table =>
        {
            table.HasCheckConstraint(
                "ck_approval_steps_rank_positive",
                "rank >= 1");
        });

        builder.HasKey(step => step.Id);

        // ValueGeneratedNever is load-bearing, not decoration (same rationale as BudgetLine,
        // JournalEntryLine and FolioCharge): ApprovalStep assigns its own Id, and UpdateAsync
        // calls ReplaceSteps on an ALREADY-PERSISTED circuit, so the new steps are discovered
        // through the navigation with their keys already set. With a value-generated key, change
        // tracking would classify them as existing rows (Modified, UPDATEs affecting 0 rows ->
        // DbUpdateConcurrencyException) instead of new ones.
        builder.Property(step => step.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(step => step.CircuitId)
            .HasColumnName("circuit_id")
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

        builder.HasIndex(step => step.CircuitId)
            .HasDatabaseName("ix_approval_steps_circuit_id");
    }
}
