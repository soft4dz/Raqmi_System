using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class DepositConfiguration : IEntityTypeConfiguration<Deposit>
{
    public void Configure(EntityTypeBuilder<Deposit> builder)
    {
        builder.ToTable("deposits", "lodging", table =>
        {
            table.HasCheckConstraint("ck_deposits_amount", "CAST(amount AS numeric) > 0");

            table.HasCheckConstraint(
                "ck_deposits_status",
                "status IN ('Requested', 'Paid', 'Applied', 'Refunded', 'Forfeited')");
        });

        builder.HasKey(deposit => deposit.Id);

        builder.Property(deposit => deposit.Id).HasColumnName("id");
        builder.Property(deposit => deposit.CreatedAt).HasColumnName("created_at");
        builder.Property(deposit => deposit.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(deposit => deposit.UpdatedAt).HasColumnName("updated_at");
        builder.Property(deposit => deposit.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(deposit => deposit.ReservationId).HasColumnName("reservation_id");
        builder.Property(deposit => deposit.Amount).HasColumnName("amount").HasPrecision(18, 2);
        builder.Property(deposit => deposit.DueDate).HasColumnName("due_date");

        builder.Property(deposit => deposit.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(deposit => deposit.PaidDate).HasColumnName("paid_date");

        builder.Property(deposit => deposit.PaymentMethod)
            .HasColumnName("payment_method")
            .HasMaxLength(Deposit.PaymentMethodMaxLength);

        builder.Property(deposit => deposit.Reference)
            .HasColumnName("reference")
            .HasMaxLength(Deposit.ReferenceMaxLength);

        builder.Property(deposit => deposit.AppliedToFolioId).HasColumnName("applied_to_folio_id");
        builder.Property(deposit => deposit.AppliedAt).HasColumnName("applied_at");
        builder.Property(deposit => deposit.AppliedBy).HasColumnName("applied_by").HasMaxLength(160);
        builder.Property(deposit => deposit.RefundedDate).HasColumnName("refunded_date");

        builder.Property(deposit => deposit.ClosingReason)
            .HasColumnName("closing_reason")
            .HasMaxLength(Deposit.NotesMaxLength);

        builder.Property(deposit => deposit.Notes)
            .HasColumnName("notes")
            .HasMaxLength(Deposit.NotesMaxLength);

        builder.Ignore(deposit => deposit.IsAvailableForApplication);

        builder.HasIndex(deposit => deposit.ReservationId)
            .HasDatabaseName("ix_deposits_reservation_id");

        builder.HasIndex(deposit => deposit.Status)
            .HasDatabaseName("ix_deposits_status");

        builder.HasOne<Reservation>()
            .WithMany()
            .HasForeignKey(deposit => deposit.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
