using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Revenue;

public sealed record RevenueCategoryResponse(
    string Code,
    string Label,
    int DisplayOrder,
    bool IsActive,
    BusinessSector? Sector);
