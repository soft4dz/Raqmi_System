namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// The resolved price of one night in an availability search. Rates are resolved night by
/// night, so a stay crossing two rate periods shows each period's price - and the stay total is
/// the sum of these entries, exactly what the reservation freezes and the folio bills.
/// </summary>
public sealed record AvailableNightRateResponse(
    DateOnly Night,
    decimal Amount,
    string RatePlanCode);
