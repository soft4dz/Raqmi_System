using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

public sealed record FolioChargeResponse(
    Guid Id,
    int LineNumber,
    DateOnly ChargeDate,
    string Label,
    decimal Amount,
    ChargeKind Kind,
    string? Reference);
