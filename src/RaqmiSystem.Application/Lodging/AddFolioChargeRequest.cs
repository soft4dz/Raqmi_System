using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Une ligne a ajouter au folio d'un sejour en cours. Le montant ne peut etre negatif que pour les
/// natures Settlement/Adjustment ; un Settlement doit porter le numero de piece de tresorerie dans
/// <paramref name="Reference"/> pour que le reglement reste tracable.
///
/// <paramref name="FolioId"/> nul vise le folio CLIENT du sejour, ce qui est le cas courant. Un
/// sejour a plusieurs folios - client, societe, agence - et c'est ce champ qui dit sur lequel la
/// prestation part.
/// </summary>
public sealed record AddFolioChargeRequest(
    DateOnly ChargeDate,
    string Label,
    decimal Amount,
    ChargeKind Kind,
    string? Reference = null,
    Guid? FolioId = null,
    decimal Quantity = 1m,
    decimal? VatRate = null,
    string? ExtraCode = null);
