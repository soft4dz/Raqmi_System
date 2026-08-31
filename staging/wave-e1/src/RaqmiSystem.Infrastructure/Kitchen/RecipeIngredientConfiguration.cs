using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Kitchen;

namespace RaqmiSystem.Infrastructure.Kitchen;

public sealed class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.ToTable("recipe_ingredients", "kitchen", table =>
        {
            table.HasCheckConstraint(
                "ck_recipe_ingredients_line_number_positive",
                "line_number >= 1");

            // The CAST is not cosmetic: the SQLite provider used by the test harness stores
            // decimal as TEXT, and a bare "quantity > 0" would compare text against integer
            // there (vacuously true). Casting to numeric first makes the very same constraint
            // text mean the same thing on both providers - same pattern as
            // BudgetLineConfiguration.
            table.HasCheckConstraint(
                "ck_recipe_ingredients_quantity_positive",
                "CAST(quantity AS numeric) > 0");
        });

        builder.HasKey(ingredient => ingredient.Id);

        builder.Property(ingredient => ingredient.Id).HasColumnName("id");

        builder.Property(ingredient => ingredient.RecipeSheetId)
            .HasColumnName("recipe_sheet_id")
            .IsRequired();

        builder.Property(ingredient => ingredient.LineNumber)
            .HasColumnName("line_number");

        // References an inventory item by code. Deliberately NOT a database foreign key: the
        // inventory tables belong to the stock module, and the cross-module dependency lives
        // in the IStockCostProvider application contract (existence is verified there at save
        // time). The integrator MAY add the FK once both modules are wired, but nothing here
        // requires it.
        builder.Property(ingredient => ingredient.ItemCode)
            .HasColumnName("item_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(ingredient => ingredient.Quantity)
            .HasColumnName("quantity")
            .HasPrecision(18, 3);

        builder.Property(ingredient => ingredient.Notes)
            .HasColumnName("notes")
            .HasMaxLength(300);

        builder.HasIndex(ingredient => ingredient.RecipeSheetId)
            .HasDatabaseName("ix_recipe_ingredients_recipe_sheet_id");

        // One line per item within a recipe (the domain refuses duplicates; the index makes
        // the database agree).
        builder.HasIndex(ingredient => new { ingredient.RecipeSheetId, ingredient.ItemCode })
            .IsUnique()
            .HasDatabaseName("ux_recipe_ingredients_recipe_item");
    }
}
