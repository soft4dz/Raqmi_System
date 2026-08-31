using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Crm;

namespace RaqmiSystem.Infrastructure.Crm;

public sealed class LoyaltyTransactionConfiguration : IEntityTypeConfiguration<LoyaltyTransaction>
{
    public void Configure(EntityTypeBuilder<LoyaltyTransaction> builder)
    {
        builder.ToTable("loyalty_transactions", "crm", table =>
        {
            // The sign rule of the ledger (LoyaltyTransaction.RequireSignMatchingKind), restated
            // where the rows actually land. It doubles as the kind check: a kind outside the four
            // known ones satisfies no branch and is refused.
            table.HasCheckConstraint(
                "ck_loyalty_transactions_sign",
                "(kind = 'Earn' AND points > 0) " +
                "OR (kind IN ('Redeem', 'Expiry') AND points < 0) " +
                "OR (kind = 'Adjustment' AND points <> 0)");
        });

        builder.HasKey(movement => movement.Id);

        builder.Property(movement => movement.Id).HasColumnName("id");
        builder.Property(movement => movement.CreatedAt).HasColumnName("created_at");
        builder.Property(movement => movement.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(movement => movement.UpdatedAt).HasColumnName("updated_at");
        builder.Property(movement => movement.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(movement => movement.CustomerCode)
            .HasColumnName("customer_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(movement => movement.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(movement => movement.Points).HasColumnName("points");

        builder.Property(movement => movement.OccurredOn).HasColumnName("occurred_on");

        builder.Property(movement => movement.Reason)
            .HasColumnName("reason")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(movement => movement.Reference)
            .HasColumnName("reference")
            .HasMaxLength(80);

        // The balance of a guest is a SUM over this index, and the statement reads the same rows
        // in date order.
        builder.HasIndex(movement => new { movement.CustomerCode, movement.OccurredOn })
            .HasDatabaseName("ix_loyalty_transactions_customer_code_occurred_on");

        builder.HasOne<Customer>()
            .WithMany()
            .HasPrincipalKey(customer => customer.Code)
            .HasForeignKey(movement => movement.CustomerCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
