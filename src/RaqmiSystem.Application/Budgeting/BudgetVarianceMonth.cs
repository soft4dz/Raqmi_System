namespace RaqmiSystem.Application.Budgeting;

/// <summary>
/// The category rows of one month (one per category of the report, in display order), plus
/// that month's totals. Same sign and same zero-target rule as <see cref="BudgetVarianceRow"/>.
/// </summary>
public sealed record BudgetVarianceMonth(
    int Month,
    IReadOnlyCollection<BudgetVarianceRow> Categories,
    decimal BudgetAmount,
    decimal ActualAmount,
    decimal VarianceAmount,
    decimal? VariancePercentage);
