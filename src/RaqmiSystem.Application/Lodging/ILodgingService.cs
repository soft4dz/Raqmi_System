using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Property management operations of the lodging module: room types and rooms of each hotel
/// unit, the reservation lifecycle (with the anti-double-booking invariant), the folio opened at
/// check-in, and the day-by-day occupancy figures.
/// </summary>
public interface ILodgingService
{
    // Room types ---------------------------------------------------------------------------

    Task<IReadOnlyCollection<RoomTypeResponse>> ListRoomTypesAsync(
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomTypeResponse>> GetRoomTypeAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomTypeResponse>> CreateRoomTypeAsync(
        CreateRoomTypeRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomTypeResponse>> UpdateRoomTypeAsync(
        Guid id,
        UpdateRoomTypeRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomTypeResponse>> SetRoomTypeActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    // Rooms --------------------------------------------------------------------------------

    Task<IReadOnlyCollection<RoomResponse>> ListRoomsAsync(
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomResponse>> GetRoomAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomResponse>> CreateRoomAsync(
        CreateRoomRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomResponse>> UpdateRoomAsync(
        Guid id,
        UpdateRoomRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RoomResponse>> SetRoomActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    // Reservations -------------------------------------------------------------------------

    /// <summary>
    /// Period filters use overlap semantics: a reservation is listed when its stay touches
    /// [from, to] (not only when it starts inside it).
    /// </summary>
    Task<IReadOnlyCollection<ReservationResponse>> ListReservationsAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        ReservationStatus? status,
        string? customerCode,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ReservationResponse>> GetReservationAsync(
        Guid id,
        CancellationToken cancellationToken);

    /// <summary>
    /// The dates-first booking flow: every ACTIVE room of the unit whose type can host
    /// <paramref name="guests"/> and that no blocking reservation overlaps over
    /// [<paramref name="from"/>, <paramref name="to"/>), each priced night by night through the
    /// tariff module (applying <paramref name="customerCode"/>'s convention when one exists).
    /// A free room the tariff module cannot fully price stays listed with HasRate=false and the
    /// resolver's message - a rate-coverage hole must be seen, not disguised as occupancy.
    /// </summary>
    Task<ApplicationResult<AvailabilityResponse>> GetAvailabilityAsync(
        string hotelUnitCode,
        DateOnly from,
        DateOnly to,
        int guests,
        string? customerCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a Booked reservation. The rate of EVERY night is resolved through the tariff
    /// module (applying the customer's convention when one exists) and FROZEN into the
    /// reservation - the arrival night as the flat snapshot, the full detail per night - so the
    /// folio opened at check-in bills exactly what the availability search announced. When any
    /// night's resolution fails the creation fails with the resolver's own message. The
    /// anti-double-booking invariant is enforced atomically here.
    /// </summary>
    Task<ApplicationResult<ReservationResponse>> CreateReservationAsync(
        CreateReservationRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Booked -> CheckedIn (on the arrival day or after). Opens the folio and generates one
    /// Night line per night at that night's frozen rate.
    /// </summary>
    Task<ApplicationResult<ReservationResponse>> CheckInAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// CheckedIn -> CheckedOut, refused while the folio balance is not exactly zero. The normal
    /// path: record the payment in treasury, add a Settlement line referencing that receipt,
    /// then check out.
    /// </summary>
    Task<ApplicationResult<ReservationResponse>> CheckOutAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ReservationResponse>> CancelReservationAsync(
        Guid id,
        CancelReservationRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<ReservationResponse>> MarkNoShowAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    // Folio --------------------------------------------------------------------------------

    Task<ApplicationResult<FolioResponse>> GetFolioAsync(
        Guid reservationId,
        CancellationToken cancellationToken);

    /// <summary>Appends a line to the folio of a CheckedIn reservation.</summary>
    Task<ApplicationResult<FolioResponse>> AddFolioChargeAsync(
        Guid reservationId,
        AddFolioChargeRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    // Occupancy ----------------------------------------------------------------------------

    /// <summary>
    /// Day-by-day occupation of one unit over [from, to] inclusive: active rooms, rooms taken by
    /// a Booked/CheckedIn reservation covering the night, and the resulting percentage.
    /// </summary>
    Task<ApplicationResult<OccupancyResponse>> GetOccupancyAsync(
        string hotelUnitCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    // Front desk ---------------------------------------------------------------------------

    /// <summary>
    /// The counter screen of one unit for one business day: arrivals of the day, departures of
    /// the day with folio balances, overdue arrivals (no-show candidates) and overdue
    /// departures, the in-house count for the night and the day's occupancy.
    /// </summary>
    Task<ApplicationResult<FrontDeskResponse>> GetFrontDeskAsync(
        string hotelUnitCode,
        DateOnly date,
        CancellationToken cancellationToken);
}
