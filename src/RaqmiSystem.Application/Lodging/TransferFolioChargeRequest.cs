namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Deplace une ligne d'un folio vers un autre folio DU MEME SEJOUR.
///
/// Le transfert n'efface rien : la ligne d'origine est contre-passee par un ajustement et une
/// nouvelle ligne est posee sur le folio cible. Supprimer la ligne serait plus simple a lire mais
/// ferait disparaitre la trace de ce qui a ete facture puis deplace - exactement ce qu'un controle
/// cherche a retrouver.
/// </summary>
public sealed record TransferFolioChargeRequest(Guid ChargeId, Guid TargetFolioId, string Reason);
