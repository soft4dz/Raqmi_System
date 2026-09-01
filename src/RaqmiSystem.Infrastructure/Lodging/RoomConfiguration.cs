using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms", "lodging");

        builder.HasKey(room => room.Id);

        builder.Property(room => room.Id).HasColumnName("id");
        builder.Property(room => room.CreatedAt).HasColumnName("created_at");
        builder.Property(room => room.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(room => room.UpdatedAt).HasColumnName("updated_at");
        builder.Property(room => room.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(room => room.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(room => room.Number)
            .HasColumnName("number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(room => room.RoomTypeCode)
            .HasColumnName("room_type_code")
            .HasMaxLength(40)
            .IsRequired();

        // Bornes alignees sur Room.NormalizeOptional (domaine). L'etage est une
        // chaine et non un entier : "RDC", "Mezzanine" et "-1" sont des etages.
        builder.Property(room => room.Floor)
            .HasColumnName("floor")
            .HasMaxLength(20);

        builder.Property(room => room.Notes)
            .HasColumnName("notes")
            .HasMaxLength(300);

        builder.Property(room => room.IsActive)
            .HasColumnName("is_active");

        // Room numbers repeat across units (every hotel has a room 101) but never within one.
        builder.HasIndex(room => new { room.HotelUnitCode, room.Number })
            .IsUnique()
            .HasDatabaseName("ux_rooms_hotel_unit_code_number");

        builder.HasIndex(room => room.HotelUnitCode)
            .HasDatabaseName("ix_rooms_hotel_unit_code");

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(room => room.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite FK on (hotel_unit_code, room_type_code) -> room_types (hotel_unit_code, code):
        // a room can only reference a type OF ITS OWN UNIT, whatever the caller sends.
        builder.HasOne<RoomType>()
            .WithMany()
            .HasPrincipalKey(roomType => new { roomType.HotelUnitCode, roomType.Code })
            .HasForeignKey(room => new { room.HotelUnitCode, room.RoomTypeCode })
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(room => room.MaxExtraBeds).HasColumnName("max_extra_beds");
        builder.Property(room => room.MaxCots).HasColumnName("max_cots");

        // Localisation physique et attributs commerciaux (passe PMS). Bornes alignees sur
        // Room.SetLocation / Room.SetAttributes : sans elles la base accepterait ce que
        // l'entite refuse.
        builder.Property(room => room.Building).HasColumnName("building").HasMaxLength(60);
        builder.Property(room => room.Wing).HasColumnName("wing").HasMaxLength(60);
        builder.Property(room => room.InternalCode).HasColumnName("internal_code").HasMaxLength(40);
        builder.Property(room => room.View).HasColumnName("view").HasMaxLength(40);
        builder.Property(room => room.Amenities).HasColumnName("amenities").HasMaxLength(400);
        builder.Property(room => room.IsAccessible).HasColumnName("is_accessible");
        builder.Property(room => room.IsSmoking).HasColumnName("is_smoking");
        builder.Property(room => room.DisplayOrder).HasColumnName("display_order");

        // Le code interne sert aux rapprochements automatiques (serrures, PABX, ancien systeme) :
        // s'il pouvait designer deux chambres, un rapprochement viserait la mauvaise. Index filtre,
        // parce que la plupart des chambres n'en portent pas.
        builder.HasIndex(room => new { room.HotelUnitCode, room.InternalCode })
            .IsUnique()
            .HasFilter("internal_code IS NOT NULL")
            .HasDatabaseName("ux_rooms_hotel_unit_code_internal_code");

        // Derive de la presence de lignes de couchage : aucun indicateur stocke, qui finirait par
        // contredire la collection.
        builder.Ignore(room => room.OverridesBeds);

        builder.HasMany(room => room.Beds)
            .WithOne()
            .HasForeignKey(bed => bed.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(room => room.Beds).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
