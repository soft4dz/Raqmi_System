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
    int MaxCots = 0);
