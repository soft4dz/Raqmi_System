using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Budgeting;

namespace RaqmiSystem.Application.Budgeting;

public interface IBudgetService
{
    Task<IReadOnlyCollection<BudgetPlanResponse>> ListPlansAsync(
        int? year,
        string? hotelUnitCode,
        BudgetStatus? status,
        CancellationToken cancellationToken);

    Task<ApplicationResult<BudgetPlanResponse>> GetPlanAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<BudgetPlanResponse>> CreatePlanAsync(
        CreateBudgetPlanRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<BudgetPlanResponse>> UpdatePlanAsync(
        Guid id,
        UpdateBudgetPlanRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<BudgetPlanResponse>> ReplacePlanLinesAsync(
        Guid id,
        ReplaceBudgetLinesRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates or adjusts the single target of the plan for one (month, category) cell.
    /// </summary>
    Task<ApplicationResult<BudgetPlanResponse>> SetPlanLineAsync(
        Guid id,
        BudgetLineRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<BudgetPlanResponse>> RemovePlanLineAsync(
        Guid id,
        Guid lineId,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<BudgetPlanResponse>> ApprovePlanAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<BudgetPlanResponse>> ClosePlanAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// The heart of the module: compares the budget of <paramref name="year"/> for
    /// <paramref name="hotelUnitCode"/> against the revenue actually produced over the same
    /// period, month by month and category by category. Only Validated daily revenue counts as
    /// actual - see <see cref="BudgetVarianceCalculator"/> for the rule and its rationale.
    /// Pass <paramref name="month"/> to narrow the report to a single month.
    /// </summary>
    Task<ApplicationResult<BudgetVarianceResponse>> GetVarianceAsync(
        int year,
        string hotelUnitCode,
        int? month,
        CancellationToken cancellationToken);
}
