namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// The frozen price of ONE night of a reservation: the night itself, the money amount charged
/// for it and the rate plan that priced it. A stay crossing two rate periods carries one entry
/// per night, so the folio generated at check-in bills exactly what the availability search
/// announced - night by night, not a flat rate silently applied to every night.
/// </summary>
public sealed record ReservationNightRate(DateOnly Night, decimal Amount, string RatePlanCode);
