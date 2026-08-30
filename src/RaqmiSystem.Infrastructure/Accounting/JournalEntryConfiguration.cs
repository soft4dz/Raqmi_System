using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Accounting;

namespace RaqmiSystem.Infrastructure.Accounting;

public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("journal_entries", "accounting", table =>
        {
            table.HasCheckConstraint(
                "ck_journal_entries_status",
                "status IN ('Draft', 'Posted', 'Cancelled')");

            // The CAST is not cosmetic: the SQLite provider used by the test harness stores
            // decimal as TEXT, and '-5.00' >= 0 compares as TEXT there. Casting to numeric first
            // makes the very same constraint text mean the same thing on both providers.
            // See ApplicationSettingsConfiguration for the same pattern.
            table.HasCheckConstraint(
                "ck_journal_entries_totals_positive",
                "CAST(total_debit AS numeric) >= 0 AND CAST(total_credit AS numeric) >= 0");

            // An entry cannot be its own reversal.
            table.HasCheckConstraint(
                "ck_journal_entries_reverses_not_self",
                "reverses_entry_id IS NULL OR reverses_entry_id <> id");
        });

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id).HasColumnName("id");
        builder.Property(entry => entry.CreatedAt).HasColumnName("created_at");
        builder.Property(entry => entry.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(entry => entry.UpdatedAt).HasColumnName("updated_at");
        builder.Property(entry => entry.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(entry => entry.EntryDate)
            .HasColumnName("entry_date");

        builder.Property(entry => entry.JournalCode)
            .HasColumnName("journal_code")
            .HasMaxLength(AccountingJournal.MaxCodeLength)
            .IsRequired();

        builder.Property(entry => entry.Label)
            .HasColumnName("label")
            .HasMaxLength(JournalEntry.MaxLabelLength)
            .IsRequired();

        builder.Property(entry => entry.Reference)
            .HasColumnName("reference")
            .HasMaxLength(JournalEntry.MaxReferenceLength);

        builder.Property(entry => entry.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(entry => entry.TotalDebit)
            .HasColumnName("total_debit")
            .HasPrecision(18, 2);

        builder.Property(entry => entry.TotalCredit)
            .HasColumnName("total_credit")
            .HasPrecision(18, 2);

        builder.Property(entry => entry.ReversesEntryId).HasColumnName("reverses_entry_id");
        builder.Property(entry => entry.ReversedByEntryId).HasColumnName("reversed_by_entry_id");

        builder.Property(entry => entry.PostedAt).HasColumnName("posted_at");
        builder.Property(entry => entry.PostedBy).HasColumnName("posted_by").HasMaxLength(160);
        builder.Property(entry => entry.ReversedAt).HasColumnName("reversed_at");
        builder.Property(entry => entry.ReversedBy).HasColumnName("reversed_by").HasMaxLength(160);
        builder.Property(entry => entry.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(entry => entry.CancelledBy).HasColumnName("cancelled_by").HasMaxLength(160);
        builder.Property(entry => entry.CancellationReason)
            .HasColumnName("cancellation_reason")
            .HasMaxLength(500);

        builder.Ignore(entry => entry.IsBalanced);
        builder.Ignore(entry => entry.CanEdit);
        builder.Ignore(entry => entry.IsReversed);

        // An entry can be reversed AT MOST ONCE. Only the reversing entry carries a value here
        // and both PostgreSQL and SQLite treat NULLs as distinct in a unique index, so this
        // constrains reversals only - and it is what makes two concurrent reversals of the same
        // entry collide at the database rather than both being recorded (AccountingService turns
        // the resulting unique violation into a 409).
        builder.HasIndex(entry => entry.ReversesEntryId)
            .IsUnique()
            .HasDatabaseName("ux_journal_entries_reverses_entry_id");

        builder.HasIndex(entry => entry.EntryDate)
            .HasDatabaseName("ix_journal_entries_entry_date");

        builder.HasIndex(entry => entry.JournalCode)
            .HasDatabaseName("ix_journal_entries_journal_code");

        builder.HasIndex(entry => entry.Status)
            .HasDatabaseName("ix_journal_entries_status");

        builder.HasOne<AccountingJournal>()
            .WithMany()
            .HasPrincipalKey(journal => journal.Code)
            .HasForeignKey(entry => entry.JournalCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(entry => entry.Lines)
            .WithOne()
            .HasForeignKey(line => line.JournalEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Lines is an IReadOnlyCollection backed by the _lines field; EF must mutate the field,
        // never the read-only projection.
        builder.Navigation(entry => entry.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
