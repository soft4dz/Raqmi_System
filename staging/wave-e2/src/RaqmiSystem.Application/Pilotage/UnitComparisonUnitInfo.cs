namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// One unit of the roster handed to <see cref="UnitComparisonCalculator"/>: identity, current
/// activity flag and the number of currently-active rooms (the denominator of its occupancy
/// rate). The roster should be the currently-active units, widened with any inactive unit still
/// referenced by the period's facts - the same widening as GroupDashboardService and
/// DailyRevenueService.GetUnitDashboardAsync, for the same reason: a validated revenue recorded
/// before its unit was deactivated must never silently vanish from a comparison the direction
/// reads.
/// </summary>
public sealed record UnitComparisonUnitInfo(
    string Code,
    string Name,
    bool IsActive,
    int ActiveRoomCount);
