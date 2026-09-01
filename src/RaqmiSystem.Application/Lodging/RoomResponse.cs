namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Une chambre et son couchage EFFECTIF.
///
/// <paramref name="Beds"/> est deja resolu : la composition propre a la chambre quand elle en a
/// une, celle de son type sinon. <paramref name="OverridesBeds"/> dit laquelle des deux, pour que
/// l'ecran puisse signaler l'exception sans avoir a comparer lui-meme.
/// Meme logique pour les couchages d'appoint : les valeurs renvoyees sont celles qui s'appliquent.
/// </summary>
public sealed record RoomResponse(
    Guid Id,
    string HotelUnitCode,
    string Number,
    string RoomTypeCode,
    string? Floor,
    string? Notes,
    bool IsActive,
    IReadOnlyCollection<BedCompositionLine> Beds,
    bool OverridesBeds,
    int MaxExtraBeds,
    int MaxCots,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    string? Building = null,
    string? Wing = null,
    string? InternalCode = null,
    string? View = null,
    IReadOnlyCollection<string>? Amenities = null,
    bool IsAccessible = false,
    bool IsSmoking = false,
    int DisplayOrder = 0,
    string? RoomTypeLabel = null,
    int Capacity = 0);
