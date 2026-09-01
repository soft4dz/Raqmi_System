namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Une recherche de disponibilite complete : dates, composition des occupants, type, tarif,
/// segment, canal, convention, et autorisation eventuelle de surreservation.
///
/// <paramref name="Rooms"/> demande N chambres du meme type : le moteur ne propose alors que les
/// types capables d'en fournir N sur TOUTE la periode.
/// </summary>
public sealed record AvailabilitySearchRequest(
    string HotelUnitCode,
    DateOnly From,
    DateOnly To,
    int Adults = 1,
    int Children = 0,
    int Infants = 0,
    int Rooms = 1,
    string? RoomTypeCode = null,
    string? RatePlanCode = null,
    string? CustomerCode = null,
    string? MarketSegmentCode = null,
    string? ChannelCode = null,
    bool AllowOverbooking = false,
    bool IncludePhysicalRooms = true);
