namespace RaqmiSystem.Application.Documents;

/// <summary>
/// Une ligne du recapitulatif par taux de TVA : la base HT et la TVA du taux. Ce sont des sommes
/// de montants de lignes deja calcules par le serveur (LineTotalExclVat, VatAmount), jamais un
/// recalcul base x taux.
/// </summary>
public sealed record InvoiceVatSummaryModel(
    decimal VatRate,
    decimal BaseExclVat,
    decimal VatAmount);
