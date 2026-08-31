namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// Year-over-year variation of each group KPI, in percent relative to the previous period's
/// value. Null - deliberately, and it is not an error state - when the previous value is zero:
/// there is then no reference to be relative to, and the consumer displays a dash. This is
/// exactly the division-by-zero behaviour of BudgetVarianceCalculator.Percentage, adopted here
/// so the direction reads one single rule across the whole product.
///
/// There is no variation for the active-unit count: HotelUnit.IsActive only reflects the
/// current state (no activation/deactivation history is persisted - see the roster comment in
/// DailyRevenueService.GetUnitDashboardAsync), so a "one year ago" unit count cannot be
/// computed honestly.
/// </summary>
public sealed record GroupKpiVariations(
    decimal? RevenuePercent,
    decimal? ReceiptsPercent,
    decimal? ReceivablesPercent,
    decimal? OccupancyPercent);
