using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

public sealed record FolioChargeResponse(
    Guid Id,
    int LineNumber,
    DateOnly ChargeDate,
    string Label,
    decimal Amount,
    ChargeKind Kind,
    string? Reference,
    decimal Quantity = 1m,
    decimal? VatRate = null,
    decimal AmountExclVat = 0m,
    decimal VatAmount = 0m,
    string? ExtraCode = null,
    string? SourceReference = null,
    DateOnly? BusinessDate = null);
