namespace RaqmiSystem.Application.Billing;

/// <summary>
/// Parametres de l'emission. Le corps est optionnel sur la route : une facture qui ne porte
/// aucune ligne d'article suivi en stock s'emet comme avant, sans rien preciser.
/// </summary>
/// <param name="WarehouseCode">
/// Magasin d'ou sortent les quantites des lignes d'articles suivis en stock. Obligatoire des
/// qu'une telle ligne existe ; ignore sinon.
/// </param>
public sealed record IssueInvoiceRequest(string? WarehouseCode = null);
