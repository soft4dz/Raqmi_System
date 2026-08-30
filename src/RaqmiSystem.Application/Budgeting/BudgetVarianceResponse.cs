using RaqmiSystem.Domain.Budgeting;

namespace RaqmiSystem.Application.Budgeting;

/// <summary>
/// The budget-versus-actual report for one hotel unit and one exercise, broken down by month and
/// by revenue category, with the totals of the whole requested period.
///
/// <paramref name="PlanStatus"/> is carried deliberately: a variance computed against a plan that
/// is still a Draft is a working figure, not a commitment, and the reader has to be able to tell
/// the difference.
/// </summary>
public sealed record BudgetVarianceResponse(
    int Year,
    string HotelUnitCode,
    int? Month,
    Guid BudgetPlanId,
    BudgetStatus PlanStatus,
    IReadOnlyCollection<BudgetVarianceMonth> Months,
    decimal BudgetAmount,
    decimal ActualAmount,
    decimal VarianceAmount,
    decimal? VariancePercentage);
