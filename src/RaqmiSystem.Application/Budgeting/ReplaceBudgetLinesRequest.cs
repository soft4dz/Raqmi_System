namespace RaqmiSystem.Application.Budgeting;

public sealed record ReplaceBudgetLinesRequest(IReadOnlyCollection<BudgetLineRequest> Lines);
