namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Mise a jour d'une chambre. <paramref name="Beds"/> REMPLACE la surcharge de couchage ; une liste
/// vide la supprime et fait retomber la chambre sur son type.
/// </summary>
public sealed record UpdateRoomRequest(
    string RoomTypeCode,
    string? Floor = null,
    string? Notes = null,
    IReadOnlyCollection<BedCompositionLine>? Beds = null,
    int? MaxExtraBeds = null,
    int? MaxCots = null,
    string? Building = null,
    string? Wing = null,
    string? InternalCode = null,
    string? View = null,
    IReadOnlyCollection<string>? Amenities = null,
    bool IsAccessible = false,
    bool IsSmoking = false,
    int DisplayOrder = 0);
