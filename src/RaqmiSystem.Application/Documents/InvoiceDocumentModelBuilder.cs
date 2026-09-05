using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Settings;
using RaqmiSystem.Domain.Billing;

namespace RaqmiSystem.Application.Documents;

/// <summary>
/// Construit le modele du gabarit de facture a partir de ce que servent les modules proprietaires
/// des donnees : la facture (IBillingService), la fiche client (IBillingService) et le parametrage
/// global (IApplicationSettingsService). Fonction pure, sans acces base ni HTTP : testable seule.
///
/// Regle absolue : AUCUN montant n'est recalcule ici. Les lignes et les totaux sont recopies de la
/// facture ; le recapitulatif par taux additionne des montants de lignes deja calcules par le
/// serveur. Si la facture et ses lignes etaient incoherentes, le document imprimerait les totaux
/// de la facture - c'est elle la piece de reference, pas ce constructeur.
/// </summary>
public static class InvoiceDocumentModelBuilder
{
    public static InvoiceDocumentModel Build(
        InvoiceResponse invoice,
        CustomerResponse? customer,
        ApplicationSettingsResponse settings)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(settings);

        // Un brouillon n'a pas de numero, donc pas de document legal : refuser ici protege le
        // gabarit d'imprimer « Facture N° » suivi de rien, quel que soit l'appelant.
        if (string.IsNullOrWhiteSpace(invoice.Number) || invoice.Status == InvoiceStatus.Draft)
        {
            throw new ArgumentException("Only an issued invoice has a legal document.", nameof(invoice));
        }

        var lines = invoice.Lines
            .OrderBy(line => line.LineNumber)
            .Select(line => new InvoiceDocumentLineModel(
                line.LineNumber,
                line.Designation,
                line.Quantity,
                line.UnitPrice,
                line.VatRate,
                line.LineTotalExclVat))
            .ToArray();

        // Sommes de montants de lignes servis par le serveur (jamais base x taux) : le gabarit
        // affiche un recapitulatif, il ne refait pas la TVA.
        var vatSummary = invoice.Lines
            .GroupBy(line => line.VatRate)
            .OrderBy(group => group.Key)
            .Select(group => new InvoiceVatSummaryModel(
                group.Key,
                group.Sum(line => line.LineTotalExclVat),
                group.Sum(line => line.VatAmount)))
            .ToArray();

        return new InvoiceDocumentModel(
            invoice.Id,
            invoice.Number,
            invoice.InvoiceDate,
            invoice.IssuedAt,
            invoice.HotelUnitCode,
            BuildIssuer(invoice, settings),
            BuildCustomer(invoice, customer),
            settings.CurrencyLabel,
            lines,
            vatSummary,
            invoice.TotalExclVat,
            invoice.TotalVat,
            invoice.TotalInclVat,
            InvoiceDocumentDefaults.PaymentTermsMention,
            InvoiceDocumentDefaults.FooterMention);
    }

    /// <summary>
    /// L'identite fiscale de l'emetteur vient de l'instantane fige a l'emission (Invoice.Issuer*),
    /// jamais du parametrage courant : un changement de RC apres coup ne reecrit pas une facture
    /// emise. Seuls la ville et les coordonnees de contact - qui ne font pas partie de
    /// l'instantane et n'identifient pas fiscalement l'emetteur - sont lus dans Settings.
    /// </summary>
    private static DocumentPartyModel BuildIssuer(InvoiceResponse invoice, ApplicationSettingsResponse settings)
    {
        return new DocumentPartyModel(
            invoice.IssuerName ?? settings.CompanyName,
            invoice.IssuerAddress,
            settings.CompanyCity,
            invoice.IssuerNif,
            invoice.IssuerRc,
            invoice.IssuerAi,
            invoice.IssuerNis,
            settings.CompanyPhone,
            settings.CompanyEmail);
    }

    /// <summary>
    /// La raison sociale vient de l'instantane fige a l'emission (InvoiceResponse.CustomerName).
    /// L'adresse et les identifiants fiscaux viennent de la fiche client courante : InvoiceResponse
    /// n'expose pas encore les colonnes customer_*_snapshot de la facture (a exposer par le module
    /// Facturation, voir docs/modules/documents.md). L'archive rend malgre tout la piece stable
    /// des le premier rendu. Un particulier n'a ni NIF ni RC a imprimer.
    /// </summary>
    private static DocumentPartyModel BuildCustomer(InvoiceResponse invoice, CustomerResponse? customer)
    {
        var isCompany = customer is null || customer.CustomerType != CustomerType.Individual;

        return new DocumentPartyModel(
            invoice.CustomerName ?? customer?.Name ?? invoice.CustomerCode,
            customer?.Address,
            customer?.City,
            isCompany ? customer?.Nif : null,
            isCompany ? customer?.Rc : null,
            isCompany ? customer?.Ai : null,
            isCompany ? customer?.Nis : null,
            customer?.Phone,
            customer?.Email);
    }
}
