namespace RaqmiSystem.Application.Lodging;

public sealed record InHouseGuestResponse(
    Guid ReservationId,
    string Number,
    Guid? RoomId,
    string? RoomNumber,
    string RoomTypeCode,
    string CustomerCode,
    string? CustomerName,
    string? GuestName,
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    int Nights,
    int Adults,
    int Children,
    int Infants,
    decimal Balance,
    string? SpecialRequests,
    string? Notes,
    bool IsVip,
    string? LoyaltyTier);
