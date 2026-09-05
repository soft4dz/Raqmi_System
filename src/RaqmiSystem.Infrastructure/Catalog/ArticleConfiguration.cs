using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Catalog;
using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Infrastructure.Catalog;

public sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.ToTable("articles", "catalog", table =>
        {
            // Le CAST n'est pas cosmetique : le fournisseur SQLite du harnais de test stocke les
            // decimaux en TEXT, et une comparaison texte/entier n'y veut pas dire ce qu'elle dit.
            // Meme motif que StockItemConfiguration.
            table.HasCheckConstraint(
                "ck_articles_unit_price_non_negative",
                "CAST(unit_price_excl_vat AS numeric) >= 0");

            // Le lien vers le stock est tout ou rien (voir Article.RequireStockLink) ; la base le
            // garantit aussi pour qu'aucun autre chemin d'ecriture ne puisse le contourner.
            // Ecrit sans "= TRUE" pour dire la meme chose sur PostgreSQL (boolean) et SQLite
            // (entier 0/1) - meme precedent que TemperatureReadingConfiguration.
            table.HasCheckConstraint(
                "ck_articles_stock_link",
                "(tracks_stock AND stock_item_code IS NOT NULL) OR (NOT tracks_stock AND stock_item_code IS NULL)");

            // Pas de contrainte "vat_rate IN (0, 9, 19)" : SQLite stocke les decimaux en TEXT et
            // refuserait toutes les lignes. La regle est tenue par le constructeur de l'entite,
            // qui delegue a InvoiceLine.RequireAllowedVatRate - meme decision que InvoiceLineConfiguration.
        });

        builder.HasKey(article => article.Id);

        builder.Property(article => article.Id).HasColumnName("id");
        builder.Property(article => article.CreatedAt).HasColumnName("created_at");
        builder.Property(article => article.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(article => article.UpdatedAt).HasColumnName("updated_at");
        builder.Property(article => article.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(article => article.Code)
            .HasColumnName("code")
            .HasMaxLength(Article.CodeMaxLength)
            .IsRequired();

        builder.Property(article => article.Designation)
            .HasColumnName("designation")
            .HasMaxLength(Article.DesignationMaxLength)
            .IsRequired();

        builder.Property(article => article.Family)
            .HasColumnName("family")
            .HasMaxLength(Article.FamilyMaxLength);

        builder.Property(article => article.UnitOfMeasure)
            .HasColumnName("unit_of_measure")
            .HasMaxLength(Article.UnitOfMeasureMaxLength)
            .IsRequired();

        builder.Property(article => article.VatRate)
            .HasColumnName("vat_rate")
            .HasPrecision(5, 2);

        builder.Property(article => article.UnitPriceExclVat)
            .HasColumnName("unit_price_excl_vat")
            .HasPrecision(18, 2);

        builder.Property(article => article.TracksStock)
            .HasColumnName("tracks_stock");

        builder.Property(article => article.StockItemCode)
            .HasColumnName("stock_item_code")
            .HasMaxLength(40);

        builder.Property(article => article.IsActive)
            .HasColumnName("is_active");

        builder.HasIndex(article => article.Code)
            .IsUnique()
            .HasDatabaseName("ux_articles_code");

        builder.HasIndex(article => article.Family)
            .HasDatabaseName("ix_articles_family");

        builder.HasIndex(article => article.IsActive)
            .HasDatabaseName("ix_articles_is_active");

        // La cle alternative sur stock_items.code existe deja (StockMovementConfiguration la
        // declare) ; ce lien s'y adosse. Restrict : on ne supprime pas un article de stock que
        // le catalogue vend encore.
        builder.HasOne<StockItem>()
            .WithMany()
            .HasPrincipalKey(item => item.Code)
            .HasForeignKey(article => article.StockItemCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
