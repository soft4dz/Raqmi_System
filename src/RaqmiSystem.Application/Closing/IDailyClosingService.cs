using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Closing;

public interface IDailyClosingService
{
    Task<IReadOnlyCollection<DailyClosingResponse>> ListAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DailyClosingResponse>> GetAsync(
        DateOnly businessDate,
        string hotelUnitCode,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DailyClosingResponse>> CloseAsync(
        CloseBusinessDayRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DailyClosingResponse>> ReopenAsync(
        Guid id,
        ReopenDailyClosingRequest request,
        OperationContext context,
        CancellationToken cancellationToken);
}
