using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Tariffs;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Persistence;
using System.Data;
using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Lodging;

/// <summary>
/// Property management service (rooms, reservations, folios, occupancy).
///
/// The central invariant - two non-cancelled/non-no-show reservations of the same room never
/// overlap - cannot be expressed as a single-row constraint, so it is enforced with the same
/// atomic-guard pattern as <c>AccountingService</c> / <c>UserAdministrationService</c>: a
/// Serializable transaction, the check re-run INSIDE that transaction, and serialization
/// failures surfaced as retryable 409s instead of 500s. Status transitions that read state a
/// concurrent request could invalidate (check-in creating the folio, check-out asserting a zero
/// balance, folio lines racing a check-out) use the conditional-claim variant of the same
/// pattern.
/// </summary>
public sealed class LodgingService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    ITariffResolutionService tariffResolutionService) : ILodgingService
{
    private const string RoomTypesEntity = "lodging.room_types";

    private const string RoomsEntity = "lodging.rooms";

    private const string ReservationsEntity = "lodging.reservations";

    private const string FoliosEntity = "lodging.folios";

    /// <summary>
    /// Answer given when the atomic claim finds the reservation is no longer in the status the
    /// request loaded it in, or when the database refused to serialize concurrent transactions.
    /// Nothing was modified either way.
    /// </summary>
    private const string ConcurrentReservationMutationRefused =
        "This reservation was just modified by a concurrent operation, so this change was rolled " +
        "back and nothing was modified. Reload the reservation and try again.";

    private const string RoomAlreadyReserved =
        "The room is already reserved over this period by another reservation.";

    /// <summary>
    /// Occupancy is computed day by day in memory; an unbounded window would turn one request
    /// into an arbitrary amount of work.
    /// </summary>
    private const int MaxOccupancyWindowDays = 366;

    /// <summary>
    /// An availability search resolves one tariff per room type and per night; a window longer
    /// than a season is not a booking search anymore, it is a batch job.
    /// </summary>
    private const int MaxAvailabilityWindowNights = 92;

    /// <summary>
    /// THE overlap rule of the anti-double-booking invariant as a database-translatable
    /// expression, in ONE place: a reservation blocks its room over [arrivalDate, departureDate)
    /// when it is neither Cancelled nor NoShow (<see cref="Reservation.IsBlocking"/>) and its
    /// own half-open period overlaps it (<see cref="Reservation.PeriodsOverlap"/>). The creation
    /// guard and the availability search filter through this same expression - they can never
    /// drift apart and disagree on whether a room is free.
    /// </summary>
    private static Expression<Func<Reservation, bool>> BlocksPeriod(
        DateOnly arrivalDate,
        DateOnly departureDate)
    {
        return reservation => reservation.Status != ReservationStatus.Cancelled
            && reservation.Status != ReservationStatus.NoShow
            && reservation.ArrivalDate < departureDate
            && reservation.DepartureDate > arrivalDate;
    }

    // Room types -----------------------------------------------------------------------------

    public async Task<IReadOnlyCollection<RoomTypeResponse>> ListRoomTypesAsync(
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<RoomType>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(roomType => roomType.IsActive);
        }

        var normalizedUnitCode = NormalizeNullableCode(hotelUnitCode);

        if (normalizedUnitCode is not null)
        {
            query = query.Where(roomType => roomType.HotelUnitCode == normalizedUnitCode);
        }

        var roomTypes = await query
            .OrderBy(roomType => roomType.HotelUnitCode)
            .ThenBy(roomType => roomType.Code)
            .ToArrayAsync(cancellationToken);

        return roomTypes.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<RoomTypeResponse>> GetRoomTypeAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var roomType = await dbContext.Set<RoomType>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (roomType is null)
        {
            return ApplicationResult<RoomTypeResponse>.NotFound("Room type was not found.");
        }

