using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// One daily revenue row of a period, with its status: the calculator - not the caller - is
/// what applies the "only Validated is actual" rule, so the rule lives in pure, unit-testable
/// code (the EF-backed service may pre-filter in SQL as an optimization, never as the rule's
/// home). <see cref="Amount"/> is the row's grand total (accommodation + food + beverage +
/// other).
/// </summary>
public sealed record UnitComparisonRevenueFact(
    string HotelUnitCode,
    DateOnly BusinessDate,
    decimal Amount,
    DailyRevenueStatus Status);
