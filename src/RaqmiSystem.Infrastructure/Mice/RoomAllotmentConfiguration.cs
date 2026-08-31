using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Mice;

namespace RaqmiSystem.Infrastructure.Mice;

/// <summary>
/// La table vit dans le schema "lodging" et non "mice" : un allotement porte sur des CHAMBRES, il
/// est lu par LodgingService a chaque recherche de disponibilite, et le ranger ailleurs
/// suggererait une independance qui n'existe pas. Le module 10.6 en est proprietaire fonctionnel,
/// le module hebergement en est lecteur permanent.
/// </summary>
public sealed class RoomAllotmentConfiguration : IEntityTypeConfiguration<RoomAllotment>
{
    public void Configure(EntityTypeBuilder<RoomAllotment> builder)
    {
        builder.ToTable("room_allotments", "lodging", table =>
        {
            table.HasCheckConstraint("ck_room_allotments_period", "departure_date > arrival_date");
            table.HasCheckConstraint("ck_room_allotments_rooms", "rooms_held > 0");

            // Une date de release posterieure a l'arrivee n'a pas de sens : le release sert a
            // rendre le solde AVANT l'arrivee du groupe, pour pouvoir revendre.
            table.HasCheckConstraint(
                "ck_room_allotments_release",
                "release_date IS NULL OR release_date <= arrival_date");
        });

        builder.HasKey(allotment => allotment.Id);

        builder.Property(allotment => allotment.Id).HasColumnName("id");

        builder.Property(allotment => allotment.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(allotment => allotment.Reference)
            .HasColumnName("reference")
            .HasMaxLength(RoomAllotment.ReferenceMaxLength)
            .IsRequired();

        builder.Property(allotment => allotment.CustomerCode)
            .HasColumnName("customer_code")
            .HasMaxLength(RoomAllotment.CustomerCodeMaxLength)
            .IsRequired();

        builder.Property(allotment => allotment.RoomTypeCode)
            .HasColumnName("room_type_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(allotment => allotment.ArrivalDate).HasColumnName("arrival_date");
        builder.Property(allotment => allotment.DepartureDate).HasColumnName("departure_date");
        builder.Property(allotment => allotment.RoomsHeld).HasColumnName("rooms_held");
        builder.Property(allotment => allotment.ReleaseDate).HasColumnName("release_date");

        builder.Property(allotment => allotment.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(allotment => allotment.Notes)
            .HasColumnName("notes")
            .HasMaxLength(RoomAllotment.NotesMaxLength);

        builder.Property(allotment => allotment.CancelReason)
            .HasColumnName("cancel_reason")
            .HasMaxLength(RoomAllotment.CancelReasonMaxLength);

        builder.Property(allotment => allotment.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(allotment => allotment.ConfirmedBy).HasColumnName("confirmed_by").HasMaxLength(160);
        builder.Property(allotment => allotment.ClosedAt).HasColumnName("closed_at");
        builder.Property(allotment => allotment.ClosedBy).HasColumnName("closed_by").HasMaxLength(160);

        builder.Property(allotment => allotment.CreatedAt).HasColumnName("created_at");
        builder.Property(allotment => allotment.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(allotment => allotment.UpdatedAt).HasColumnName("updated_at");
        builder.Property(allotment => allotment.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        // Derives de la periode et du statut : jamais stockes.
        builder.Ignore(allotment => allotment.Nights);
        builder.Ignore(allotment => allotment.IsOpen);

        builder.HasIndex(allotment => new { allotment.HotelUnitCode, allotment.Reference }, "ux_room_allotments_unit_reference")
            .IsUnique()
            .HasDatabaseName("ux_room_allotments_unit_reference");

        // Index de travail du calcul de solde : il interroge (unite, type) sur une periode a
        // chaque recherche de disponibilite, donc sur le chemin le plus chaud du PMS.
        builder.HasIndex(
                allotment => new { allotment.HotelUnitCode, allotment.RoomTypeCode, allotment.ArrivalDate },
                "ix_room_allotments_unit_type_period")
            .HasDatabaseName("ix_room_allotments_unit_type_period");
    }
}
