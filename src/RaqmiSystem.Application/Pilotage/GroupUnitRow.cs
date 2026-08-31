namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// One unit of the group table, ranked by validated revenue (descending). Percentages follow
/// the dashboard's single division-by-zero rule (null, displayed as a dash - see
/// <see cref="GroupKpiVariations"/>): <see cref="GroupSharePercent"/> is null when the group
/// produced nothing, <see cref="OccupancyRatePercent"/> when the unit has no active room.
///
/// The budget columns are null when NO approved (or closed - a closed plan was approved and
/// stays a frozen reference) budget plan exists for the unit on the years the period touches:
/// absence of a budget is reported as such, never as a target of zero, for the same reason
/// BudgetService.GetVarianceAsync answers NotFound rather than a zeroed grid.
/// </summary>
public sealed record GroupUnitRow(
    string HotelUnitCode,
    string HotelUnitName,
    bool IsActive,
    decimal ValidatedRevenue,
    decimal? GroupSharePercent,
    decimal ConfirmedReceipts,
    int OccupiedNights,
    int AvailableNights,
    decimal? OccupancyRatePercent,
    int UnclosedDayCount,
    decimal? BudgetTarget,
    decimal? BudgetVarianceAmount,
    decimal? BudgetVariancePercent);
