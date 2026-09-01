namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Une journee de previsionnel. Toutes les colonnes viennent du MEME inventaire que la recherche de
/// disponibilite : un forecast qui compterait autrement finirait par contredire ce que la reception
/// peut reellement vendre.
///
/// <paramref name="Adr"/> est le prix moyen de la nuitee vendue et <paramref name="RevPar"/> le
/// revenu par chambre DISPONIBLE (capacite vendable, blocages deduits) : c'est le RevPAR qui dit si
/// l'hotel remplit bien, l'ADR seul peut monter pendant que l'etablissement se vide.
/// </summary>
public sealed record ForecastDayResponse(
    DateOnly Date,
    int PhysicalRooms,
    int OutOfOrderRooms,
    int OutOfServiceRooms,
    int SellableRooms,
    int AllotmentHolds,
    int SoldRooms,
    int Arrivals,
    int Departures,
    int StayOvers,
    int RemainingRooms,
    int OverbookingAllowed,
    int OverbookingUsed,
    decimal OccupancyPercent,
    decimal RoomRevenue,
    decimal Adr,
    decimal RevPar,
    int InHouseGuests);
