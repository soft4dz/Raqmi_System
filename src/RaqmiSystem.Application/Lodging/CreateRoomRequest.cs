namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Creation d'une chambre. <paramref name="Beds"/> n'est a renseigner que si la chambre S'ECARTE de
/// son type ; laisse vide, elle suit la composition du type, ce qui est le cas courant.
/// <paramref name="MaxExtraBeds"/> et <paramref name="MaxCots"/> valent null pour suivre le type.
/// </summary>
public sealed record CreateRoomRequest(
    string HotelUnitCode,
    string Number,
    string RoomTypeCode,
    string? Floor = null,
    string? Notes = null,
    IReadOnlyCollection<BedCompositionLine>? Beds = null,
    int? MaxExtraBeds = null,
    int? MaxCots = null);
