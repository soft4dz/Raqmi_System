using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Housekeeping;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Housekeeping;

public sealed class MinibarConsumptionConfiguration : IEntityTypeConfiguration<MinibarConsumption>
{
    public void Configure(EntityTypeBuilder<MinibarConsumption> builder)
    {
        builder.ToTable("minibar_consumptions", "housekeeping", table =>
        {
            table.HasCheckConstraint(
                "ck_minibar_consumptions_quantity",
                "quantity > 0");

            // CAST for the SQLite test provider, as everywhere else amounts are constrained.
            table.HasCheckConstraint(
                "ck_minibar_consumptions_unit_price",
                "CAST(unit_price AS numeric) > 0");

            table.HasCheckConstraint(
                "ck_minibar_consumptions_total_amount",
                "CAST(total_amount AS numeric) > 0");
        });

        builder.HasKey(consumption => consumption.Id);

        builder.Property(consumption => consumption.Id).HasColumnName("id");
        builder.Property(consumption => consumption.CreatedAt).HasColumnName("created_at");
        builder.Property(consumption => consumption.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(consumption => consumption.UpdatedAt).HasColumnName("updated_at");
        builder.Property(consumption => consumption.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(consumption => consumption.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(consumption => consumption.RoomId)
            .HasColumnName("room_id")
            .IsRequired();

        builder.Property(consumption => consumption.RoomNumber)
            .HasColumnName("room_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(consumption => consumption.ReservationId)
            .HasColumnName("reservation_id")
            .IsRequired();

        // Commercial snapshot of the price list at recording time - the same discipline as the
        // nightly rate frozen into a reservation. Editing the price list must never rewrite what
        // a guest was charged last week.
        builder.Property(consumption => consumption.ItemCode)
            .HasColumnName("item_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(consumption => consumption.ItemLabel)
            .HasColumnName("item_label")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(consumption => consumption.UnitPrice)
            .HasColumnName("unit_price")
            .HasPrecision(18, 2);

        builder.Property(consumption => consumption.Quantity).HasColumnName("quantity");

        builder.Property(consumption => consumption.TotalAmount)
            .HasColumnName("total_amount")
            .HasPrecision(18, 2);

        builder.Property(consumption => consumption.ConsumedOn).HasColumnName("consumed_on");
        builder.Property(consumption => consumption.Notes).HasColumnName("notes").HasMaxLength(300);

        builder.HasIndex(consumption => new { consumption.HotelUnitCode, consumption.ConsumedOn })
            .HasDatabaseName("ix_minibar_consumptions_unit_consumed_on");

        // A folio line under dispute is traced back through the stay it was billed on.
        builder.HasIndex(consumption => consumption.ReservationId)
            .HasDatabaseName("ix_minibar_consumptions_reservation_id");

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(consumption => consumption.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(consumption => consumption.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Reservation>()
            .WithMany()
            .HasForeignKey(consumption => consumption.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);

        // No FK to minibar_items on purpose: the row holds a SNAPSHOT of the price list, not a
        // reference to it. A product withdrawn from the card must not make the consumptions it
        // once produced undeletable, nor drag them along when its label or price changes.
    }
}
