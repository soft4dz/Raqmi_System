using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Application.Organization;

public sealed record HotelUnitResponse(
    Guid Id,
    string Code,
    string Name,
    HotelUnitType UnitType,
    BusinessSector Sector,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
