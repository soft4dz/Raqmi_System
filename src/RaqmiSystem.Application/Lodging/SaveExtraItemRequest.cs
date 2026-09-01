using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

public sealed record SaveExtraItemRequest(
    string HotelUnitCode,
    string Code,
    string Label,
    ExtraPricingBasis PricingBasis,
    decimal UnitPrice,
    decimal VatRate,
    ChargeKind ChargeKind = ChargeKind.Extra,
    string? Description = null,
    bool IsPostedByNightAudit = false,
    int DisplayOrder = 0);
