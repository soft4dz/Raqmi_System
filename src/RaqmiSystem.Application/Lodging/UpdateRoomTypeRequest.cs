namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Mise a jour d'un type de chambre. <paramref name="Beds"/> REMPLACE la composition existante ;
/// une liste vide efface la declaration et ramene le type a "composition inconnue".
/// </summary>
public sealed record UpdateRoomTypeRequest(
    string Label,
    int Capacity,
    string? Description = null,
    IReadOnlyCollection<BedCompositionLine>? Beds = null,
    int MaxExtraBeds = 0,
    int MaxCots = 0,
    int MaxAdults = 0,
    int MaxChildren = 0,
    int MaxInfants = 0,
    decimal BaseRate = 0m,
    decimal SurfaceSquareMeters = 0m,
    int Rank = 0,
    IReadOnlyCollection<string>? Amenities = null,
    int DisplayOrder = 0);
