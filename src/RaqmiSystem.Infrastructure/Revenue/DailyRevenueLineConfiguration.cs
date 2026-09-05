using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Infrastructure.Revenue;

public sealed class DailyRevenueLineConfiguration : IEntityTypeConfiguration<DailyRevenueLine>
{
    public void Configure(EntityTypeBuilder<DailyRevenueLine> builder)
    {
        builder.ToTable("daily_revenue_lines", "exploitation", table =>
        {
            // CAST pour la même raison que BudgetLineConfiguration : le fournisseur SQLite des
            // tests stocke decimal en TEXT et une comparaison texte/entier n'y garderait rien.
            table.HasCheckConstraint(
                "ck_daily_revenue_lines_amount_non_negative",
                "CAST(amount AS numeric) >= 0");
        });

        builder.HasKey(line => line.Id);

        // ValueGeneratedNever est porteur, pas décoratif (même raison que BudgetLine) : la ligne
        // choisit son Id avant d'être persistée ; laissé à la convention, une ligne ajoutée à une
        // recette déjà persistée serait suivie comme Modified et l'UPDATE échouerait.
        builder.Property(line => line.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(line => line.DailyRevenueId)
            .HasColumnName("daily_revenue_id")
            .IsRequired();

        builder.Property(line => line.CategoryCode)
            .HasColumnName("category_code")
            .HasMaxLength(RevenueCategoryCodes.MaxLength)
            .IsRequired();

        builder.Property(line => line.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2);

        // Un montant par recette et par catégorie : DailyRevenue.UpdateLines ajuste la ligne
        // existante plutôt que d'en ajouter une seconde, contradictoire.
        builder.HasIndex(line => new { line.DailyRevenueId, line.CategoryCode })
            .IsUnique()
            .HasDatabaseName("ux_daily_revenue_lines_revenue_category");

        builder.HasIndex(line => line.CategoryCode)
            .HasDatabaseName("ix_daily_revenue_lines_category_code");

        // Restrict : une catégorie référencée par une recette ne se supprime pas, elle se
        // désactive (RevenueCategoryService refuse la suppression avant d'en arriver là).
        builder.HasOne<RevenueCategory>()
            .WithMany()
            .HasForeignKey(line => line.CategoryCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
