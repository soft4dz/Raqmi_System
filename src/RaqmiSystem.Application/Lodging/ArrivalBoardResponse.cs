namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Le tableau des arrivees du jour, avec ce qui manque pour enregistrer chaque client :
/// une chambre, une chambre PRETE, une garantie, un solde deja du.
/// </summary>
public sealed record ArrivalBoardResponse(
    string HotelUnitCode,
    DateOnly BusinessDate,
    IReadOnlyCollection<ArrivalRowResponse> Arrivals,
    int ExpectedGuests,
    int RoomsToPrepare,
    int NotReadyCount,
    int UnassignedCount);
