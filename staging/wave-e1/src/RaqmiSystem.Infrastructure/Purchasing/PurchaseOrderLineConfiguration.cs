using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Purchasing;

namespace RaqmiSystem.Infrastructure.Purchasing;

public sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("purchase_order_lines", "purchasing", table =>
        {
            table.HasCheckConstraint(
                "ck_purchase_order_lines_line_number_positive",
                "line_number >= 1");

            // The CASTs are not cosmetic: the SQLite provider used by the test harness stores
            // decimals as TEXT, and bare numeric comparisons would misbehave there. The last
            // clause is the database-level twin of the over-receipt rule: the cumulative
            // received quantity can never exceed the ordered quantity.
            table.HasCheckConstraint(
                "ck_purchase_order_lines_quantities",
                "CAST(quantity AS numeric) > 0 " +
                "AND CAST(unit_price AS numeric) >= 0 " +
                "AND CAST(quantity_received AS numeric) >= 0 " +
                "AND CAST(quantity_received AS numeric) <= CAST(quantity AS numeric)");
        });

        builder.HasKey(line => line.Id);

        builder.Property(line => line.Id).HasColumnName("id");

        builder.Property(line => line.PurchaseOrderId)
            .HasColumnName("purchase_order_id")
            .IsRequired();

        builder.Property(line => line.LineNumber)
            .HasColumnName("line_number");

        builder.Property(line => line.ItemCode)
            .HasColumnName("item_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(line => line.Designation)
            .HasColumnName("designation")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(line => line.Quantity)
            .HasColumnName("quantity")
            .HasPrecision(18, 3);

        builder.Property(line => line.UnitPrice)
            .HasColumnName("unit_price")
            .HasPrecision(18, 2);

        builder.Property(line => line.LineTotalExclVat)
            .HasColumnName("line_total_excl_vat")
            .HasPrecision(18, 2);

        builder.Property(line => line.QuantityReceived)
            .HasColumnName("quantity_received")
            .HasPrecision(18, 3);

        builder.Ignore(line => line.RemainingQuantity);
        builder.Ignore(line => line.IsFullyReceived);

        builder.HasIndex(line => line.PurchaseOrderId)
            .HasDatabaseName("ix_purchase_order_lines_purchase_order_id");

        builder.HasIndex(line => line.ItemCode)
            .HasDatabaseName("ix_purchase_order_lines_item_code");
    }
}
