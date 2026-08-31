using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// One daily revenue row of the period, with its status: the calculator - not the caller - is
/// what applies the "only Validated is actual" rule, so the rule lives in pure, unit-testable
/// code. <see cref="Amount"/> is the row's grand total (accommodation + food + beverage +
/// other). <see cref="SubmittedAt"/> feeds the pending-validation alert.
/// </summary>
public sealed record GroupRevenueFact(
    string HotelUnitCode,
    DateOnly BusinessDate,
    decimal Amount,
    DailyRevenueStatus Status,
    DateTimeOffset? SubmittedAt);
