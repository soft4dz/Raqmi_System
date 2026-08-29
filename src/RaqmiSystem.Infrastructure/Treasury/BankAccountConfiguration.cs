using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Infrastructure.Treasury;

public sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("bank_accounts", "finance");

        builder.HasKey(account => account.Id);

        builder.Property(account => account.Id).HasColumnName("id");
        builder.Property(account => account.CreatedAt).HasColumnName("created_at");
        builder.Property(account => account.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(account => account.UpdatedAt).HasColumnName("updated_at");
        builder.Property(account => account.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(account => account.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(account => account.Label)
            .HasColumnName("label")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(account => account.BankName)
            .HasColumnName("bank_name")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(account => account.AccountNumber)
            .HasColumnName("account_number")
            .HasMaxLength(34)
            .IsRequired();

        builder.Property(account => account.IsActive)
            .HasColumnName("is_active");

        // No separate unique index on Code: CashReceiptConfiguration and
        // PaymentOrderConfiguration both target Code with HasPrincipalKey, which forces EF to
        // create an alternate-key unique constraint on this column (same technique as
        // HotelUnitConfiguration).
        builder.HasIndex(account => account.IsActive)
            .HasDatabaseName("ix_bank_accounts_is_active");
    }
}
