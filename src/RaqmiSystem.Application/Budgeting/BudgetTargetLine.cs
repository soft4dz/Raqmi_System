using RaqmiSystem.Domain.Budgeting;

namespace RaqmiSystem.Application.Budgeting;

/// <summary>
/// One budgeted target, flattened out of a <see cref="BudgetPlan"/> so
/// <see cref="BudgetVarianceCalculator"/> can be exercised without a database or an entity graph.
/// </summary>
public sealed record BudgetTargetLine(
    int Month,
    BudgetCategory Category,
    decimal AmountTarget);
