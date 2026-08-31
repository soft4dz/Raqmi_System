namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// One unit of the roster handed to <see cref="GroupDashboardCalculator"/>: identity, current
/// activity flag and the number of currently-active rooms (the denominator of its occupancy).
/// The roster should be the currently-active units, widened with any inactive unit still
/// referenced by the period's facts - the same widening as
/// DailyRevenueService.GetUnitDashboardAsync, and for the same reason: a revenue recorded
/// before its unit was deactivated must never silently vanish from a group total.
/// </summary>
public sealed record GroupUnitInfo(
    string Code,
    string Name,
    bool IsActive,
    int ActiveRoomCount);
