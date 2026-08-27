using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Application.Organization;

public sealed record UpdateHotelUnitRequest(
    string Name,
    HotelUnitType UnitType,
    int DisplayOrder = 0);
