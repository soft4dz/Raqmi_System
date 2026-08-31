using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Infrastructure.Inventory;

public sealed class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("stock_items", "inventory", table =>
        {
            table.HasCheckConstraint(
                "ck_stock_items_category",
                "category IN ('Alimentaire', 'Boisson', 'Entretien', 'Equipement', 'Autre')");

            // The CAST is not cosmetic: the SQLite provider used by the test harness stores
            // decimal as TEXT, and a text-versus-integer comparison there does not mean what
            // it says. Casting to numeric first makes the very same constraint text mean the
            // same thing on both providers - same pattern as BudgetLineConfiguration.
            table.HasCheckConstraint(
                "ck_stock_items_minimum_quantity_non_negative",
                "CAST(minimum_quantity AS numeric) >= 0");
        });

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at");
        builder.Property(item => item.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.Property(item => item.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(item => item.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(item => item.Designation)
            .HasColumnName("designation")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(item => item.UnitOfMeasure)
            .HasColumnName("unit_of_measure")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(item => item.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(item => item.MinimumQuantity)
            .HasColumnName("minimum_quantity")
            .HasPrecision(18, 3);

        builder.Property(item => item.IsActive)
            .HasColumnName("is_active");

        builder.HasIndex(item => item.Code)
            .IsUnique()
            .HasDatabaseName("ux_stock_items_code");

        builder.HasIndex(item => item.Category)
            .HasDatabaseName("ix_stock_items_category");
    }
}
