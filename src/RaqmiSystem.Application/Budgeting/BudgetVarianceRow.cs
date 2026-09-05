namespace RaqmiSystem.Application.Budgeting;

/// <summary>
/// Budget versus actual for one month and one revenue category (<paramref name="Category"/> is
/// the category code, <paramref name="CategoryLabel"/> its display label).
/// <paramref name="VarianceAmount"/> is <c>Actual - Budget</c>: these are revenue categories, so a
/// positive variance means the unit did better than its target and a negative one means it fell
/// short. <paramref name="VariancePercentage"/> is that gap relative to the target, in percent,
/// and is <c>null</c> when the target is zero (see <see cref="BudgetVarianceCalculator"/>).
/// </summary>
public sealed record BudgetVarianceRow(
    int Month,
    string Category,
    string CategoryLabel,
    decimal BudgetAmount,
    decimal ActualAmount,
    decimal VarianceAmount,
    decimal? VariancePercentage);
