using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Infrastructure.Inventory;

public sealed class InventoryCountConfiguration : IEntityTypeConfiguration<InventoryCount>
{
    public void Configure(EntityTypeBuilder<InventoryCount> builder)
    {
        builder.ToTable("inventory_counts", "inventory", table =>
        {
            table.HasCheckConstraint(
                "ck_inventory_counts_status",
                "status IN ('Draft', 'Validated')");
        });

        builder.HasKey(count => count.Id);

        builder.Property(count => count.Id).HasColumnName("id");
        builder.Property(count => count.CreatedAt).HasColumnName("created_at");
        builder.Property(count => count.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(count => count.UpdatedAt).HasColumnName("updated_at");
        builder.Property(count => count.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(count => count.WarehouseCode)
            .HasColumnName("warehouse_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(count => count.CountDate)
            .HasColumnName("count_date");

        builder.Property(count => count.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(count => count.ValidatedAt).HasColumnName("validated_at");
        builder.Property(count => count.ValidatedBy).HasColumnName("validated_by").HasMaxLength(160);

        builder.Ignore(count => count.CanEdit);

        builder.HasIndex(count => count.WarehouseCode)
            .HasDatabaseName("ix_inventory_counts_warehouse_code");

        builder.HasIndex(count => count.Status)
            .HasDatabaseName("ix_inventory_counts_status");

        builder.HasIndex(count => count.CountDate)
            .HasDatabaseName("ix_inventory_counts_count_date");

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasPrincipalKey(warehouse => warehouse.Code)
            .HasForeignKey(count => count.WarehouseCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(count => count.Lines)
            .WithOne()
            .HasForeignKey(line => line.InventoryCountId)
            .OnDelete(DeleteBehavior.Cascade);

        // Lines is an IReadOnlyCollection backed by the _lines field; EF must mutate the
        // field, never the read-only projection.
        builder.Navigation(count => count.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
