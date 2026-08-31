using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Mice;

namespace RaqmiSystem.Infrastructure.Mice;

public sealed class EventBookingConfiguration : IEntityTypeConfiguration<EventBooking>
{
    public void Configure(EntityTypeBuilder<EventBooking> builder)
    {
        builder.ToTable("event_bookings", "lodging", table =>
        {
            table.HasCheckConstraint("ck_event_bookings_duration", "duration_minutes > 0");
            table.HasCheckConstraint(
                "ck_event_bookings_buffers",
                "setup_minutes >= 0 AND teardown_minutes >= 0");
            table.HasCheckConstraint("ck_event_bookings_attendance", "expected_attendance > 0");

            // La fenetre d'occupation est derivee du creneau : cette contrainte est le filet qui
            // empeche une ecriture directe en base de creer un evenement finissant avant de
            // commencer, ce qui rendrait le garde de chevauchement inoperant.
            table.HasCheckConstraint("ck_event_bookings_window", "occupied_to > occupied_from");
        });

        builder.HasKey(booking => booking.Id);

        builder.Property(booking => booking.Id).HasColumnName("id");

        builder.Property(booking => booking.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(booking => booking.Reference)
            .HasColumnName("reference")
            .HasMaxLength(EventBooking.ReferenceMaxLength)
            .IsRequired();

        builder.Property(booking => booking.FunctionSpaceCode)
            .HasColumnName("function_space_code")
            .HasMaxLength(FunctionSpace.CodeMaxLength)
            .IsRequired();

        builder.Property(booking => booking.CustomerCode)
            .HasColumnName("customer_code")
            .HasMaxLength(EventBooking.CustomerCodeMaxLength)
            .IsRequired();

        builder.Property(booking => booking.Title)
            .HasColumnName("title")
            .HasMaxLength(EventBooking.TitleMaxLength)
            .IsRequired();

        builder.Property(booking => booking.EventDate).HasColumnName("event_date");
        builder.Property(booking => booking.StartTime).HasColumnName("start_time");
        builder.Property(booking => booking.DurationMinutes).HasColumnName("duration_minutes");
        builder.Property(booking => booking.SetupMinutes).HasColumnName("setup_minutes");
        builder.Property(booking => booking.TeardownMinutes).HasColumnName("teardown_minutes");

        // DateTime et non DateTimeOffset : ce sont des heures d'HORLOGE MURALE de l'hotel, sans
        // decalage a porter. Ce choix rend aussi la comparaison traduisible par le fournisseur
        // SQLite du harnais de test, qui refuse de comparer un DateTimeOffset - or c'est exactement
        // sur ces deux colonnes que le garde anti-double-reservation compare.
        builder.Property(booking => booking.OccupiedFrom).HasColumnName("occupied_from").IsRequired();
        builder.Property(booking => booking.OccupiedTo).HasColumnName("occupied_to").IsRequired();

        builder.Property(booking => booking.SetupStyle)
            .HasColumnName("setup_style")
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(booking => booking.ExpectedAttendance).HasColumnName("expected_attendance");

        builder.Property(booking => booking.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(booking => booking.Notes)
            .HasColumnName("notes")
            .HasMaxLength(EventBooking.NotesMaxLength);

        builder.Property(booking => booking.CancelReason)
            .HasColumnName("cancel_reason")
            .HasMaxLength(EventBooking.CancelReasonMaxLength);

        builder.Property(booking => booking.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(booking => booking.CancelledBy).HasColumnName("cancelled_by").HasMaxLength(160);
        builder.Property(booking => booking.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(booking => booking.ConfirmedBy).HasColumnName("confirmed_by").HasMaxLength(160);
        builder.Property(booking => booking.InvoiceId).HasColumnName("invoice_id");

        builder.Property(booking => booking.CreatedAt).HasColumnName("created_at");
        builder.Property(booking => booking.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(booking => booking.UpdatedAt).HasColumnName("updated_at");
        builder.Property(booking => booking.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.HasMany(booking => booking.Lines)
            .WithOne()
            .HasForeignKey(line => line.EventBookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(booking => booking.Schedule)
            .WithOne()
            .HasForeignKey(item => item.EventBookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(booking => booking.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(booking => booking.Schedule).UsePropertyAccessMode(PropertyAccessMode.Field);

        // La reference est unique PAR UNITE : c'est elle qui est imprimee sur le BEO et citee au
        // telephone, deux evenements ne peuvent pas la partager.
        builder.HasIndex(booking => new { booking.HotelUnitCode, booking.Reference }, "ux_event_bookings_unit_reference")
            .IsUnique()
            .HasDatabaseName("ux_event_bookings_unit_reference");

        // Index de travail du garde de chevauchement et du planning.
        builder.HasIndex(
                booking => new { booking.HotelUnitCode, booking.FunctionSpaceCode, booking.OccupiedFrom },
                "ix_event_bookings_space_window")
            .HasDatabaseName("ix_event_bookings_space_window");
    }
}
