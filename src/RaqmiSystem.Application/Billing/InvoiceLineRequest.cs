namespace RaqmiSystem.Application.Billing;

/// <summary>
/// Une ligne de facture a saisir. Deux formes cohabitent :
/// <list type="bullet">
///   <item>la ligne LIBRE (pas de <see cref="ArticleCode"/>) : designation, prix unitaire et
///         taux de TVA sont obligatoires - c'est la forme historique, inchangee ;</item>
///   <item>la ligne d'ARTICLE (<see cref="ArticleCode"/> renseigne) : la designation, le prix
///         et le taux sont repris de l'article du catalogue quand ils sont omis, et peuvent
///         etre surcharges un a un (remise negociee, libelle precise).</item>
/// </list>
/// <see cref="VatAmount"/> fige la TVA d'une ligne construite depuis un montant TTC (ligne de
/// folio) pour que la facture retombe au centime sur ce que le client doit ; null dans le cas
/// general, ou la TVA est calculee HT x taux. Voir InvoiceLine.
/// </summary>
public sealed record InvoiceLineRequest(
    string? Designation,
    decimal Quantity,
    decimal? UnitPrice = null,
    decimal? VatRate = null,
    string? ArticleCode = null,
    decimal? VatAmount = null);
