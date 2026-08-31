using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class RoomTypeConfiguration : IEntityTypeConfiguration<RoomType>
{
    public void Configure(EntityTypeBuilder<RoomType> builder)
    {
        builder.ToTable("room_types", "lodging", table =>
        {
            table.HasCheckConstraint("ck_room_types_capacity", "capacity > 0");
        });

        builder.HasKey(roomType => roomType.Id);

        builder.Property(roomType => roomType.Id).HasColumnName("id");
        builder.Property(roomType => roomType.CreatedAt).HasColumnName("created_at");
        builder.Property(roomType => roomType.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(roomType => roomType.UpdatedAt).HasColumnName("updated_at");
        builder.Property(roomType => roomType.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(roomType => roomType.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(roomType => roomType.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(roomType => roomType.Label)
            .HasColumnName("label")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(roomType => roomType.Capacity)
            .HasColumnName("capacity");

        // Bornes alignees sur RoomType.NormalizeOptional (domaine) : sans elles la
        // colonne serait creee sans limite et la base accepterait ce que l'entite
        // refuse.
        builder.Property(roomType => roomType.Description)
            .HasColumnName("description")
            .HasMaxLength(300);

        builder.Property(roomType => roomType.IsActive)
            .HasColumnName("is_active");

        // The type code is unique PER UNIT, not globally. Declared as an alternate key (rather
        // than a plain unique index) because Room's composite foreign key targets this pair -
        // which is also what enforces "a room's type exists within the room's own unit" at the
        // database level.
        builder.HasAlternateKey(roomType => new { roomType.HotelUnitCode, roomType.Code });

        builder.HasIndex(roomType => roomType.HotelUnitCode)
            .HasDatabaseName("ix_room_types_hotel_unit_code");

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(roomType => roomType.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(roomType => roomType.MaxExtraBeds).HasColumnName("max_extra_beds");
        builder.Property(roomType => roomType.MaxCots).HasColumnName("max_cots");

        // Totaux derives du couchage : calcules a la lecture, jamais stockes.
        builder.Ignore(roomType => roomType.DeclaredSleeps);
        builder.Ignore(roomType => roomType.MaxOccupancy);

        builder.HasMany(roomType => roomType.Beds)
            .WithOne()
            .HasForeignKey(bed => bed.RoomTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(roomType => roomType.Beds).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
