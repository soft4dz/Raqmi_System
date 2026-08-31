using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Infrastructure.Inventory;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements", "inventory", table =>
        {
            table.HasCheckConstraint(
                "ck_stock_movements_kind",
                "kind IN ('PurchaseEntry', 'Consumption', 'TransferOut', 'TransferIn', 'InventoryAdjustment')");

            // The CAST is not cosmetic: the SQLite provider used by the test harness stores
            // decimal as TEXT, and a text-versus-integer comparison there does not mean what
            // it says. Casting to numeric first makes the very same constraint text mean the
            // same thing on both providers - same pattern as BudgetLineConfiguration.
            table.HasCheckConstraint(
                "ck_stock_movements_quantity_positive",
                "CAST(quantity AS numeric) > 0");

            table.HasCheckConstraint(
                "ck_stock_movements_unit_cost_non_negative",
                "unit_cost IS NULL OR CAST(unit_cost AS numeric) >= 0");

            // A purchase entry without a cost cannot feed the weighted average: the domain
            // factory refuses it, and the database refuses it too so no other write path can
            // ever sneak one in.
            table.HasCheckConstraint(
                "ck_stock_movements_purchase_entry_costed",
                "kind <> 'PurchaseEntry' OR unit_cost IS NOT NULL");

            // The adjustment direction exists exactly when the movement is an adjustment.
            table.HasCheckConstraint(
                "ck_stock_movements_adjustment_direction",
                "(kind = 'InventoryAdjustment' AND adjustment_is_increase IS NOT NULL) OR " +
                "(kind <> 'InventoryAdjustment' AND adjustment_is_increase IS NULL)");
        });

        builder.HasKey(movement => movement.Id);

        builder.Property(movement => movement.Id).HasColumnName("id");
        builder.Property(movement => movement.CreatedAt).HasColumnName("created_at");
        builder.Property(movement => movement.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(movement => movement.UpdatedAt).HasColumnName("updated_at");
        builder.Property(movement => movement.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(movement => movement.WarehouseCode)
            .HasColumnName("warehouse_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(movement => movement.ItemCode)
            .HasColumnName("item_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(movement => movement.MovementDate)
            .HasColumnName("movement_date");

        builder.Property(movement => movement.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(movement => movement.Quantity)
            .HasColumnName("quantity")
            .HasPrecision(18, 3);

        builder.Property(movement => movement.UnitCost)
            .HasColumnName("unit_cost")
            .HasPrecision(18, 2);

        builder.Property(movement => movement.Reference)
            .HasColumnName("reference")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(movement => movement.LotNumber)
            .HasColumnName("lot_number")
            .HasMaxLength(60);

        builder.Property(movement => movement.ExpiryDate)
            .HasColumnName("expiry_date");

        builder.Property(movement => movement.Notes)
            .HasColumnName("notes")
            .HasMaxLength(500);

        builder.Property(movement => movement.AdjustmentIsIncrease)
            .HasColumnName("adjustment_is_increase");

        builder.Property(movement => movement.TransferGroupId)
            .HasColumnName("transfer_group_id");

        // Derived, never stored: the signed quantity is the direction rule applied to the
        // stored positive quantity (see StockMovement.SignedQuantity).
        builder.Ignore(movement => movement.SignedQuantity);

        // The workhorse index: current stock of a (warehouse, item) pair is the sum of its
        // movements, so that pair is the access path of every stock read and outflow guard.
        builder.HasIndex(movement => new { movement.WarehouseCode, movement.ItemCode })
            .HasDatabaseName("ix_stock_movements_warehouse_item");

        builder.HasIndex(movement => movement.MovementDate)
            .HasDatabaseName("ix_stock_movements_movement_date");

        builder.HasIndex(movement => movement.Kind)
            .HasDatabaseName("ix_stock_movements_kind");

        builder.HasIndex(movement => movement.TransferGroupId)
            .HasDatabaseName("ix_stock_movements_transfer_group_id");

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasPrincipalKey(warehouse => warehouse.Code)
            .HasForeignKey(movement => movement.WarehouseCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StockItem>()
            .WithMany()
            .HasPrincipalKey(item => item.Code)
            .HasForeignKey(movement => movement.ItemCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
