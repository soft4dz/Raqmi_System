namespace RaqmiSystem.Application.Documents;

/// <summary>
/// Tout ce que le gabarit de facture imprime, et rien d'autre. Le modele est construit cote
/// serveur par <see cref="InvoiceDocumentModelBuilder"/> a partir de la facture servie par le
/// module Facturation : les totaux sont ceux de la facture, recopies tels quels (charte : les
/// chiffres viennent du serveur, le gabarit ne calcule aucun montant).
///
/// Le modele ne porte volontairement PAS le statut de la facture (emise, reglee, annulee) : la
/// piece archivee est figee au premier rendu, et un statut qui change ensuite (un reglement, une
/// annulation) ferait mentir une piece qu'on ne re-rend jamais. Le reglement est un fait distinct
/// de la facture ; il aura sa propre piece.
/// </summary>
public sealed record InvoiceDocumentModel(
    Guid InvoiceId,
    string Number,
    DateOnly InvoiceDate,
    DateTimeOffset? IssuedAt,
    string HotelUnitCode,
    DocumentPartyModel Issuer,
    DocumentPartyModel Customer,
    string CurrencyLabel,
    IReadOnlyList<InvoiceDocumentLineModel> Lines,
    IReadOnlyList<InvoiceVatSummaryModel> VatSummary,
    decimal TotalExclVat,
    decimal TotalVat,
    decimal TotalInclVat,
    string PaymentTerms,
    string FooterMentions);
