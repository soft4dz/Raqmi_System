namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// La disponibilite d'un type sur toute la periode demandee.
///
/// <paramref name="PublicAvailable"/> est le MINIMUM des nuits, pas la moyenne : un sejour a besoin
/// de la chambre chaque nuit, et une seule nuit a zero suffit a le rendre impossible.
/// <paramref name="RequiresOverbooking"/> dit qu'il ne reste plus de chambre physique et que seule
/// la surreservation autorisee permettrait de vendre - c'est la que le commercial doit s'arreter et
/// decider.
/// </summary>
public sealed record RoomTypeAvailabilityResponse(
    string RoomTypeCode,
    string RoomTypeLabel,
    int Capacity,
    int MaxOccupancy,
    int Rank,
    int PublicAvailable,
    int CommercialAvailable,
    int SellableCapacity,
    bool RequiresOverbooking,
    bool HasRate,
    string? RateIssue,
    string? RatePlanCode,
    decimal? TotalStayAmount,
    IReadOnlyCollection<AvailableNightRateResponse> NightlyRates,
    IReadOnlyCollection<NightInventoryResponse> Nights,
    IReadOnlyCollection<string> RestrictionMessages);
