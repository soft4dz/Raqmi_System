using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

public sealed record ReservationExtraResponse(
    Guid Id,
    Guid ReservationId,
    string ExtraCode,
    string Label,
    ExtraPricingBasis PricingBasis,
    decimal UnitPrice,
    decimal VatRate,
    ChargeKind ChargeKind,
    decimal Quantity,
    DateOnly? FromDate,
    DateOnly? ToDate,
    bool IsIncludedInRate,
    string? PackageCode,
    string? Notes,
    decimal EstimatedTotal);
