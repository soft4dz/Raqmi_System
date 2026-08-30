using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Accounting;

namespace RaqmiSystem.Infrastructure.Accounting;

public sealed class ChartAccountConfiguration : IEntityTypeConfiguration<ChartAccount>
{
    public void Configure(EntityTypeBuilder<ChartAccount> builder)
    {
        builder.ToTable("chart_accounts", "accounting", table =>
        {
            table.HasCheckConstraint(
                "ck_chart_accounts_account_class",
                "account_class BETWEEN 1 AND 7");

            table.HasCheckConstraint(
                "ck_chart_accounts_kind",
                "kind IN ('Asset', 'Liability', 'Equity', 'Revenue', 'Expense')");
        });

        builder.HasKey(account => account.Id);

        builder.Property(account => account.Id).HasColumnName("id");
        builder.Property(account => account.CreatedAt).HasColumnName("created_at");
        builder.Property(account => account.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(account => account.UpdatedAt).HasColumnName("updated_at");
        builder.Property(account => account.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(account => account.Code)
            .HasColumnName("code")
            .HasMaxLength(ChartAccount.MaxCodeLength)
            .IsRequired();

        builder.Property(account => account.Label)
            .HasColumnName("label")
            .HasMaxLength(ChartAccount.MaxLabelLength)
            .IsRequired();

        builder.Property(account => account.AccountClass)
            .HasColumnName("account_class");

        builder.Property(account => account.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(account => account.IsActive)
            .HasColumnName("is_active");

        // No separate unique index on Code: journal entry lines reference an account by code (a
        // chart of accounts is read and typed by humans in terms of its codes), and
        // JournalEntryLineConfiguration's HasPrincipalKey(account => account.Code) already forces
        // EF to create an alternate-key unique constraint on this column - same arrangement as
        // finance.customers with InvoiceConfiguration. That constraint is what turns a duplicate
        // code into the 409 that AccountingService.CreateAccountAsync returns.
        builder.HasIndex(account => account.AccountClass)
            .HasDatabaseName("ix_chart_accounts_account_class");
    }
}
