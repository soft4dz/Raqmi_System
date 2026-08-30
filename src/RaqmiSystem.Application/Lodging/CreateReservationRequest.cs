namespace RaqmiSystem.Application.Lodging;

public sealed record CreateReservationRequest(
    string HotelUnitCode,
    Guid RoomId,
    string CustomerCode,
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    int GuestCount);
