using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Infrastructure.Revenue;

public sealed class RevenueCategoryConfiguration : IEntityTypeConfiguration<RevenueCategory>
{
    public void Configure(EntityTypeBuilder<RevenueCategory> builder)
    {
        builder.ToTable("revenue_categories", "exploitation", table =>
        {
            table.HasCheckConstraint(
                "ck_revenue_categories_sector",
                "sector IS NULL OR sector IN ('Hospitality', 'Retail', 'Services', 'Manufacturing', 'Education', 'Health', 'Other')");

            table.HasCheckConstraint(
                "ck_revenue_categories_display_order_non_negative",
                "display_order >= 0");
        });

        // Le code est la clé : c'est lui que portent les lignes de recettes et de budget, et il ne
        // change jamais. Une clé technique séparée n'apporterait qu'une jointure de plus partout.
        builder.HasKey(category => category.Code);

        builder.Property(category => category.Code)
            .HasColumnName("code")
            .HasMaxLength(RevenueCategoryCodes.MaxLength)
            .ValueGeneratedNever();

        builder.Property(category => category.Label)
            .HasColumnName("label")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(category => category.DisplayOrder)
            .HasColumnName("display_order");

        builder.Property(category => category.IsActive)
            .HasColumnName("is_active");

        builder.Property(category => category.Sector)
            .HasColumnName("sector")
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.HasIndex(category => category.DisplayOrder)
            .HasDatabaseName("ix_revenue_categories_display_order");

        // Jeu par défaut semé par le modèle lui-même : il arrive avec la migration qui crée la
        // table (et avec EnsureCreated dans les tests), sans dépendre d'un amorçage au démarrage.
        // Les valeurs du catalogue sont des constantes : le snapshot de modèle reste stable.
        builder.HasData(RevenueCategoryCatalog.All);
    }
}
