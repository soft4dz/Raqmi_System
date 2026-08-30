namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// One reservation the front desk expects (or should have already seen) arrive: who, which
/// room, how long, how many guests and at what price. <see cref="TotalStayAmount"/> is the sum
/// of the per-night frozen rates - what the folio will bill at check-in.
/// </summary>
public sealed record FrontDeskArrivalResponse(
    Guid ReservationId,
    string CustomerCode,
    string? CustomerName,
    Guid RoomId,
    string? RoomNumber,
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    int Nights,
    int GuestCount,
    decimal NightlyRateSnapshot,
    string RatePlanCodeSnapshot,
    decimal TotalStayAmount);
