using RaqmiSystem.Domain.Budgeting;

namespace RaqmiSystem.Application.Budgeting;

/// <summary>
/// Budget versus actual for one month and one revenue category.
/// <paramref name="VarianceAmount"/> is <c>Actual - Budget</c>: these are revenue categories, so a
/// positive variance means the unit did better than its target and a negative one means it fell
/// short. <paramref name="VariancePercentage"/> is that gap relative to the target, in percent,
/// and is <c>null</c> when the target is zero (see <see cref="BudgetVarianceCalculator"/>).
/// </summary>
public sealed record BudgetVarianceRow(
    int Month,
    BudgetCategory Category,
    decimal BudgetAmount,
    decimal ActualAmount,
    decimal VarianceAmount,
    decimal? VariancePercentage);
