using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Housekeeping;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Housekeeping;

public sealed class MinibarItemConfiguration : IEntityTypeConfiguration<MinibarItem>
{
    public void Configure(EntityTypeBuilder<MinibarItem> builder)
    {
        builder.ToTable("minibar_items", "housekeeping", table =>
        {
            // CAST because the SQLite test provider stores decimal as TEXT (same technique as
            // the treasury/billing/lodging amount constraints). Strictly positive, because a
            // consumption of this item becomes a folio line and a folio line is never zero.
            table.HasCheckConstraint(
                "ck_minibar_items_unit_price",
                "CAST(unit_price AS numeric) > 0");
        });

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at");
        builder.Property(item => item.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.Property(item => item.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(item => item.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(item => item.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(item => item.Label)
            .HasColumnName("label")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(item => item.UnitPrice)
            .HasColumnName("unit_price")
            .HasPrecision(18, 2);

        builder.Property(item => item.IsActive).HasColumnName("is_active");

        // Item codes repeat across units (every hotel sells water) but never within one.
        builder.HasIndex(item => new { item.HotelUnitCode, item.Code })
            .IsUnique()
            .HasDatabaseName("ux_minibar_items_hotel_unit_code_code");

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(item => item.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
