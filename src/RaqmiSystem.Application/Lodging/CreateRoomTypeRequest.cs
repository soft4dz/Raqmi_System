namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Creation d'un type de chambre. <paramref name="Beds"/> est facultatif : un type peut etre cree
/// sans composition declaree, et completee ensuite. Quand elle est fournie, son total doit egaler
/// <paramref name="Capacity"/>.
/// </summary>
public sealed record CreateRoomTypeRequest(
    string HotelUnitCode,
    string Code,
    string Label,
    int Capacity,
    string? Description = null,
    IReadOnlyCollection<BedCompositionLine>? Beds = null,
    int MaxExtraBeds = 0,
    int MaxCots = 0);
