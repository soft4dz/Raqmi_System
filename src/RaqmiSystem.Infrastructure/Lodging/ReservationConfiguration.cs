using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations", "lodging", table =>
        {
            table.HasCheckConstraint(
                "ck_reservations_status",
                "status IN ('Booked', 'CheckedIn', 'CheckedOut', 'Cancelled', 'NoShow')");

            // At least one night: the half-open [arrival, departure) period must be non-empty.
            table.HasCheckConstraint(
                "ck_reservations_dates",
                "departure_date > arrival_date");

            table.HasCheckConstraint(
                "ck_reservations_guest_count",
                "guest_count > 0");

            // CAST because the SQLite test provider stores decimal as TEXT (same technique as
            // the treasury/billing amount constraints).
            table.HasCheckConstraint(
                "ck_reservations_nightly_rate",
                "CAST(nightly_rate_snapshot AS numeric) >= 0");
        });

        builder.HasKey(reservation => reservation.Id);

        builder.Property(reservation => reservation.Id).HasColumnName("id");
        builder.Property(reservation => reservation.CreatedAt).HasColumnName("created_at");
        builder.Property(reservation => reservation.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(reservation => reservation.UpdatedAt).HasColumnName("updated_at");
        builder.Property(reservation => reservation.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(reservation => reservation.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(reservation => reservation.RoomId)
            .HasColumnName("room_id");

        builder.Property(reservation => reservation.CustomerCode)
            .HasColumnName("customer_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(reservation => reservation.ArrivalDate)
            .HasColumnName("arrival_date");

        builder.Property(reservation => reservation.DepartureDate)
            .HasColumnName("departure_date");

        builder.Property(reservation => reservation.GuestCount)
            .HasColumnName("guest_count");

        builder.Property(reservation => reservation.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(reservation => reservation.NightlyRateSnapshot)
            .HasColumnName("nightly_rate_snapshot")
            .HasPrecision(18, 2);

        builder.Property(reservation => reservation.RatePlanCodeSnapshot)
            .HasColumnName("rate_plan_code_snapshot")
            .HasMaxLength(60)
            .IsRequired();

        // Per-night frozen rate detail (JSON array, one entry per night), written once at
        // creation. Nullable: rows created before this column existed keep billing the flat
        // nightly_rate_snapshot, which GetNightlyRates falls back to.
        builder.Property(reservation => reservation.NightlyRatesSnapshotJson)
            .HasColumnName("nightly_rates_snapshot");

        builder.Property(reservation => reservation.CancelReason)
            .HasColumnName("cancel_reason")
            .HasMaxLength(500);

        builder.Property(reservation => reservation.CheckedInAt).HasColumnName("checked_in_at");
        builder.Property(reservation => reservation.CheckedInBy).HasColumnName("checked_in_by").HasMaxLength(160);
        builder.Property(reservation => reservation.CheckedOutAt).HasColumnName("checked_out_at");
        builder.Property(reservation => reservation.CheckedOutBy).HasColumnName("checked_out_by").HasMaxLength(160);
        builder.Property(reservation => reservation.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(reservation => reservation.CancelledBy).HasColumnName("cancelled_by").HasMaxLength(160);
        builder.Property(reservation => reservation.NoShowAt).HasColumnName("no_show_at");
        builder.Property(reservation => reservation.NoShowBy).HasColumnName("no_show_by").HasMaxLength(160);

        builder.Ignore(reservation => reservation.Nights);
        builder.Ignore(reservation => reservation.IsBlocking);
        builder.Ignore(reservation => reservation.TotalStayAmount);

        // The anti-double-booking check scans the reservations of one room over a period; the
        // rest of the module lists by unit, status, customer and dates.
        builder.HasIndex(reservation => new { reservation.RoomId, reservation.ArrivalDate })
            .HasDatabaseName("ix_reservations_room_id_arrival_date");

        builder.HasIndex(reservation => reservation.HotelUnitCode)
            .HasDatabaseName("ix_reservations_hotel_unit_code");

        builder.HasIndex(reservation => reservation.Status)
            .HasDatabaseName("ix_reservations_status");

        builder.HasIndex(reservation => reservation.CustomerCode)
            .HasDatabaseName("ix_reservations_customer_code");

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(reservation => reservation.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(reservation => reservation.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>()
            .WithMany()
            .HasPrincipalKey(customer => customer.Code)
            .HasForeignKey(reservation => reservation.CustomerCode)
            .OnDelete(DeleteBehavior.Restrict);

        // Rattachement au bloc de groupe. Nullable : la vente publique n'en a pas. Aucune cle
        // etrangere declaree vers room_allotments a dessein - un allotement annule ne doit pas
        // pouvoir effacer en cascade des reservations bien reelles.
        builder.Property(reservation => reservation.AllotmentId).HasColumnName("allotment_id");

        builder.Property(reservation => reservation.GuestName)
            .HasColumnName("guest_name")
            .HasMaxLength(160);

        builder.HasIndex(reservation => reservation.AllotmentId, "ix_reservations_allotment_id")
            .HasDatabaseName("ix_reservations_allotment_id");
    }
}
