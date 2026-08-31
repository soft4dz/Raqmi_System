using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// One reservation overlapping the period, with its status: the calculator applies the lodging
/// module's occupancy rule (every stay that is neither Cancelled nor NoShow keeps its room
/// busy - Reservation.IsBlocking, including CheckedOut, so history never reads as vacant)
/// itself, in pure code. The RoomId allows per-night distinct-room counting, exactly like
/// LodgingService.GetOccupancyAsync.
/// </summary>
public sealed record GroupStayFact(
    string HotelUnitCode,
    Guid RoomId,
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    ReservationStatus Status);
