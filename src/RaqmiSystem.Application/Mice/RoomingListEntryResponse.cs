namespace RaqmiSystem.Application.Mice;

/// <summary>Une chambre du bloc effectivement attribuee, avec son occupant.</summary>
public sealed record RoomingListEntryResponse(
    Guid ReservationId,
    string RoomNumber,
    string? GuestName,
    int GuestCount,
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    string Status);
