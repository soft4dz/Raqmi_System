using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// One reservation overlapping the period, with its status: the calculator applies the lodging
/// module's occupancy rule (every stay that is neither Cancelled nor NoShow keeps its room
/// busy - Reservation.IsBlocking, including CheckedOut, so history never reads as vacant)
/// itself, in pure code. The RoomId allows per-night distinct-room counting, exactly like
/// LodgingService.GetOccupancyAsync.
///
/// RoomId est NULLABLE depuis que le PMS vend par type : un sejour sans chambre affectee consomme
/// bien l'inventaire, il n'a simplement pas encore de numero. Le comptage distinct ne peut donc pas
/// s'appuyer sur lui seul - voir GroupDashboardCalculator.
/// </summary>
public sealed record GroupStayFact(
    string HotelUnitCode,
    Guid? RoomId,
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    ReservationStatus Status);
