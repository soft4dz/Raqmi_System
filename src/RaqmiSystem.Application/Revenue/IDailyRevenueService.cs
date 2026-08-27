using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Revenue;

public interface IDailyRevenueService
{
    Task<IReadOnlyCollection<DailyRevenueResponse>> ListAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        DailyRevenueStatus? status,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DailyRevenueResponse>> GetAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DailyRevenueResponse>> CreateAsync(
        CreateDailyRevenueRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DailyRevenueResponse>> UpdateAsync(
        Guid id,
        UpdateDailyRevenueRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DailyRevenueResponse>> SubmitAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DailyRevenueResponse>> ValidateAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DailyRevenueResponse>> RejectAsync(
        Guid id,
        RejectDailyRevenueRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DailyRevenueSummaryResponse>> GetSummaryAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        DailyRevenueStatus? status,
        CancellationToken cancellationToken);
}
