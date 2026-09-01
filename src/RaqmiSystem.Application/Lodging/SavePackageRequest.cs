namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Creation ou mise a jour d'un forfait. La somme des composantes doit egaler
/// <paramref name="TotalPrice"/> : sans cette egalite, le chiffre d'affaires par service serait
/// faux, et c'est precisement ce que la ventilation existe pour eviter.
/// </summary>
public sealed record SavePackageRequest(
    string HotelUnitCode,
    string Code,
    string Label,
    decimal TotalPrice,
    IReadOnlyCollection<PackageComponentResponse> Components,
    string? Description = null,
    string? RatePlanCode = null,
    string? RoomTypeCode = null,
    DateOnly? ValidFrom = null,
    DateOnly? ValidTo = null,
    int Nights = 0);
