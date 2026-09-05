using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Billing;

namespace RaqmiSystem.Infrastructure.Billing;

public sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        // No "vat_rate IN (0, 9, 19)" check constraint here on purpose: SQLite (used by the
        // integration-test harness) stores decimals as TEXT, so a numeric IN-list would reject
        // every row there. The allowed-rates invariant is enforced by the InvoiceLine constructor.
        builder.ToTable("invoice_lines", "finance", table =>
        {
            table.HasCheckConstraint(
                "ck_invoice_lines_line_number_positive",
                "line_number >= 1");
        });

        builder.HasKey(line => line.Id);

        builder.Property(line => line.Id).HasColumnName("id");

        builder.Property(line => line.InvoiceId)
            .HasColumnName("invoice_id")
            .IsRequired();

        builder.Property(line => line.LineNumber)
            .HasColumnName("line_number");

        builder.Property(line => line.Designation)
            .HasColumnName("designation")
            .HasMaxLength(300)
            .IsRequired();

        // Nullable : les factures anterieures au catalogue et les lignes libres n'en portent pas.
        // Pas de cle etrangere vers catalog.articles : la ligne fige ce qu'elle a repris de
        // l'article, et un article supprime ou renomme ne doit pas empecher de relire une facture.
        builder.Property(line => line.ArticleCode)
            .HasColumnName("article_code")
            .HasMaxLength(40);

        builder.Property(line => line.Quantity)
            .HasColumnName("quantity")
            .HasPrecision(18, 3);

        builder.Property(line => line.UnitPrice)
            .HasColumnName("unit_price")
            .HasPrecision(18, 2);

        builder.Property(line => line.VatRate)
            .HasColumnName("vat_rate")
            .HasPrecision(5, 2);

        builder.Property(line => line.LineTotalExclVat)
            .HasColumnName("line_total_excl_vat")
            .HasPrecision(18, 2);

        // Stockee (et non plus derivee a la lecture) : la TVA d'une ligne emise est un montant
        // legal, et une ligne construite depuis un TTC la fige au centime. La migration qui ajoute
        // la colonne doit la remplir pour les lignes existantes : round(line_total_excl_vat *
        // vat_rate / 100, 2), la regle que la propriete calculait jusqu'ici.
        builder.Property(line => line.VatAmount)
            .HasColumnName("vat_amount")
            .HasPrecision(18, 2);

        builder.Ignore(line => line.LineTotalInclVat);

        builder.HasIndex(line => line.InvoiceId)
            .HasDatabaseName("ix_invoice_lines_invoice_id");

        // Chemin d'acces des statistiques de vente par article.
        builder.HasIndex(line => line.ArticleCode)
            .HasDatabaseName("ix_invoice_lines_article_code");
    }
}
