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
    }
}
