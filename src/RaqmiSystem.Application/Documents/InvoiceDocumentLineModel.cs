namespace RaqmiSystem.Application.Documents;

/// <summary>
/// Une ligne du gabarit de facture. Chaque montant est recopie de la ligne de facture servie par
/// le module Facturation : le gabarit n'a rien a multiplier ni a arrondir.
/// </summary>
public sealed record InvoiceDocumentLineModel(
    int LineNumber,
    string Designation,
    decimal Quantity,
    decimal UnitPrice,
    decimal VatRate,
    decimal LineTotalExclVat);
