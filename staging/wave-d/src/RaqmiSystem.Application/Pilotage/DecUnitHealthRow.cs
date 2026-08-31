using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// One line of the "health of the day" table: yesterday's revenue (the validated figure, or
/// failing that the submitted one FLAGGED AS PROVISIONAL - a draft or rejected entry is not a
/// usable figure and yields a null total), whether yesterday's business day is closed, and
/// today's occupancy. <see cref="NeedsAttention"/> is true when the unit has neither a usable
/// revenue figure for yesterday nor a closed yesterday - the combination the DEC must not miss.
///
/// Occupancy follows the recently hardened lodging rule: every non-cancelled / non-no-show
/// reservation (Booked, CheckedIn, CheckedOut) covering tonight blocks its room. Only active
/// rooms enter the denominator (and the numerator, so the rate stays consistent); the rate is
/// null when the unit has no active room.
/// </summary>
public sealed record DecUnitHealthRow(
    string HotelUnitCode,
    string HotelUnitName,
    DailyRevenueStatus? YesterdayRevenueStatus,
    decimal? YesterdayRevenueTotal,
    bool YesterdayRevenueIsProvisional,
    bool YesterdayClosed,
    int OccupiedRooms,
    int ActiveRooms,
    decimal? OccupancyRatePercent,
    bool NeedsAttention);
