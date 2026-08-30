using RaqmiSystem.Domain.Budgeting;

namespace RaqmiSystem.Application.Budgeting;

public sealed record BudgetPlanResponse(
    Guid Id,
    int Year,
    string HotelUnitCode,
    string Label,
    BudgetStatus Status,
    decimal TotalTarget,
    IReadOnlyCollection<BudgetLineResponse> Lines,
    bool CanEdit,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    DateTimeOffset? ClosedAt,
    string? ClosedBy,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
