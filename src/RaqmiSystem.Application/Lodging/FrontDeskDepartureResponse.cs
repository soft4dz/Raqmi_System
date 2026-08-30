namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// One in-house reservation due (or overdue) to leave, WITH its folio balance: the receptionist
/// must see who still has to pay before letting the guest go - check-out is refused while the
/// balance is not zero. <see cref="FolioBalance"/> is null only on a stay whose folio is
/// missing (abnormal for a checked-in reservation, but never hidden as a fake zero).
/// </summary>
public sealed record FrontDeskDepartureResponse(
    Guid ReservationId,
    string CustomerCode,
    string? CustomerName,
    Guid RoomId,
    string? RoomNumber,
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    int Nights,
    int GuestCount,
    decimal? FolioBalance);
