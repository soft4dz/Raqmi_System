using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Revenue;

/// <summary>
/// Combines a hotel unit roster with the daily revenue entries recorded for a single business
/// date. Pure in-memory combination (no database access) so the counting logic can be unit
/// tested independently of the EF-backed service - the caller is responsible for fetching the
/// relevant units and the revenue rows already filtered to <paramref name="businessDate"/>.
/// The roster passed in should normally be the currently-active units, widened with any unit
/// referenced by <paramref name="revenuesForDate"/> that is no longer active - otherwise a
/// revenue entry recorded before its unit was deactivated would be silently dropped from the
/// totals below.
/// </summary>
public sealed class UnitDashboardCalculator
{
    public UnitDashboardResponse Build(
        DateOnly businessDate,
        IReadOnlyCollection<HotelUnit> units,
        IReadOnlyCollection<DailyRevenue> revenuesForDate)
    {
        var revenueByUnitCode = revenuesForDate.ToDictionary(revenue => revenue.HotelUnitCode);

        var rows = units
            .OrderBy(unit => unit.DisplayOrder)
            .ThenBy(unit => unit.Name)
            .Select(unit => revenueByUnitCode.TryGetValue(unit.Code, out var revenue)
                ? new UnitDashboardRow(
                    unit.Code,
                    unit.Name,
                    HasEntry: true,
                    revenue.Status,
                    revenue.Total,
                    revenue.SubmittedAt,
                    revenue.ValidatedAt)
                : new UnitDashboardRow(
                    unit.Code,
                    unit.Name,
                    HasEntry: false,
                    Status: null,
                    Total: null,
                    SubmittedAt: null,
                    ValidatedAt: null))
            .ToArray();

        return new UnitDashboardResponse(
            businessDate,
            rows,
            rows.Length,
            rows.Count(row => row.HasEntry),
            rows.Count(row => !row.HasEntry),
            rows.Count(row => row.Status == DailyRevenueStatus.Submitted),
            rows.Sum(row => row.Total ?? 0m));
    }
}
