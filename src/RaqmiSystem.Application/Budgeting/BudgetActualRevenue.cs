namespace RaqmiSystem.Application.Budgeting;

/// <summary>
/// One day of actual revenue feeding the variance report, projected out of
/// <c>exploitation.daily_revenues</c>. The caller is responsible for having already filtered the
/// rows to the hotel unit, the period AND the Validated status - see
/// <see cref="BudgetVarianceCalculator"/> for why only validated revenue counts as actual.
/// </summary>
public sealed record BudgetActualRevenue(
    DateOnly BusinessDate,
    decimal Accommodation,
    decimal Food,
    decimal Beverage,
    decimal Other);
