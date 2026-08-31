using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Kitchen;

namespace RaqmiSystem.Infrastructure.Kitchen;

public sealed class RecipeSheetConfiguration : IEntityTypeConfiguration<RecipeSheet>
{
    public void Configure(EntityTypeBuilder<RecipeSheet> builder)
    {
        builder.ToTable("recipe_sheets", "kitchen", table =>
        {
            table.HasCheckConstraint(
                "ck_recipe_sheets_category",
                "category IN ('Entree', 'Plat', 'Dessert', 'Boisson', 'SousPreparation')");

            table.HasCheckConstraint(
                "ck_recipe_sheets_yield_portions_positive",
                "yield_portions >= 1");
        });

        builder.HasKey(recipe => recipe.Id);

        builder.Property(recipe => recipe.Id).HasColumnName("id");
        builder.Property(recipe => recipe.CreatedAt).HasColumnName("created_at");
        builder.Property(recipe => recipe.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(recipe => recipe.UpdatedAt).HasColumnName("updated_at");
        builder.Property(recipe => recipe.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(recipe => recipe.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(recipe => recipe.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(recipe => recipe.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(recipe => recipe.YieldPortions)
            .HasColumnName("yield_portions");

        // Free-text allergen mentions: deliberately no regulatory nomenclature (see the
        // entity's doc comment) - just a bounded input field.
        builder.Property(recipe => recipe.Allergens)
            .HasColumnName("allergens")
            .HasMaxLength(300);

        builder.Property(recipe => recipe.Instructions)
            .HasColumnName("instructions")
            .HasMaxLength(4000);

        builder.Property(recipe => recipe.IsActive)
            .HasColumnName("is_active");

        builder.HasIndex(recipe => recipe.Code)
            .IsUnique()
            .HasDatabaseName("ux_recipe_sheets_code");

        builder.HasIndex(recipe => recipe.Category)
            .HasDatabaseName("ix_recipe_sheets_category");

        builder.HasMany(recipe => recipe.Ingredients)
            .WithOne()
            .HasForeignKey(ingredient => ingredient.RecipeSheetId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ingredients is an IReadOnlyCollection backed by the _ingredients field; EF must
        // mutate the field, never the read-only projection (same as Invoice.Lines).
        builder.Navigation(recipe => recipe.Ingredients)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
