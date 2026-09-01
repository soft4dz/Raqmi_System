using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class OverbookingAllowanceConfiguration : IEntityTypeConfiguration<OverbookingAllowance>
{
    public void Configure(EntityTypeBuilder<OverbookingAllowance> builder)
    {
        builder.ToTable("overbooking_allowances", "lodging", table =>
        {
            table.HasCheckConstraint("ck_overbooking_allowances_dates", "to_date >= from_date");

            table.HasCheckConstraint(
                "ck_overbooking_allowances_extra_rooms",
                "extra_rooms > 0 AND extra_rooms <= 50");
        });

        builder.HasKey(allowance => allowance.Id);

        builder.Property(allowance => allowance.Id).HasColumnName("id");
        builder.Property(allowance => allowance.CreatedAt).HasColumnName("created_at");
        builder.Property(allowance => allowance.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(allowance => allowance.UpdatedAt).HasColumnName("updated_at");
        builder.Property(allowance => allowance.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(allowance => allowance.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(allowance => allowance.RoomTypeCode)
            .HasColumnName("room_type_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(allowance => allowance.FromDate).HasColumnName("from_date");
        builder.Property(allowance => allowance.ToDate).HasColumnName("to_date");
        builder.Property(allowance => allowance.ExtraRooms).HasColumnName("extra_rooms");
        builder.Property(allowance => allowance.IsActive).HasColumnName("is_active");

        builder.Property(allowance => allowance.Notes)
            .HasColumnName("notes")
            .HasMaxLength(OverbookingAllowance.NotesMaxLength);

        builder.HasIndex(allowance => new { allowance.HotelUnitCode, allowance.RoomTypeCode, allowance.FromDate })
            .HasDatabaseName("ix_overbooking_allowances_unit_type_from");

        // Cle composee vers room_types : une autorisation ne peut viser qu'un type de SON unite.
        builder.HasOne<RoomType>()
            .WithMany()
            .HasPrincipalKey(roomType => new { roomType.HotelUnitCode, roomType.Code })
            .HasForeignKey(allowance => new { allowance.HotelUnitCode, allowance.RoomTypeCode })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(allowance => allowance.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
