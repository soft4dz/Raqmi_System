using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// One line to append to a checked-in reservation's folio. The amount may only be negative for
/// Settlement/Adjustment kinds; a Settlement should carry the treasury receipt number in
/// <paramref name="Reference"/> so the payment can be traced back.
/// </summary>
public sealed record AddFolioChargeRequest(
    DateOnly ChargeDate,
    string Label,
    decimal Amount,
    ChargeKind Kind,
    string? Reference = null);
