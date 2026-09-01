using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

public sealed record ExtraItemResponse(
    Guid Id,
    string HotelUnitCode,
    string Code,
    string Label,
    string? Description,
    ExtraPricingBasis PricingBasis,
    decimal UnitPrice,
    decimal VatRate,
    ChargeKind ChargeKind,
    bool IsPostedByNightAudit,
    bool IsActive,
    int DisplayOrder,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
