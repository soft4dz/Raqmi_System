using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

public sealed record ReservationResponse(
    Guid Id,
    string HotelUnitCode,
    Guid RoomId,
    string? RoomNumber,
    string CustomerCode,
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    int Nights,
    int GuestCount,
    ReservationStatus Status,
    decimal NightlyRateSnapshot,
    string RatePlanCodeSnapshot,
    string? CancelReason,
    DateTimeOffset? CheckedInAt,
    string? CheckedInBy,
    DateTimeOffset? CheckedOutAt,
    string? CheckedOutBy,
    DateTimeOffset? CancelledAt,
    string? CancelledBy,
    DateTimeOffset? NoShowAt,
    string? NoShowBy,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
