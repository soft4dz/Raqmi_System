using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Tariffs;

/// <summary>
/// Management side of the tariffs module: rate plans, their rate periods and customer
/// conventions. Nightly-rate resolution lives in <see cref="ITariffResolutionService"/>.
/// </summary>
public interface ITariffService
{
    Task<IReadOnlyCollection<RatePlanResponse>> ListPlansAsync(
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RatePlanResponse>> GetPlanAsync(
        string code,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RatePlanResponse>> CreatePlanAsync(
        CreateRatePlanRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RatePlanResponse>> UpdatePlanAsync(
        string code,
        UpdateRatePlanRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RatePlanResponse>> SetPlanDefaultAsync(
        string code,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RatePlanResponse>> SetPlanActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<IReadOnlyCollection<RatePeriodResponse>>> ListPeriodsAsync(
        string planCode,
        string? roomTypeCode,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RatePeriodResponse>> AddPeriodAsync(
        string planCode,
        CreateRatePeriodRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RatePeriodResponse>> UpdatePeriodAsync(
        string planCode,
        Guid periodId,
        UpdateRatePeriodRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RatePeriodResponse>> DeletePeriodAsync(
        string planCode,
        Guid periodId,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CustomerConventionResponse>> ListConventionsAsync(
        string? customerCode,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CustomerConventionResponse>> GetConventionAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CustomerConventionResponse>> CreateConventionAsync(
        CreateCustomerConventionRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CustomerConventionResponse>> UpdateConventionAsync(
        Guid id,
        UpdateCustomerConventionRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CustomerConventionResponse>> SetConventionActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);
}
