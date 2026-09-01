namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Un constat du night audit. <paramref name="IsBlocking"/> distingue ce qui EMPECHE de cloturer -
/// une arrivee non traitee laisserait une chambre facturee a personne - de ce qui merite seulement
/// d'etre signale.
/// </summary>
public sealed record NightAuditFindingResponse(
    string Code,
    string Message,
    bool IsBlocking,
    Guid? ReservationId,
    string? RoomNumber);
