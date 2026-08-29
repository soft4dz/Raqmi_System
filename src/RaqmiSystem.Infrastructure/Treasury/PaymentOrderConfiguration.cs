using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Infrastructure.Treasury;

/// <summary>
/// Known limitation (accepted): PaymentOrder carries no optimistic-concurrency token, for
/// the same reason as CashReceiptConfiguration - the xmin-based token
/// (UseXminAsConcurrencyToken / .Property&lt;uint&gt;("xmin").IsRowVersion()) is
/// PostgreSQL-only and would break the SQLite integration-test provider. The Draft →
/// Approved → Paid / Cancelled transitions are protected by status invariants in the entity,
/// which convert concurrent double transitions into clean validation errors rather than
/// silent lost updates.
/// </summary>
public sealed class PaymentOrderConfiguration : IEntityTypeConfiguration<PaymentOrder>
{
    public void Configure(EntityTypeBuilder<PaymentOrder> builder)
    {
        builder.ToTable("payment_orders", "finance", table =>
        {
            table.HasCheckConstraint(
                "ck_payment_orders_amount_positive",
                "amount > 0");

            table.HasCheckConstraint(
                "ck_payment_orders_status",
                "status IN ('Draft', 'Approved', 'Paid', 'Cancelled')");
        });

        builder.HasKey(order => order.Id);

        builder.Property(order => order.Id).HasColumnName("id");
        builder.Property(order => order.CreatedAt).HasColumnName("created_at");
        builder.Property(order => order.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(order => order.UpdatedAt).HasColumnName("updated_at");
        builder.Property(order => order.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(order => order.OrderDate)
            .HasColumnName("order_date");

        builder.Property(order => order.Beneficiary)
            .HasColumnName("beneficiary")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(order => order.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2);

        builder.Property(order => order.DueDate)
            .HasColumnName("due_date");

        builder.Property(order => order.BankAccountCode)
            .HasColumnName("bank_account_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(order => order.Reference)
            .HasColumnName("reference")
            .HasMaxLength(80);

        builder.Property(order => order.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(order => order.ApprovedAt).HasColumnName("approved_at");
        builder.Property(order => order.ApprovedBy).HasColumnName("approved_by").HasMaxLength(160);
        builder.Property(order => order.PaidAt).HasColumnName("paid_at");
        builder.Property(order => order.PaidBy).HasColumnName("paid_by").HasMaxLength(160);
        builder.Property(order => order.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(order => order.CancelledBy).HasColumnName("cancelled_by").HasMaxLength(160);
        builder.Property(order => order.CancelReason).HasColumnName("cancel_reason").HasMaxLength(500);

        builder.HasIndex(order => order.DueDate)
            .HasDatabaseName("ix_payment_orders_due_date");

        builder.HasIndex(order => order.Status)
            .HasDatabaseName("ix_payment_orders_status");

        builder.HasOne<BankAccount>()
            .WithMany()
            .HasPrincipalKey(account => account.Code)
            .HasForeignKey(order => order.BankAccountCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
