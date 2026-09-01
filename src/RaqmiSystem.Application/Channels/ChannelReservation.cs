namespace RaqmiSystem.Application.Channels;

/// <summary>
/// Une reservation telle que le canal la rapporte, dans une forme volontairement PAUVRE : le PMS la
/// rejoue par son propre chemin de creation. Elle ne porte donc ni identifiant de chambre, ni prix
/// impose - le canal ne decide pas de l'inventaire de l'hotel.
/// </summary>
public sealed record ChannelReservation(
    string ProviderCode,
    string ExternalReservationId,
    ChannelReservationAction Action,
    string HotelUnitCode,
    string RoomTypeCode,
    string? RatePlanCode,
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    int Adults,
    int Children,
    int Infants,
    string GuestName,
    string? GuestEmail,
    decimal TotalAmount,
    string CurrencyCode,
    DateTimeOffset ReceivedAt,
    string? Notes);