        return ApplicationResult<RoomTypeResponse>.Success(Map(roomType));
    }

    public async Task<ApplicationResult<RoomTypeResponse>> CreateRoomTypeAsync(
        CreateRoomTypeRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var unitFailure = await RequireActiveHotelUnitAsync<RoomTypeResponse>(
            request.HotelUnitCode,
            cancellationToken);

        if (unitFailure.Failure is not null)
        {
            return unitFailure.Failure;
        }

        RoomType roomType;

        try
        {
            roomType = new RoomType(unitFailure.UnitCode, request.Code, request.Label, request.Capacity);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RoomTypeResponse>.Validation(ex.Message);
        }

        var exists = await dbContext.Set<RoomType>()
            .AnyAsync(
                current => current.HotelUnitCode == roomType.HotelUnitCode && current.Code == roomType.Code,
                cancellationToken);

        if (exists)
        {
            return ApplicationResult<RoomTypeResponse>.Conflict(
                "A room type with this code already exists in this hotel unit.");
        }

        roomType.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<RoomType>().Add(roomType);

        try
        {
            await WriteAuditAsync(
                "lodging.room_type.created",
                RoomTypesEntity,
                roomType.Id,
                context,
                new { roomType.HotelUnitCode, roomType.Code, roomType.Label, roomType.Capacity },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // The exists-check above and this insert are not atomic: a concurrent create with
            // the same (unit, code) pair loses the race against the alternate key.
            return ApplicationResult<RoomTypeResponse>.Conflict(
                "A room type with this code already exists in this hotel unit.");
        }

        return ApplicationResult<RoomTypeResponse>.Success(Map(roomType));
    }

    public async Task<ApplicationResult<RoomTypeResponse>> UpdateRoomTypeAsync(
        Guid id,
        UpdateRoomTypeRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var roomType = await dbContext.Set<RoomType>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (roomType is null)
        {
            return ApplicationResult<RoomTypeResponse>.NotFound("Room type was not found.");
        }

        try
        {
            roomType.UpdateDetails(request.Label, request.Capacity);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RoomTypeResponse>.Validation(ex.Message);
        }

        roomType.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "lodging.room_type.updated",
            RoomTypesEntity,
            roomType.Id,
            context,
            new { roomType.HotelUnitCode, roomType.Code, roomType.Label, roomType.Capacity },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<RoomTypeResponse>.Success(Map(roomType));
    }

    public async Task<ApplicationResult<RoomTypeResponse>> SetRoomTypeActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var roomType = await dbContext.Set<RoomType>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (roomType is null)
        {
            return ApplicationResult<RoomTypeResponse>.NotFound("Room type was not found.");
        }

        if (isActive)
        {
            roomType.Activate();
        }
        else
        {
            roomType.Deactivate();
        }

        roomType.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "lodging.room_type.activated" : "lodging.room_type.deactivated",
            RoomTypesEntity,
            roomType.Id,
            context,
            new { roomType.HotelUnitCode, roomType.Code, roomType.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<RoomTypeResponse>.Success(Map(roomType));
    }

    // Rooms ----------------------------------------------------------------------------------

    public async Task<IReadOnlyCollection<RoomResponse>> ListRoomsAsync(
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Room>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(room => room.IsActive);
        }

        var normalizedUnitCode = NormalizeNullableCode(hotelUnitCode);

        if (normalizedUnitCode is not null)
        {
            query = query.Where(room => room.HotelUnitCode == normalizedUnitCode);
        }

        var rooms = await query
            .OrderBy(room => room.HotelUnitCode)
            .ThenBy(room => room.Number)
            .ToArrayAsync(cancellationToken);

        return rooms.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<RoomResponse>> GetRoomAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var room = await dbContext.Set<Room>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (room is null)
        {
            return ApplicationResult<RoomResponse>.NotFound("Room was not found.");
        }

        return ApplicationResult<RoomResponse>.Success(Map(room));
    }

    public async Task<ApplicationResult<RoomResponse>> CreateRoomAsync(
        CreateRoomRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var unitFailure = await RequireActiveHotelUnitAsync<RoomResponse>(
            request.HotelUnitCode,
            cancellationToken);

        if (unitFailure.Failure is not null)
        {
            return unitFailure.Failure;
        }

        Room room;

        try
        {
            room = new Room(unitFailure.UnitCode, request.Number, request.RoomTypeCode);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RoomResponse>.Validation(ex.Message);
        }

        var roomTypeFailure = await RequireActiveRoomTypeAsync<RoomResponse>(
            room.HotelUnitCode,
            room.RoomTypeCode,
            cancellationToken);

        if (roomTypeFailure is not null)
        {
            return roomTypeFailure;
        }

        var exists = await dbContext.Set<Room>()
            .AnyAsync(
                current => current.HotelUnitCode == room.HotelUnitCode && current.Number == room.Number,
                cancellationToken);

        if (exists)
        {
            return ApplicationResult<RoomResponse>.Conflict(
                "A room with this number already exists in this hotel unit.");
        }

        room.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<Room>().Add(room);

        try
        {
            await WriteAuditAsync(
                "lodging.room.created",
                RoomsEntity,
                room.Id,
                context,
                new { room.HotelUnitCode, room.Number, room.RoomTypeCode },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<RoomResponse>.Conflict(
                "A room with this number already exists in this hotel unit.");
        }

        return ApplicationResult<RoomResponse>.Success(Map(room));
    }

    public async Task<ApplicationResult<RoomResponse>> UpdateRoomAsync(
        Guid id,
        UpdateRoomRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var room = await dbContext.Set<Room>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (room is null)
        {
            return ApplicationResult<RoomResponse>.NotFound("Room was not found.");
        }

        string normalizedRoomTypeCode;

        try
        {
            normalizedRoomTypeCode = RoomType.NormalizeCode(request.RoomTypeCode);
        }
        catch (ArgumentException ex)
        {
            return ApplicationResult<RoomResponse>.Validation(ex.Message);
        }

        var roomTypeFailure = await RequireActiveRoomTypeAsync<RoomResponse>(
            room.HotelUnitCode,
            normalizedRoomTypeCode,
            cancellationToken);

        if (roomTypeFailure is not null)
        {
            return roomTypeFailure;
        }

        room.AssignRoomType(normalizedRoomTypeCode);
        room.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "lodging.room.updated",
            RoomsEntity,
            room.Id,
            context,
            new { room.HotelUnitCode, room.Number, room.RoomTypeCode },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<RoomResponse>.Success(Map(room));
    }

    public async Task<ApplicationResult<RoomResponse>> SetRoomActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var room = await dbContext.Set<Room>()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (room is null)
        {
            return ApplicationResult<RoomResponse>.NotFound("Room was not found.");
        }

        if (isActive)
        {
            room.Activate();
        }
        else
        {
            room.Deactivate();
        }

        room.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "lodging.room.activated" : "lodging.room.deactivated",
            RoomsEntity,
            room.Id,
            context,
            new { room.HotelUnitCode, room.Number, room.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<RoomResponse>.Success(Map(room));
    }

    // Reservations ---------------------------------------------------------------------------

    public async Task<IReadOnlyCollection<ReservationResponse>> ListReservationsAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        ReservationStatus? status,
        string? customerCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Reservation>().AsNoTracking();

        // Overlap semantics: a stay is listed when it touches the [from, to] window, so an
        // in-house guest who arrived before the window still shows up in it.
        if (from.HasValue)
        {
            query = query.Where(reservation => reservation.DepartureDate > from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(reservation => reservation.ArrivalDate <= to.Value);
        }

        var normalizedUnitCode = NormalizeNullableCode(hotelUnitCode);

        if (normalizedUnitCode is not null)
        {
            query = query.Where(reservation => reservation.HotelUnitCode == normalizedUnitCode);
        }

        if (status.HasValue)
        {
            query = query.Where(reservation => reservation.Status == status.Value);
        }

        var normalizedCustomerCode = NormalizeNullableCode(customerCode);

        if (normalizedCustomerCode is not null)
        {
            query = query.Where(reservation => reservation.CustomerCode == normalizedCustomerCode);
        }

        var reservations = await query
            .OrderBy(reservation => reservation.ArrivalDate)
            .ThenBy(reservation => reservation.HotelUnitCode)
            .ThenBy(reservation => reservation.CustomerCode)
            .ToArrayAsync(cancellationToken);

        var roomNumbers = await LoadRoomNumbersAsync(
            reservations.Select(reservation => reservation.RoomId).Distinct().ToArray(),
            cancellationToken);

        return reservations
            .Select(reservation => Map(reservation, roomNumbers.GetValueOrDefault(reservation.RoomId)))
            .ToArray();
    }

    public async Task<ApplicationResult<ReservationResponse>> GetReservationAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var reservation = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (reservation is null)
        {
            return ApplicationResult<ReservationResponse>.NotFound("Reservation was not found.");
        }

        return ApplicationResult<ReservationResponse>.Success(
            Map(reservation, await LoadRoomNumberAsync(reservation.RoomId, cancellationToken)));
    }

    public async Task<ApplicationResult<ReservationResponse>> CreateReservationAsync(
        CreateReservationRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (request.DepartureDate <= request.ArrivalDate)
        {
            return ApplicationResult<ReservationResponse>.Validation(
                "The departure date must be after the arrival date (a reservation covers at least one night).");
        }

        var unitFailure = await RequireActiveHotelUnitAsync<ReservationResponse>(
            request.HotelUnitCode,
            cancellationToken);

        if (unitFailure.Failure is not null)
        {
            return unitFailure.Failure;
        }

        var room = await dbContext.Set<Room>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == request.RoomId, cancellationToken);

        if (room is null || room.HotelUnitCode != unitFailure.UnitCode)
        {
            return ApplicationResult<ReservationResponse>.NotFound("Room was not found in this hotel unit.");
        }

        if (!room.IsActive)
        {
            return ApplicationResult<ReservationResponse>.Validation(
                "Reservations cannot be taken on an inactive room.");
        }

        var roomType = await dbContext.Set<RoomType>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                current => current.HotelUnitCode == room.HotelUnitCode && current.Code == room.RoomTypeCode,
                cancellationToken);

        if (roomType is null)
        {
            return ApplicationResult<ReservationResponse>.NotFound("The room's type was not found.");
        }

        if (request.GuestCount <= 0)
        {
            return ApplicationResult<ReservationResponse>.Validation("Guest count must be strictly positive.");
        }

        if (request.GuestCount > roomType.Capacity)
        {
            return ApplicationResult<ReservationResponse>.Validation(
                $"Guest count ({request.GuestCount}) exceeds the capacity of room type " +
                $"'{roomType.Code}' ({roomType.Capacity}).");
        }

        var normalizedCustomerCode = NormalizeCodeOrEmpty(request.CustomerCode);

        var customer = await dbContext.Set<Customer>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedCustomerCode, cancellationToken);

        if (customer is null)
        {
            return ApplicationResult<ReservationResponse>.NotFound("Customer was not found.");
        }

        if (!customer.IsActive)
        {
            return ApplicationResult<ReservationResponse>.Validation(
                "Reservations cannot be taken for an inactive customer.");
        }

        // The rate of EVERY night is resolved and frozen into the reservation (same snapshot
        // discipline as the issuer identity of issued invoices): the arrival night doubles as
        // the flat snapshot, and the per-night detail is what the folio bills at check-in - so
        // a stay crossing two rate periods is billed exactly what the availability search
        // announced, not the arrival rate flattened over every night. Any failed resolution
        // fails the creation with the resolver's own message - a booking with an unpriced
        // night is not a booking.
        var nightlyRates = new List<ReservationNightRate>();
        ResolvedNightlyRate? arrivalRate = null;

        for (var night = request.ArrivalDate; night < request.DepartureDate; night = night.AddDays(1))
        {
            var rateResult = await tariffResolutionService.ResolveAsync(
                unitFailure.UnitCode,
                room.RoomTypeCode,
                night,
                normalizedCustomerCode,
                cancellationToken);

            if (!rateResult.Succeeded || rateResult.Value is null)
            {
                return MirrorFailure<ResolvedNightlyRate, ReservationResponse>(
                    rateResult,
                    "The nightly rate could not be resolved.");
            }

            arrivalRate ??= rateResult.Value;
            nightlyRates.Add(new ReservationNightRate(night, rateResult.Value.Amount, rateResult.Value.RatePlanCode));
        }

        // ANTI-DOUBLE-BOOKING GUARD. The overlap check must run INSIDE a Serializable
        // transaction together with the insert: checked outside of one, two concurrent creates
        // both read "the room is free" and both commit. Under PostgreSQL the loser's commit is
        // refused with a serialization failure; under the SQLite test provider the loser's
        // write is turned away with "database is locked". Both are surfaced as a retryable 409.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var overlapping = await dbContext.Set<Reservation>()
                .Where(current => current.RoomId == room.Id)
                .Where(BlocksPeriod(request.ArrivalDate, request.DepartureDate))
                .AnyAsync(cancellationToken);

            if (overlapping)
            {
                return ApplicationResult<ReservationResponse>.Conflict(RoomAlreadyReserved);
            }

            Reservation reservation;

            try
            {
                reservation = new Reservation(
                    unitFailure.UnitCode,
                    room.Id,
                    normalizedCustomerCode,
                    request.ArrivalDate,
                    request.DepartureDate,
                    request.GuestCount,
                    arrivalRate!.Amount,
                    arrivalRate.RatePlanCode);

                reservation.FreezeNightlyRates(nightlyRates);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                return ApplicationResult<ReservationResponse>.Validation(ex.Message);
            }

            reservation.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
            dbContext.Set<Reservation>().Add(reservation);

            await WriteAuditAsync(
                "lodging.reservation.created",
                ReservationsEntity,
                reservation.Id,
                context,
                new
                {
                    reservation.HotelUnitCode,
                    reservation.RoomId,
                    RoomNumber = room.Number,
                    reservation.CustomerCode,
                    reservation.ArrivalDate,
                    reservation.DepartureDate,
                    reservation.GuestCount,
                    reservation.NightlyRateSnapshot,
                    reservation.RatePlanCodeSnapshot,
                    reservation.TotalStayAmount
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<ReservationResponse>.Success(Map(reservation, room.Number));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<ReservationResponse>.Conflict(RoomAlreadyReserved);
        }
    }

    public async Task<ApplicationResult<ReservationResponse>> CheckInAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        // Serializable transaction + conditional claim: check-in both flips the status and
        // creates the folio, so a double-click racing itself must produce exactly one folio.
        // The unique index on folios.reservation_id is the backstop.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var reservation = await dbContext.Set<Reservation>()
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (reservation is null)
            {
                return ApplicationResult<ReservationResponse>.NotFound("Reservation was not found.");
            }

            if (reservation.Status != ReservationStatus.Booked)
            {
                return ApplicationResult<ReservationResponse>.Conflict(
                    "Only a booked reservation can be checked in.");
            }

            var now = DateTimeOffset.UtcNow;

            if (!await TryClaimReservationStatusAsync(reservation.Id, ReservationStatus.Booked, now, cancellationToken))
            {
                return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
            }

            // The business day follows UTC, consistently with every other UtcNow-based decision
            // in this codebase (invoice issue year, closing dates).
            var today = DateOnly.FromDateTime(now.UtcDateTime);

            try
            {
                reservation.CheckIn(today, context.UserName, now);
            }
            catch (InvalidOperationException ex)
            {
                return ApplicationResult<ReservationResponse>.Validation(ex.Message);
            }

            // The folio opens with one Night line per night of the stay, each at THAT night's
            // rate frozen at booking time (never re-resolved here): the guest is billed exactly
            // what the availability search announced, even across a rate-period boundary. A
            // zero-rate night (e.g. a 100% convention discount) produces no line - a folio line
            // cannot carry a zero amount, and a free night has nothing to bill.
            var folio = new Folio(reservation.Id);

            foreach (var nightRate in reservation.GetNightlyRates())
            {
                if (nightRate.Amount == 0)
                {
                    continue;
                }

                folio.AddCharge(new FolioCharge(
                    nightRate.Night,
                    $"Night of {nightRate.Night:yyyy-MM-dd}",
                    nightRate.Amount,
                    ChargeKind.Night));
            }

            folio.MarkCreated(context.UserName, now);
            dbContext.Set<Folio>().Add(folio);

            reservation.MarkUpdated(context.UserName, now);

            await WriteAuditAsync(
                "lodging.reservation.checked_in",
                ReservationsEntity,
                reservation.Id,
                context,
                new
                {
                    reservation.HotelUnitCode,
                    reservation.RoomId,
                    FolioId = folio.Id,
                    NightCount = reservation.Nights,
                    reservation.NightlyRateSnapshot,
                    FolioTotal = folio.Balance
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<ReservationResponse>.Success(
                Map(reservation, await LoadRoomNumberAsync(reservation.RoomId, cancellationToken)));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            // ux_folios_reservation_id: a concurrent check-in already opened this stay's folio.
            return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
        }
    }

    public async Task<ApplicationResult<ReservationResponse>> CheckOutAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        // The zero-balance rule reads the folio, so the read, the check and the status flip must
        // sit in one Serializable transaction: without it, a charge added concurrently between
        // the balance read and the commit would let a guest leave with an unsettled folio.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var reservation = await dbContext.Set<Reservation>()
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (reservation is null)
            {
                return ApplicationResult<ReservationResponse>.NotFound("Reservation was not found.");
            }

            if (reservation.Status != ReservationStatus.CheckedIn)
            {
                return ApplicationResult<ReservationResponse>.Conflict(
                    "Only a checked-in reservation can be checked out.");
            }

            var folio = await dbContext.Set<Folio>()
                .AsNoTracking()
                .Include(current => current.Charges)
                .SingleOrDefaultAsync(current => current.ReservationId == reservation.Id, cancellationToken);

            if (folio is null)
            {
                return ApplicationResult<ReservationResponse>.Validation(
                    "The reservation has no folio; it cannot be checked out.");
            }

            var balance = folio.Balance;

            if (balance != 0)
            {
                return ApplicationResult<ReservationResponse>.Validation(
                    $"Check-out refused: the folio balance is {balance:0.00}, not zero. Record the " +
                    "payment as a treasury receipt, add a Settlement line referencing it, then check out.");
            }

            var now = DateTimeOffset.UtcNow;

            if (!await TryClaimReservationStatusAsync(reservation.Id, ReservationStatus.CheckedIn, now, cancellationToken))
            {
                return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
            }

            try
            {
                reservation.CheckOut(context.UserName, now);
            }
            catch (InvalidOperationException ex)
            {
                return ApplicationResult<ReservationResponse>.Validation(ex.Message);
            }

            reservation.MarkUpdated(context.UserName, now);

            await WriteAuditAsync(
                "lodging.reservation.checked_out",
                ReservationsEntity,
                reservation.Id,
                context,
                new { reservation.HotelUnitCode, reservation.RoomId, FolioBalance = balance },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<ReservationResponse>.Success(
                Map(reservation, await LoadRoomNumberAsync(reservation.RoomId, cancellationToken)));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
        }
    }

    public async Task<ApplicationResult<ReservationResponse>> CancelReservationAsync(
        Guid id,
        CancelReservationRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await ChangeBookedReservationAsync(
            id,
            context,
            "lodging.reservation.cancelled",
            (reservation, now) => reservation.Cancel(request.Reason, context.UserName, now),
            reservation => new { reservation.HotelUnitCode, reservation.RoomId, reservation.CancelReason },
            cancellationToken);
    }

    public async Task<ApplicationResult<ReservationResponse>> MarkNoShowAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await ChangeBookedReservationAsync(
            id,
            context,
            "lodging.reservation.no_show",
            (reservation, now) => reservation.MarkNoShow(
                DateOnly.FromDateTime(now.UtcDateTime),
                context.UserName,
                now),
            reservation => new { reservation.HotelUnitCode, reservation.RoomId, reservation.ArrivalDate },
            cancellationToken);
    }

    // Folio ----------------------------------------------------------------------------------

    public async Task<ApplicationResult<FolioResponse>> GetFolioAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        var reservationExists = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .AnyAsync(current => current.Id == reservationId, cancellationToken);

        if (!reservationExists)
        {
            return ApplicationResult<FolioResponse>.NotFound("Reservation was not found.");
        }

        var folio = await dbContext.Set<Folio>()
            .AsNoTracking()
            .Include(current => current.Charges)
            .SingleOrDefaultAsync(current => current.ReservationId == reservationId, cancellationToken);

        if (folio is null)
        {
            return ApplicationResult<FolioResponse>.NotFound(
                "The reservation has no folio yet: the folio is opened at check-in.");
        }

        return ApplicationResult<FolioResponse>.Success(Map(folio));
    }

    public async Task<ApplicationResult<FolioResponse>> AddFolioChargeAsync(
        Guid reservationId,
        AddFolioChargeRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        // A folio line racing a check-out must lose against the already-closed stay rather than
        // land on a folio whose zero balance was just asserted - hence the same Serializable
        // transaction + conditional claim as the check-out itself.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var reservation = await dbContext.Set<Reservation>()
                .SingleOrDefaultAsync(current => current.Id == reservationId, cancellationToken);

            if (reservation is null)
            {
                return ApplicationResult<FolioResponse>.NotFound("Reservation was not found.");
            }

            if (reservation.Status != ReservationStatus.CheckedIn)
            {
                return ApplicationResult<FolioResponse>.Conflict(
                    "Folio lines can only be added while the reservation is checked in.");
            }

            var folio = await dbContext.Set<Folio>()
                .Include(current => current.Charges)
                .SingleOrDefaultAsync(current => current.ReservationId == reservation.Id, cancellationToken);

            if (folio is null)
            {
                return ApplicationResult<FolioResponse>.Validation(
                    "The reservation has no folio; nothing can be charged.");
            }

            var now = DateTimeOffset.UtcNow;

            if (!await TryClaimReservationStatusAsync(reservation.Id, ReservationStatus.CheckedIn, now, cancellationToken))
            {
                return ApplicationResult<FolioResponse>.Conflict(ConcurrentReservationMutationRefused);
            }

            FolioCharge charge;

            try
            {
                charge = new FolioCharge(
                    request.ChargeDate,
                    request.Label,
                    request.Amount,
                    request.Kind,
                    request.Reference);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                return ApplicationResult<FolioResponse>.Validation(ex.Message);
            }

            folio.AddCharge(charge);
            folio.MarkUpdated(context.UserName, now);

            await WriteAuditAsync(
                "lodging.folio.charge_added",
                FoliosEntity,
                folio.Id,
                context,
                new
                {
                    ReservationId = reservation.Id,
                    charge.ChargeDate,
                    charge.Label,
                    charge.Amount,
                    Kind = charge.Kind.ToString(),
                    charge.Reference,
                    NewBalance = folio.Balance
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<FolioResponse>.Success(Map(folio));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<FolioResponse>.Conflict(ConcurrentReservationMutationRefused);
        }
    }

    // Occupancy ------------------------------------------------------------------------------

    public async Task<ApplicationResult<OccupancyResponse>> GetOccupancyAsync(
        string hotelUnitCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return ApplicationResult<OccupancyResponse>.Validation(
                "The from date cannot be after the to date.");
        }

        if (to.DayNumber - from.DayNumber + 1 > MaxOccupancyWindowDays)
        {
            return ApplicationResult<OccupancyResponse>.Validation(
                $"The occupancy window cannot exceed {MaxOccupancyWindowDays} days.");
        }

        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitExists = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .AnyAsync(current => current.Code == normalizedUnitCode, cancellationToken);

        if (!unitExists)
        {
            return ApplicationResult<OccupancyResponse>.NotFound("Hotel unit was not found.");
        }

        var totalActiveRooms = await dbContext.Set<Room>()
            .AsNoTracking()
            .CountAsync(room => room.HotelUnitCode == normalizedUnitCode && room.IsActive, cancellationToken);

        // A night is occupied when a non-cancelled / non-no-show stay covers it: Booked,
        // CheckedIn AND CheckedOut all count (the entity's IsBlocking definition). Excluding
        // CheckedOut would retroactively empty history - a guest who has left still consumed
        // those nights, and a fully past month must not read as vacant. Filtered in the
        // database, counted day by day in memory.
        var reservations = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.HotelUnitCode == normalizedUnitCode
                && reservation.Status != ReservationStatus.Cancelled
                && reservation.Status != ReservationStatus.NoShow
                && reservation.ArrivalDate <= to
                && reservation.DepartureDate > from)
            .Select(reservation => new { reservation.RoomId, reservation.ArrivalDate, reservation.DepartureDate })
            .ToArrayAsync(cancellationToken);

        var days = new List<OccupancyDayResponse>(to.DayNumber - from.DayNumber + 1);

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var night = day;

            // Distinct rooms, although the anti-double-booking invariant already makes one room
            // impossible to count twice for the same night: the figure must stay right even if
            // data predating the invariant exists.
            var occupiedRooms = reservations
                .Where(reservation => reservation.ArrivalDate <= night && night < reservation.DepartureDate)
                .Select(reservation => reservation.RoomId)
                .Distinct()
                .Count();

            var ratePercent = totalActiveRooms == 0
                ? 0m
                : Math.Round(occupiedRooms * 100m / totalActiveRooms, 2, MidpointRounding.AwayFromZero);

            days.Add(new OccupancyDayResponse(day, totalActiveRooms, occupiedRooms, ratePercent));
        }

        return ApplicationResult<OccupancyResponse>.Success(
            new OccupancyResponse(normalizedUnitCode, from, to, days));
    }

    // Availability ---------------------------------------------------------------------------

    public async Task<ApplicationResult<AvailabilityResponse>> GetAvailabilityAsync(
        string hotelUnitCode,
        DateOnly from,
        DateOnly to,
        int guests,
        string? customerCode,
        CancellationToken cancellationToken)
    {
        if (to <= from)
        {
            return ApplicationResult<AvailabilityResponse>.Validation(
                "The to date must be after the from date (an availability search covers at least one night).");
        }

        var nights = to.DayNumber - from.DayNumber;

        if (nights > MaxAvailabilityWindowNights)
        {
            return ApplicationResult<AvailabilityResponse>.Validation(
                $"The availability window cannot exceed {MaxAvailabilityWindowNights} nights.");
        }

        if (guests <= 0)
        {
            return ApplicationResult<AvailabilityResponse>.Validation("Guest count must be strictly positive.");
        }

        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitExists = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .AnyAsync(current => current.Code == normalizedUnitCode, cancellationToken);

        if (!unitExists)
        {
            return ApplicationResult<AvailabilityResponse>.NotFound("Hotel unit was not found.");
        }

        // Same customer discipline as the creation the search leads to: quoting convention
        // rates for a customer who cannot book would announce prices no reservation can honor.
        var normalizedCustomerCode = NormalizeNullableCode(customerCode);

        if (normalizedCustomerCode is not null)
        {
            var customer = await dbContext.Set<Customer>()
                .AsNoTracking()
                .SingleOrDefaultAsync(current => current.Code == normalizedCustomerCode, cancellationToken);

            if (customer is null)
            {
                return ApplicationResult<AvailabilityResponse>.NotFound("Customer was not found.");
            }

            if (!customer.IsActive)
            {
                return ApplicationResult<AvailabilityResponse>.Validation(
                    "Reservations cannot be taken for an inactive customer.");
            }
        }

        // Candidate rooms: ACTIVE, of a type whose capacity can host the party. Mirrors what
        // the creation checks (active room, capacity cap) - a listed room must be bookable
        // as-is once it prices.
        var candidateRooms = await (
            from room in dbContext.Set<Room>().AsNoTracking()
            where room.HotelUnitCode == normalizedUnitCode && room.IsActive
            join roomType in dbContext.Set<RoomType>().AsNoTracking()
                on new { Unit = room.HotelUnitCode, Code = room.RoomTypeCode }
                equals new { Unit = roomType.HotelUnitCode, Code = roomType.Code }
            where roomType.Capacity >= guests
            orderby room.Number
            select new
            {
                room.Id,
                room.Number,
                RoomTypeCode = roomType.Code,
                RoomTypeLabel = roomType.Label,
                roomType.Capacity
            })
            .ToArrayAsync(cancellationToken);

        // Rooms taken over the period, through the SAME overlap expression as the creation
        // guard: what this search calls free is exactly what a creation would accept (up to the
        // race the creation's Serializable transaction then settles).
        var occupiedRoomIds = (await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.HotelUnitCode == normalizedUnitCode)
            .Where(BlocksPeriod(from, to))
            .Select(reservation => reservation.RoomId)
            .Distinct()
            .ToArrayAsync(cancellationToken))
            .ToHashSet();

        // One resolution per (room type, night), shared by every room of the type: pricing is
        // a function of the type, not of the individual room.
        var rateCache = new Dictionary<(string RoomTypeCode, DateOnly Night), ApplicationResult<ResolvedNightlyRate>>();
        var rooms = new List<AvailableRoomResponse>();

        foreach (var candidate in candidateRooms)
        {
            if (occupiedRoomIds.Contains(candidate.Id))
            {
                continue;
            }

            var nightRates = new List<AvailableNightRateResponse>(nights);
            ResolvedNightlyRate? arrivalRate = null;
            string? rateIssue = null;

            for (var night = from; night < to; night = night.AddDays(1))
            {
                if (!rateCache.TryGetValue((candidate.RoomTypeCode, night), out var rateResult))
                {
                    rateResult = await tariffResolutionService.ResolveAsync(
                        normalizedUnitCode,
                        candidate.RoomTypeCode,
                        night,
                        normalizedCustomerCode,
                        cancellationToken);

                    rateCache[(candidate.RoomTypeCode, night)] = rateResult;
                }

                if (!rateResult.Succeeded || rateResult.Value is null)
                {
                    // A free room the tariff module cannot price stays LISTED, flagged with the
                    // resolver's own message: the operator must see the rate-coverage hole, not
                    // a room silently missing from the search.
                    rateIssue =
                        $"Night of {night.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}: " +
                        (rateResult.Error ?? "the nightly rate could not be resolved.");
                    break;
                }

                arrivalRate ??= rateResult.Value;
                nightRates.Add(new AvailableNightRateResponse(night, rateResult.Value.Amount, rateResult.Value.RatePlanCode));
            }

            var hasRate = rateIssue is null;

            rooms.Add(new AvailableRoomResponse(
                candidate.Id,
                candidate.Number,
                candidate.RoomTypeCode,
                candidate.RoomTypeLabel,
                candidate.Capacity,
                hasRate,
                rateIssue,
                arrivalRate?.RatePlanCode,
                arrivalRate?.ConventionCustomerCode,
                arrivalRate?.DiscountPercent,
                hasRate ? nightRates.Sum(rate => rate.Amount) : null,
                nightRates));
        }

        return ApplicationResult<AvailabilityResponse>.Success(
            new AvailabilityResponse(normalizedUnitCode, from, to, nights, guests, rooms));
    }

    // Front desk -----------------------------------------------------------------------------

    public async Task<ApplicationResult<FrontDeskResponse>> GetFrontDeskAsync(
        string hotelUnitCode,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        var unitExists = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .AnyAsync(current => current.Code == normalizedUnitCode, cancellationToken);

        if (!unitExists)
        {
            return ApplicationResult<FrontDeskResponse>.NotFound("Hotel unit was not found.");
        }

        // Booked stays whose arrival date is reached or past: today's arrivals plus the overdue
        // ones (no-show candidates the receptionist must deal with first).
        var expectedArrivals = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.HotelUnitCode == normalizedUnitCode
                && reservation.Status == ReservationStatus.Booked
                && reservation.ArrivalDate <= date)
            .OrderBy(reservation => reservation.ArrivalDate)
            .ThenBy(reservation => reservation.CustomerCode)
            .ToArrayAsync(cancellationToken);

        // Every in-house stay: today's departures, the overdue ones, and the in-house count.
        var inHouseReservations = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.HotelUnitCode == normalizedUnitCode
                && reservation.Status == ReservationStatus.CheckedIn)
            .OrderBy(reservation => reservation.DepartureDate)
            .ThenBy(reservation => reservation.CustomerCode)
            .ToArrayAsync(cancellationToken);

        var arrivals = expectedArrivals.Where(reservation => reservation.ArrivalDate == date).ToArray();
        var overdueArrivals = expectedArrivals.Where(reservation => reservation.ArrivalDate < date).ToArray();
        var departures = inHouseReservations.Where(reservation => reservation.DepartureDate == date).ToArray();
        var overdueDepartures = inHouseReservations.Where(reservation => reservation.DepartureDate < date).ToArray();
        var inHouseCount = inHouseReservations.Count(reservation => reservation.CoversNight(date));

        // The departure lists carry the folio balance: who still has to pay before leaving.
        var departureIds = departures.Concat(overdueDepartures)
            .Select(reservation => reservation.Id)
            .ToArray();

        var folioBalances = departureIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : (await dbContext.Set<Folio>()
                .AsNoTracking()
                .Include(folio => folio.Charges)
                .Where(folio => departureIds.Contains(folio.ReservationId))
                .ToArrayAsync(cancellationToken))
                .ToDictionary(folio => folio.ReservationId, folio => folio.Balance);

        var allListed = arrivals.Concat(overdueArrivals).Concat(departures).Concat(overdueDepartures).ToArray();

        var roomNumbers = await LoadRoomNumbersAsync(
            allListed.Select(reservation => reservation.RoomId).Distinct().ToArray(),
            cancellationToken);

        var customerCodes = allListed.Select(reservation => reservation.CustomerCode).Distinct().ToArray();

        var customerNames = customerCodes.Length == 0
            ? new Dictionary<string, string>()
            : await dbContext.Set<Customer>()
                .AsNoTracking()
                .Where(customer => customerCodes.Contains(customer.Code))
                .ToDictionaryAsync(customer => customer.Code, customer => customer.Name, cancellationToken);

        // The day's occupancy reuses the exact occupancy logic - one figure, one definition.
        var occupancyResult = await GetOccupancyAsync(normalizedUnitCode, date, date, cancellationToken);

        if (!occupancyResult.Succeeded || occupancyResult.Value is null)
        {
            return MirrorFailure<OccupancyResponse, FrontDeskResponse>(
                occupancyResult,
                "The day's occupancy could not be computed.");
        }

        FrontDeskArrivalResponse MapArrival(Reservation reservation)
        {
            return new FrontDeskArrivalResponse(
                reservation.Id,
                reservation.CustomerCode,
                customerNames.GetValueOrDefault(reservation.CustomerCode),
                reservation.RoomId,
                roomNumbers.GetValueOrDefault(reservation.RoomId),
                reservation.ArrivalDate,
                reservation.DepartureDate,
                reservation.Nights,
                reservation.GuestCount,
                reservation.NightlyRateSnapshot,
                reservation.RatePlanCodeSnapshot,
                reservation.TotalStayAmount);
        }

        FrontDeskDepartureResponse MapDeparture(Reservation reservation)
        {
            return new FrontDeskDepartureResponse(
                reservation.Id,
                reservation.CustomerCode,
                customerNames.GetValueOrDefault(reservation.CustomerCode),
                reservation.RoomId,
                roomNumbers.GetValueOrDefault(reservation.RoomId),
                reservation.ArrivalDate,
                reservation.DepartureDate,
                reservation.Nights,
                reservation.GuestCount,
                folioBalances.TryGetValue(reservation.Id, out var balance) ? balance : null);
        }

        return ApplicationResult<FrontDeskResponse>.Success(new FrontDeskResponse(
            normalizedUnitCode,
            date,
            arrivals.Select(MapArrival).ToArray(),
            overdueArrivals.Select(MapArrival).ToArray(),
            departures.Select(MapDeparture).ToArray(),
            overdueDepartures.Select(MapDeparture).ToArray(),
            inHouseCount,
            occupancyResult.Value.Days.Single()));
    }

    // Shared helpers -------------------------------------------------------------------------

    /// <summary>
    /// Shared Serializable-transaction + claim skeleton of the two Booked-only transitions
    /// (cancel, no-show): the domain transition runs only on a row the database just re-asserted
    /// as Booked, so a concurrent check-in cannot be silently overwritten.
    /// </summary>
    private async Task<ApplicationResult<ReservationResponse>> ChangeBookedReservationAsync(
        Guid id,
        OperationContext context,
        string auditAction,
        Action<Reservation, DateTimeOffset> change,
        Func<Reservation, object> auditDetails,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var reservation = await dbContext.Set<Reservation>()
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (reservation is null)
            {
                return ApplicationResult<ReservationResponse>.NotFound("Reservation was not found.");
            }

            if (reservation.Status != ReservationStatus.Booked)
            {
                return ApplicationResult<ReservationResponse>.Conflict(
                    "This operation is only allowed on a booked reservation.");
            }

            var now = DateTimeOffset.UtcNow;

            if (!await TryClaimReservationStatusAsync(reservation.Id, ReservationStatus.Booked, now, cancellationToken))
            {
                return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
            }

            try
            {
                change(reservation, now);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return ApplicationResult<ReservationResponse>.Validation(ex.Message);
            }

            reservation.MarkUpdated(context.UserName, now);

            await WriteAuditAsync(
                auditAction,
                ReservationsEntity,
                reservation.Id,
                context,
                auditDetails(reservation),
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<ReservationResponse>.Success(
                Map(reservation, await LoadRoomNumberAsync(reservation.RoomId, cancellationToken)));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<ReservationResponse>.Conflict(ConcurrentReservationMutationRefused);
        }
    }

    /// <summary>
    /// Atomic form of "this reservation is still in the expected status": the invariant travels
    /// as the WHERE clause of one conditional UPDATE on the reservation's own row (the
    /// claim-in-one-statement pattern of <c>AccountingService.TryClaimDraftEntryAsync</c>). The
    /// single written column, UpdatedAt, is one the caller's mutation stamps anyway with the
    /// very same timestamp - the claim adds no state, it only needs to be a write.
    /// </summary>
    private async Task<bool> TryClaimReservationStatusAsync(
        Guid reservationId,
        ReservationStatus expectedStatus,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var claimedRows = await dbContext.Set<Reservation>()
            .Where(current => current.Id == reservationId && current.Status == expectedStatus)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(current => current.UpdatedAt, now),
                cancellationToken);

        return claimedRows == 1;
    }

    /// <summary>
    /// Loads a hotel unit for a mutating operation and refuses missing or inactive ones with a
    /// clean failure. Returns the normalized code alongside so callers stop re-normalizing.
    /// </summary>
    private async Task<(ApplicationResult<T>? Failure, string UnitCode)> RequireActiveHotelUnitAsync<T>(
        string hotelUnitCode,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        if (string.IsNullOrWhiteSpace(normalizedUnitCode))
        {
            return (ApplicationResult<T>.Validation("Hotel unit code is required."), string.Empty);
        }

        var unit = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedUnitCode, cancellationToken);

        if (unit is null)
        {
            return (ApplicationResult<T>.NotFound("Hotel unit was not found."), normalizedUnitCode);
        }

        if (!unit.IsActive)
        {
            return (ApplicationResult<T>.Validation("This operation is not allowed on an inactive hotel unit."), normalizedUnitCode);
        }

        return (null, normalizedUnitCode);
    }

    private async Task<ApplicationResult<T>?> RequireActiveRoomTypeAsync<T>(
        string hotelUnitCode,
        string roomTypeCode,
        CancellationToken cancellationToken)
    {
        var roomType = await dbContext.Set<RoomType>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                current => current.HotelUnitCode == hotelUnitCode && current.Code == roomTypeCode,
                cancellationToken);

        if (roomType is null)
        {
            return ApplicationResult<T>.NotFound(
                $"Room type '{roomTypeCode}' was not found in hotel unit '{hotelUnitCode}'.");
        }

        if (!roomType.IsActive)
        {
            return ApplicationResult<T>.Validation(
                $"Room type '{roomTypeCode}' is inactive and cannot be used.");
        }

        return null;
    }

    /// <summary>
    /// Re-types a failed <see cref="ApplicationResult{T}"/> coming from a collaborator (here the
    /// tariff resolver) without losing its error type or message.
    /// </summary>
    private static ApplicationResult<TTarget> MirrorFailure<TSource, TTarget>(
        ApplicationResult<TSource> source,
        string fallbackMessage)
    {
        var message = source.Error ?? fallbackMessage;

        return source.ErrorType switch
        {
            ApplicationErrorType.NotFound => ApplicationResult<TTarget>.NotFound(message),
            ApplicationErrorType.Conflict => ApplicationResult<TTarget>.Conflict(message),
            _ => ApplicationResult<TTarget>.Validation(message)
        };
    }

    private async Task<string?> LoadRoomNumberAsync(Guid roomId, CancellationToken cancellationToken)
    {
        return await dbContext.Set<Room>()
            .AsNoTracking()
            .Where(room => room.Id == roomId)
            .Select(room => room.Number)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, string>> LoadRoomNumbersAsync(
        Guid[] roomIds,
        CancellationToken cancellationToken)
    {
        if (roomIds.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.Set<Room>()
            .AsNoTracking()
            .Where(room => roomIds.Contains(room.Id))
            .ToDictionaryAsync(room => room.Id, room => room.Number, cancellationToken);
    }

    private static RoomTypeResponse Map(RoomType roomType)
    {
        return new RoomTypeResponse(
            roomType.Id,
            roomType.HotelUnitCode,
            roomType.Code,
            roomType.Label,
            roomType.Capacity,
            roomType.IsActive,
            roomType.CreatedAt,
            roomType.CreatedBy,
            roomType.UpdatedAt,
            roomType.UpdatedBy);
    }

    private static RoomResponse Map(Room room)
    {
        return new RoomResponse(
            room.Id,
            room.HotelUnitCode,
            room.Number,
            room.RoomTypeCode,
            room.IsActive,
            room.CreatedAt,
            room.CreatedBy,
            room.UpdatedAt,
            room.UpdatedBy);
    }

    private static ReservationResponse Map(Reservation reservation, string? roomNumber)
    {
        return new ReservationResponse(
            reservation.Id,
            reservation.HotelUnitCode,
            reservation.RoomId,
            roomNumber,
            reservation.CustomerCode,
            reservation.ArrivalDate,
            reservation.DepartureDate,
            reservation.Nights,
            reservation.GuestCount,
            reservation.Status,
            reservation.NightlyRateSnapshot,
            reservation.RatePlanCodeSnapshot,
            reservation.CancelReason,
            reservation.CheckedInAt,
            reservation.CheckedInBy,
            reservation.CheckedOutAt,
            reservation.CheckedOutBy,
            reservation.CancelledAt,
            reservation.CancelledBy,
            reservation.NoShowAt,
            reservation.NoShowBy,
            reservation.CreatedAt,
            reservation.CreatedBy,
            reservation.UpdatedAt,
            reservation.UpdatedBy);
    }

    private static FolioResponse Map(Folio folio)
    {
        var charges = folio.Charges
            .OrderBy(charge => charge.LineNumber)
            .Select(charge => new FolioChargeResponse(
                charge.Id,
                charge.LineNumber,
                charge.ChargeDate,
                charge.Label,
                charge.Amount,
                charge.Kind,
                charge.Reference))
            .ToArray();

        return new FolioResponse(
            folio.Id,
            folio.ReservationId,
            folio.Balance,
            charges,
            folio.CreatedAt,
            folio.CreatedBy,
            folio.UpdatedAt,
            folio.UpdatedBy);
    }

    private static string NormalizeCodeOrEmpty(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
    }

    private static string? NormalizeNullableCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Explicit flush after the audit write. AuditLogWriter.WriteAsync already calls
    /// SaveChangesAsync internally (persisting the pending entity changes together with the
    /// audit row), so this call is usually a no-op - it exists so persistence never silently
    /// depends on the audit writer's internals.
    /// </summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task WriteAuditAsync(
        string action,
        string entityName,
        Guid entityId,
        OperationContext context,
        object details,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                entityName,
                entityId.ToString(),
                context.IpAddress,
                JsonSerializer.Serialize(details)),
            cancellationToken);
    }
}
