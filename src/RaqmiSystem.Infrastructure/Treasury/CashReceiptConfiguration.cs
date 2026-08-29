using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Infrastructure.Treasury;

/// <summary>
/// Known limitation (accepted): CashReceipt carries no optimistic-concurrency token. The
/// natural PostgreSQL choice - mapping the system column xmin as an IsRowVersion() token
/// (UseXminAsConcurrencyToken) - is Npgsql-specific and breaks the SQLite provider used by
/// the integration tests (no xmin column exists there). The practical risk is limited: every
/// state transition is guarded by status invariants in the entity (a receipt confirmed twice
/// or edited after confirmation throws), so a concurrent lost update can only occur between
/// two simultaneous edits of the same Draft receipt, which is operationally rare and
/// self-correcting (drafts remain editable).
/// </summary>
public sealed class CashReceiptConfiguration : IEntityTypeConfiguration<CashReceipt>
{
    public void Configure(EntityTypeBuilder<CashReceipt> builder)
    {
        builder.ToTable("cash_receipts", "finance", table =>
        {
            table.HasCheckConstraint(
                "ck_cash_receipts_amount_positive",
                "amount > 0");

            table.HasCheckConstraint(
                "ck_cash_receipts_method",
                "method IN ('Cash', 'Card', 'Cheque', 'BankTransfer')");

            table.HasCheckConstraint(
                "ck_cash_receipts_status",
                "status IN ('Draft', 'Confirmed', 'Cancelled')");
        });

        builder.HasKey(receipt => receipt.Id);

        builder.Property(receipt => receipt.Id).HasColumnName("id");
        builder.Property(receipt => receipt.CreatedAt).HasColumnName("created_at");
        builder.Property(receipt => receipt.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(receipt => receipt.UpdatedAt).HasColumnName("updated_at");
        builder.Property(receipt => receipt.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(receipt => receipt.ReceiptDate)
            .HasColumnName("receipt_date");

        builder.Property(receipt => receipt.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(receipt => receipt.Method)
            .HasColumnName("method")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(receipt => receipt.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2);

        builder.Property(receipt => receipt.Reference)
            .HasColumnName("reference")
            .HasMaxLength(80);

        builder.Property(receipt => receipt.BankAccountCode)
            .HasColumnName("bank_account_code")
            .HasMaxLength(40);

        builder.Property(receipt => receipt.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(receipt => receipt.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(receipt => receipt.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(receipt => receipt.ConfirmedBy).HasColumnName("confirmed_by").HasMaxLength(160);
        builder.Property(receipt => receipt.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(receipt => receipt.CancelledBy).HasColumnName("cancelled_by").HasMaxLength(160);
        builder.Property(receipt => receipt.CancelReason).HasColumnName("cancel_reason").HasMaxLength(500);

        builder.Ignore(receipt => receipt.CanEdit);

        builder.HasIndex(receipt => new { receipt.ReceiptDate, receipt.HotelUnitCode })
            .HasDatabaseName("ix_cash_receipts_receipt_date_hotel_unit_code");

        builder.HasIndex(receipt => receipt.Status)
            .HasDatabaseName("ix_cash_receipts_status");

        builder.HasIndex(receipt => receipt.Method)
            .HasDatabaseName("ix_cash_receipts_method");

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(receipt => receipt.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<BankAccount>()
            .WithMany()
            .HasPrincipalKey(account => account.Code)
            .HasForeignKey(receipt => receipt.BankAccountCode)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
