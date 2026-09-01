namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Affectation d'une chambre physique a un dossier vendu par type. <paramref name="RoomId"/> nul
/// LIBERE la chambre et remet le dossier en attente d'affectation - geste courant quand on
/// reorganise le plan la veille des arrivees.
/// </summary>
public sealed record AssignRoomRequest(Guid? RoomId, string? Reason = null);
