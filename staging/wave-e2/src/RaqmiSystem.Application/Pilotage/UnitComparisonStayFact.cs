using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// One reservation overlapping the period, with its status: the calculator applies the lodging
/// module's occupancy rule itself, in pure code - a night is occupied when a stay that is
/// neither Cancelled nor NoShow covers it (Reservation.IsBlocking: Booked, CheckedIn and
/// CheckedOut all count, because excluding CheckedOut would retroactively empty history). The
/// RoomId allows per-night distinct-room counting, exactly like
/// LodgingService.GetOccupancyAsync.
/// </summary>
public sealed record UnitComparisonStayFact(
    string HotelUnitCode,
    Guid RoomId,
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    ReservationStatus Status);
