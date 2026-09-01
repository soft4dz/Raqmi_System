namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Une chambre physique libre sur la periode cherchee.
///
/// <para>
/// <see cref="HasRate"/> separe deux situations tres differentes que l'operateur doit toutes deux
/// voir : une chambre vendable avec son prix nuit par nuit, et une chambre libre que le module
/// Tarifs ne sait pas chiffrer (un trou de couverture tarifaire). La seconde reste VISIBLE -
/// la cacher deguiserait une erreur de parametrage en occupation complete - mais porte
/// <see cref="RateIssue"/> (le message du resolveur, nommant la premiere nuit sans prix) au lieu
/// d'un total, et ne peut pas etre vendue en l'etat.
/// </para>
/// </summary>
public sealed record AvailableRoomResponse(
    Guid RoomId,
    string RoomNumber,
    string RoomTypeCode,
    string RoomTypeLabel,
    int Capacity,
    bool HasRate,
    string? RateIssue,
    string? RatePlanCode,
    string? ConventionCustomerCode,
    decimal? DiscountPercent,
    decimal? TotalStayAmount,
    IReadOnlyCollection<AvailableNightRateResponse> NightlyRates,
    string? Floor = null,
    string? Building = null,
    string? View = null,
    bool IsAccessible = false,
    bool IsSmoking = false,
    string? HousekeepingStatus = null);
