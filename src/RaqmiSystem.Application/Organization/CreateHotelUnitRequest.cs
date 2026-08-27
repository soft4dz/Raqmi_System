using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Application.Organization;

public sealed record CreateHotelUnitRequest(
    string Code,
    string Name,
    HotelUnitType UnitType,
    int DisplayOrder = 0);
