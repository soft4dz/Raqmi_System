namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Un type de chambre et son couchage.
///
/// <paramref name="Capacity"/> reste la valeur de reference : c'est elle, et elle seule, que la
/// recherche de disponibilite compare au nombre de personnes. <paramref name="Beds"/> DECRIT ce
/// couchage sans jamais le contredire - le serveur refuse une composition dont le total ne
/// correspond pas a la capacite.
/// <paramref name="Beds"/> vide signifie "composition non declaree", pas "aucun lit" : les types
/// crees avant l'arrivee du couchage restent valides.
/// </summary>
public sealed record RoomTypeResponse(
    Guid Id,
    string HotelUnitCode,
    string Code,
    string Label,
    int Capacity,
    string? Description,
    bool IsActive,
    int ActiveRoomCount,
    IReadOnlyCollection<BedCompositionLine> Beds,
    int DeclaredSleeps,
    int MaxExtraBeds,
    int MaxCots,
    int MaxOccupancy,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
