namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Pose ou modifie une regle de vente. Les bornes de dates sont INCLUSIVES des deux cotes : une
/// regle du 1er au 15 aout couvre la nuit du 15.
/// </summary>
public sealed record SaveRateRestrictionRequest(
    string HotelUnitCode,
    DateOnly FromDate,
    DateOnly ToDate,
    bool IsClosed = false,
    bool IsClosedToArrival = false,
    bool IsClosedToDeparture = false,
    int MinimumStay = 0,
    int MaximumStay = 0,
    int MinAdvanceDays = 0,
    int MaxAdvanceDays = 0,
    string? RoomTypeCode = null,
    string? RatePlanCode = null,
    string? ChannelCode = null,
    string? Notes = null);
