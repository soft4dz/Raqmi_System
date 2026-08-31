using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Infrastructure.Inventory;

public sealed class InventoryCountLineConfiguration : IEntityTypeConfiguration<InventoryCountLine>
{
    public void Configure(EntityTypeBuilder<InventoryCountLine> builder)
    {
        builder.ToTable("inventory_count_lines", "inventory", table =>
        {
            table.HasCheckConstraint(
                "ck_inventory_count_lines_line_number_positive",
                "line_number >= 1");

            // CAST for the SQLite test provider's TEXT-stored decimals - same pattern as
            // BudgetLineConfiguration.
            table.HasCheckConstraint(
                "ck_inventory_count_lines_counted_quantity_non_negative",
                "CAST(counted_quantity AS numeric) >= 0");
        });

        builder.HasKey(line => line.Id);

        builder.Property(line => line.Id).HasColumnName("id");

        builder.Property(line => line.InventoryCountId)
            .HasColumnName("inventory_count_id")
            .IsRequired();

        builder.Property(line => line.LineNumber)
            .HasColumnName("line_number");

        builder.Property(line => line.ItemCode)
            .HasColumnName("item_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(line => line.CountedQuantity)
            .HasColumnName("counted_quantity")
            .HasPrecision(18, 3);

        // One line per item within a count: the entity refuses duplicates
        // (InventoryCount.ReplaceLines) and the database refuses them too.
        builder.HasIndex(line => new { line.InventoryCountId, line.ItemCode })
            .IsUnique()
            .HasDatabaseName("ux_inventory_count_lines_count_item");

        builder.HasOne<StockItem>()
            .WithMany()
            .HasPrincipalKey(item => item.Code)
            .HasForeignKey(line => line.ItemCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
