using RaqmiSystem.Domain.Budgeting;

namespace RaqmiSystem.Application.Budgeting;

public sealed record BudgetLineResponse(
    Guid Id,
    int Month,
    BudgetCategory Category,
    decimal AmountTarget);
