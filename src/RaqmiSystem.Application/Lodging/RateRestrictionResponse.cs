namespace RaqmiSystem.Application.Lodging;

/// <summary>Une regle de vente. Une portee nulle signifie TOUS (types, plans ou canaux).</summary>
public sealed record RateRestrictionResponse(
    Guid Id,
    string HotelUnitCode,
    string? RoomTypeCode,
    string? RatePlanCode,
    string? ChannelCode,
    DateOnly FromDate,
    DateOnly ToDate,
    bool IsClosed,
    bool IsClosedToArrival,
    bool IsClosedToDeparture,
    int MinimumStay,
    int MaximumStay,
    int MinAdvanceDays,
    int MaxAdvanceDays,
    bool IsActive,
    string? Notes,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
