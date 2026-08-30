using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Accounting;

namespace RaqmiSystem.Infrastructure.Accounting;

public sealed class AccountingJournalConfiguration : IEntityTypeConfiguration<AccountingJournal>
{
    public void Configure(EntityTypeBuilder<AccountingJournal> builder)
    {
        builder.ToTable("journals", "accounting");

        builder.HasKey(journal => journal.Id);

        builder.Property(journal => journal.Id).HasColumnName("id");
        builder.Property(journal => journal.CreatedAt).HasColumnName("created_at");
        builder.Property(journal => journal.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(journal => journal.UpdatedAt).HasColumnName("updated_at");
        builder.Property(journal => journal.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(journal => journal.Code)
            .HasColumnName("code")
            .HasMaxLength(AccountingJournal.MaxCodeLength)
            .IsRequired();

        builder.Property(journal => journal.Label)
            .HasColumnName("label")
            .HasMaxLength(AccountingJournal.MaxLabelLength)
            .IsRequired();

        builder.Property(journal => journal.IsActive)
            .HasColumnName("is_active");

        // No separate unique index on Code: entries reference their journal by code, and
        // JournalEntryConfiguration's HasPrincipalKey(journal => journal.Code) already forces EF
        // to create an alternate-key unique constraint on this column - same arrangement as
        // finance.customers with InvoiceConfiguration.
        builder.HasIndex(journal => journal.IsActive)
            .HasDatabaseName("ix_journals_is_active");
    }
}
