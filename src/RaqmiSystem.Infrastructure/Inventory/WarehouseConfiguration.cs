using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Inventory;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Inventory;

public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("warehouses", "inventory");

        builder.HasKey(warehouse => warehouse.Id);

        builder.Property(warehouse => warehouse.Id).HasColumnName("id");
        builder.Property(warehouse => warehouse.CreatedAt).HasColumnName("created_at");
        builder.Property(warehouse => warehouse.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(warehouse => warehouse.UpdatedAt).HasColumnName("updated_at");
        builder.Property(warehouse => warehouse.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(warehouse => warehouse.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(warehouse => warehouse.Label)
            .HasColumnName("label")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(warehouse => warehouse.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(warehouse => warehouse.IsActive)
            .HasColumnName("is_active");

        builder.HasIndex(warehouse => warehouse.Code)
            .IsUnique()
            .HasDatabaseName("ux_warehouses_code");

        builder.HasIndex(warehouse => warehouse.HotelUnitCode)
            .HasDatabaseName("ix_warehouses_hotel_unit_code");

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(warehouse => warehouse.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
