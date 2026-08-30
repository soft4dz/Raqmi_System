using RaqmiSystem.Domain.Budgeting;

namespace RaqmiSystem.Application.Budgeting;

public sealed record BudgetLineRequest(
    int Month,
    BudgetCategory Category,
    decimal AmountTarget);
