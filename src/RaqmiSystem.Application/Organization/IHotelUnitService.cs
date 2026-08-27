using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Organization;

public interface IHotelUnitService
{
    Task<IReadOnlyCollection<HotelUnitResponse>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<HotelUnitResponse>> GetAsync(
        string code,
        CancellationToken cancellationToken);

    Task<ApplicationResult<HotelUnitResponse>> CreateAsync(
        CreateHotelUnitRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<HotelUnitResponse>> UpdateAsync(
        string code,
        UpdateHotelUnitRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<HotelUnitResponse>> SetActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);
}
