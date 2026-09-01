namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Changement de chambre en cours de sejour. Le motif est OBLIGATOIRE : un client deplace sans
/// raison ecrite est une reclamation qu'on ne saura pas instruire, et le housekeeping doit savoir
/// pourquoi une chambre libre est soudain a nettoyer.
/// </summary>
public sealed record RoomMoveRequest(Guid TargetRoomId, string Reason);
