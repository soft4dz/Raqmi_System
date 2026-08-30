namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// One room free over the searched period.
///
/// <para>
/// <see cref="HasRate"/> separates two very different situations the operator must both see:
/// a bookable room with its full per-night pricing, and a free room the tariff module cannot
/// price (a rate-coverage hole). The second stays VISIBLE - hiding it would disguise a
/// tariff-setup mistake as full occupancy - but carries <see cref="RateIssue"/> (the resolver's
/// own message, naming the first unpriced night) instead of a total, and cannot be booked as-is.
/// </para>
///
/// <para>
/// <see cref="RatePlanCode"/>, <see cref="ConventionCustomerCode"/> and
/// <see cref="DiscountPercent"/> describe the ARRIVAL night's resolution (the same figures a
/// reservation freezes as its flat snapshot); <see cref="NightlyRates"/> carries the plan night
/// by night when it varies. On a room without a rate, the nights resolved before the hole are
/// still listed so the operator sees exactly where coverage stops.
/// </para>
/// </summary>
public sealed record AvailableRoomResponse(
    Guid RoomId,
    string RoomNumber,
    string RoomTypeCode,
    string RoomTypeLabel,
    int Capacity,
    bool HasRate,
    string? RateIssue,
    string? RatePlanCode,
    string? ConventionCustomerCode,
    decimal? DiscountPercent,
    decimal? TotalStayAmount,
    IReadOnlyCollection<AvailableNightRateResponse> NightlyRates);
