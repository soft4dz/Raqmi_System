using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Accounting;

namespace RaqmiSystem.Infrastructure.Accounting;

public sealed class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> builder)
    {
        builder.ToTable("journal_entry_lines", "accounting", table =>
        {
            table.HasCheckConstraint(
                "ck_journal_entry_lines_line_number_positive",
                "line_number >= 1");

            // The one-side-only rule of double-entry bookkeeping, restated at the database level
            // so it holds for rows this module did not write either. Read as: both amounts are
            // positive-or-zero, AND exactly one of them is zero (boolean inequality is XOR) -
            // which rules out both "debit and credit at once" and "neither".
            //
            // The CASTs are not cosmetic: the SQLite provider used by the test harness stores
            // decimal as TEXT, where '0.00' = 0 is false and '-5.00' >= 0 compares as TEXT.
            // Casting to numeric first makes the very same constraint text mean the same thing
            // on both providers. See ApplicationSettingsConfiguration for the same pattern.
            table.HasCheckConstraint(
                "ck_journal_entry_lines_debit_credit_exclusive",
                "CAST(debit AS numeric) >= 0 AND CAST(credit AS numeric) >= 0 " +
                "AND (CAST(debit AS numeric) = 0) <> (CAST(credit AS numeric) = 0)");
        });

        builder.HasKey(line => line.Id);

        // ValueGeneratedNever is load-bearing: JournalEntryLine assigns its own Id, and a line
        // added to an already-persisted entry would otherwise be discovered by change detection
        // with its key already set, which EF reads as "this row exists" - tracking it as Modified
        // and emitting an UPDATE against a row that was never inserted. Same reasoning, at more
        // length, in BudgetLineConfiguration. No schema change: the column has no database default
        // either way.
        builder.Property(line => line.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(line => line.JournalEntryId)
            .HasColumnName("journal_entry_id")
            .IsRequired();

        builder.Property(line => line.LineNumber)
            .HasColumnName("line_number");

        builder.Property(line => line.AccountCode)
            .HasColumnName("account_code")
            .HasMaxLength(ChartAccount.MaxCodeLength)
            .IsRequired();

        builder.Property(line => line.Label)
            .HasColumnName("label")
            .HasMaxLength(JournalEntryLine.MaxLabelLength)
            .IsRequired();

        builder.Property(line => line.Debit)
            .HasColumnName("debit")
            .HasPrecision(18, 2);

        builder.Property(line => line.Credit)
            .HasColumnName("credit")
            .HasPrecision(18, 2);

        builder.HasIndex(line => line.JournalEntryId)
            .HasDatabaseName("ix_journal_entry_lines_journal_entry_id");

        // The trial balance groups by account code over a period: this index is what keeps it
        // from scanning the whole ledger.
        builder.HasIndex(line => line.AccountCode)
            .HasDatabaseName("ix_journal_entry_lines_account_code");

        // Restrict, not Cascade: an account that has ever been posted to cannot be deleted out
        // from under the entries that reference it. Accounts are deactivated instead.
        builder.HasOne<ChartAccount>()
            .WithMany()
            .HasPrincipalKey(account => account.Code)
            .HasForeignKey(line => line.AccountCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
