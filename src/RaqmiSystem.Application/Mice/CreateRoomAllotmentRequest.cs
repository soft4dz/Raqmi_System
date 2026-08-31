namespace RaqmiSystem.Application.Mice;

/// <summary>
/// Pose un bloc de chambres pour un groupe.
///
/// <paramref name="ReleaseDate"/> est la date au-dela de laquelle le solde invendu retourne a la
/// vente. La laisser nulle est un ENGAGEMENT FERME : les chambres restent bloquees jusqu'au depart,
/// meme si le groupe ne les prend jamais.
/// </summary>
public sealed record CreateRoomAllotmentRequest(
    string HotelUnitCode,
    string Reference,
    string CustomerCode,
    string RoomTypeCode,
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    int RoomsHeld,
    DateOnly? ReleaseDate,
    string? Notes);
