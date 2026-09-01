namespace RaqmiSystem.Application.Lodging;

public sealed record DepartureRowResponse(
    Guid ReservationId,
    string Number,
    Guid? RoomId,
    string? RoomNumber,
    string CustomerCode,
    string? CustomerName,
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    TimeOnly? EstimatedDepartureTime,
    bool IsLateCheckOut,
    decimal Balance,
    bool IsSettled,
    bool CheckedOut,
    DateTimeOffset? CheckedOutAt);
