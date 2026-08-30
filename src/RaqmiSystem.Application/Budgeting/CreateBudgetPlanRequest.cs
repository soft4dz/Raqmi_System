namespace RaqmiSystem.Application.Budgeting;

public sealed record CreateBudgetPlanRequest(
    int Year,
    string HotelUnitCode,
    string Label,
    IReadOnlyCollection<BudgetLineRequest>? Lines = null);
