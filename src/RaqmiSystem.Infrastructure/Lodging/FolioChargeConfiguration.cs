using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class FolioChargeConfiguration : IEntityTypeConfiguration<FolioCharge>
{
    public void Configure(EntityTypeBuilder<FolioCharge> builder)
    {
        builder.ToTable("folio_charges", "lodging", table =>
        {
            table.HasCheckConstraint(
                "ck_folio_charges_kind",
                "kind IN ('Night', 'Extra', 'Settlement', 'Adjustment', 'Tax', 'Package')");

            // La regle de signe du domaine, redite pour tout ecrivain qui contournerait l'entite :
            // pas de ligne a zero, et un montant negatif seulement sur Settlement/Adjustment. CAST
            // parce que le fournisseur SQLite des tests stocke les decimaux en TEXT.
            table.HasCheckConstraint(
                "ck_folio_charges_amount_nonzero",
                "CAST(amount AS numeric) <> 0");

            table.HasCheckConstraint(
                "ck_folio_charges_amount_sign",
                "kind IN ('Settlement', 'Adjustment') OR CAST(amount AS numeric) > 0");

            table.HasCheckConstraint(
                "ck_folio_charges_quantity",
                "CAST(quantity AS numeric) > 0");

            table.HasCheckConstraint(
                "ck_folio_charges_vat_rate",
                "vat_rate IS NULL OR CAST(vat_rate AS numeric) IN (0, 9, 19)");
        });

        builder.HasKey(charge => charge.Id);

        // ValueGeneratedNever est porteur, pas decoratif (meme raison que BudgetLine et
        // JournalEntryLine) : FolioCharge s'attribue son propre Id, et une ligne ajoutee a un folio
        // DEJA SUIVI est decouverte par la navigation avec sa cle deja renseignee. Avec une cle
        // generee, le suivi de changements classerait cette ligne comme existante (Modified, donc
        // un UPDATE affectant zero ligne -> DbUpdateConcurrencyException) au lieu de nouvelle.
        builder.Property(charge => charge.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(charge => charge.FolioId)
            .HasColumnName("folio_id");

        builder.Property(charge => charge.LineNumber)
            .HasColumnName("line_number");

        builder.Property(charge => charge.ChargeDate)
            .HasColumnName("charge_date");

        builder.Property(charge => charge.BusinessDate)
            .HasColumnName("business_date");

        builder.Property(charge => charge.Label)
            .HasColumnName("label")
            .HasMaxLength(FolioCharge.LabelMaxLength)
            .IsRequired();

        builder.Property(charge => charge.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2);

        builder.Property(charge => charge.Quantity)
            .HasColumnName("quantity")
            .HasPrecision(12, 3);

        builder.Property(charge => charge.VatRate)
            .HasColumnName("vat_rate")
            .HasPrecision(5, 2);

        builder.Property(charge => charge.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(charge => charge.ExtraCode)
            .HasColumnName("extra_code")
            .HasMaxLength(ExtraItem.CodeMaxLength);

        builder.Property(charge => charge.Reference)
            .HasColumnName("reference")
            .HasMaxLength(FolioCharge.ReferenceMaxLength);

        builder.Property(charge => charge.SourceReference)
            .HasColumnName("source_reference")
            .HasMaxLength(FolioCharge.SourceReferenceMaxLength);

        builder.Ignore(charge => charge.AmountExclVat);
        builder.Ignore(charge => charge.VatAmount);

        builder.HasIndex(charge => charge.FolioId)
            .HasDatabaseName("ix_folio_charges_folio_id");

        // LE VERROU D'IDEMPOTENCE DU NIGHT AUDIT. Un geste - "la nuitee du 14 aout du dossier X" -
        // ne peut produire qu'UNE ligne sur un folio donne. C'est cet index, et non un controle
        // applicatif, qui garantit qu'un night audit relance ne double jamais une nuitee : meme un
        // second passage lance en parallele du premier heurte la contrainte au lieu d'inserer.
        // Filtre, parce que les lignes saisies a la main n'ont pas de reference de geste.
        builder.HasIndex(charge => new { charge.FolioId, charge.SourceReference })
            .IsUnique()
            .HasFilter("source_reference IS NOT NULL")
            .HasDatabaseName("ux_folio_charges_folio_id_source_reference");
    }
}
