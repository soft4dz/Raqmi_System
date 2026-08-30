namespace RaqmiSystem.Application.Receivables;

/// <summary>
/// Outstanding amounts split by age bracket. All amounts are VAT-inclusive invoice totals
/// expressed in the establishment's currency.
/// </summary>
public sealed record AgingBucketsResponse(
    decimal NotDue,
    decimal Days1To30,
    decimal Days31To60,
    decimal Days61To90,
    decimal Over90,
    decimal Total);
