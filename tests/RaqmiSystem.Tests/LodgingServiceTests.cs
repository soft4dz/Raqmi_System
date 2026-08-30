using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Tariffs;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Lodging;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Service-level coverage of the lodging workflows against a dedicated SQLite ":memory:"
/// database (one per test), with the tariff resolution pinned by
/// <see cref="StubTariffResolutionService"/>: rate snapshotting, the anti-double-booking guard
/// and its boundary, the folio generated at check-in, the zero-balance check-out rule, the
/// no-show window and the occupancy figures.
/// </summary>
public sealed class LodgingServiceTests
{
    private const string UnitCode = "HTL1";

    private const string CustomerCode = "CLI1";

    private const decimal NightlyRate = 12_000.00m;

    private static readonly OperationContext Context = new(null, "receptionist", "127.0.0.1");

    [Fact]
    public async Task Reservation_creation_freezes_the_resolved_rate_and_plan()
    {
        await using var harness = await HarnessAsync();

        var result = await harness.Service.CreateReservationAsync(
            new CreateReservationRequest(UnitCode, harness.RoomId, CustomerCode,
                new DateOnly(2030, 5, 1), new DateOnly(2030, 5, 4), 2),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(ReservationStatus.Booked, result.Value!.Status);
        Assert.Equal(NightlyRate, result.Value.NightlyRateSnapshot);
        Assert.Equal("STD", result.Value.RatePlanCodeSnapshot);
        Assert.Equal(3, result.Value.Nights);
        Assert.Equal("101", result.Value.RoomNumber);
        Assert.Equal(1, harness.Resolver.ResolveCallCount);
    }

    [Fact]
    public async Task Reservation_creation_fails_with_the_resolver_message_when_resolution_fails()
    {
        await using var harness = await HarnessAsync();

        harness.Resolver.NextResult = ApplicationResult<ResolvedNightlyRate>
            .Validation("No rate plan covers this night for this room type.");

        var result = await harness.Service.CreateReservationAsync(
            new CreateReservationRequest(UnitCode, harness.RoomId, CustomerCode,
                new DateOnly(2030, 5, 1), new DateOnly(2030, 5, 2), 1),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);
        Assert.Equal("No rate plan covers this night for this room type.", result.Error);

        // A booking without a price never reaches the database.
        Assert.Equal(0, await harness.DbContext.Set<Reservation>().CountAsync());
    }

    [Fact]
    public async Task Guest_count_above_the_room_type_capacity_is_refused()
    {
        await using var harness = await HarnessAsync();

        var result = await harness.Service.CreateReservationAsync(
            new CreateReservationRequest(UnitCode, harness.RoomId, CustomerCode,
                new DateOnly(2030, 5, 1), new DateOnly(2030, 5, 2), 3),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);
        Assert.Contains("capacity", result.Error);
    }

    [Fact]
    public async Task Overlapping_reservations_are_refused_but_the_departure_day_is_free()
    {
        await using var harness = await HarnessAsync();

        var first = await CreateReservationAsync(harness, new DateOnly(2030, 5, 1), new DateOnly(2030, 5, 4));
        Assert.True(first.Succeeded, first.Error);

        // Overlaps the night of the 3rd: refused.
        var overlapping = await CreateReservationAsync(harness, new DateOnly(2030, 5, 3), new DateOnly(2030, 5, 6));
        Assert.False(overlapping.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, overlapping.ErrorType);

        // Boundary case of the invariant: departure on the 4th + arrival on the 4th is fine -
        // the departing guest does not sleep the night of the 4th.
        var backToBack = await CreateReservationAsync(harness, new DateOnly(2030, 5, 4), new DateOnly(2030, 5, 6));
        Assert.True(backToBack.Succeeded, backToBack.Error);
    }

    [Fact]
    public async Task A_cancelled_reservation_no_longer_blocks_the_room()
    {
        await using var harness = await HarnessAsync();

        var first = await CreateReservationAsync(harness, new DateOnly(2030, 6, 1), new DateOnly(2030, 6, 4));
        Assert.True(first.Succeeded, first.Error);

        var cancelled = await harness.Service.CancelReservationAsync(
            first.Value!.Id,
            new CancelReservationRequest("Guest cancelled by phone."),
            Context,
            CancellationToken.None);

        Assert.True(cancelled.Succeeded, cancelled.Error);
        Assert.Equal(ReservationStatus.Cancelled, cancelled.Value!.Status);

        var replacement = await CreateReservationAsync(harness, new DateOnly(2030, 6, 1), new DateOnly(2030, 6, 4));
        Assert.True(replacement.Succeeded, replacement.Error);
    }

    [Fact]
    public async Task Check_in_opens_the_folio_with_one_night_line_per_night_at_the_frozen_rate()
    {
        await using var harness = await HarnessAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var created = await CreateReservationAsync(harness, today, today.AddDays(3));
        Assert.True(created.Succeeded, created.Error);

        var checkedIn = await harness.Service.CheckInAsync(created.Value!.Id, Context, CancellationToken.None);
        Assert.True(checkedIn.Succeeded, checkedIn.Error);
        Assert.Equal(ReservationStatus.CheckedIn, checkedIn.Value!.Status);

        var folio = await harness.Service.GetFolioAsync(created.Value.Id, CancellationToken.None);
        Assert.True(folio.Succeeded, folio.Error);

        Assert.Equal(3, folio.Value!.Charges.Count);
        Assert.All(folio.Value.Charges, charge =>
        {
            Assert.Equal(ChargeKind.Night, charge.Kind);
            Assert.Equal(NightlyRate, charge.Amount);
        });

        // One line per night, dated night by night, never including the departure day.
        Assert.Equal(
            new[] { today, today.AddDays(1), today.AddDays(2) },
            folio.Value.Charges.OrderBy(charge => charge.LineNumber).Select(charge => charge.ChargeDate).ToArray());

        Assert.Equal(3 * NightlyRate, folio.Value.Balance);

        // A second check-in of the same stay is a state conflict, not a second folio.
        var again = await harness.Service.CheckInAsync(created.Value.Id, Context, CancellationToken.None);
        Assert.False(again.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, again.ErrorType);
        Assert.Equal(1, await harness.DbContext.Set<Folio>().CountAsync());
    }

    [Fact]
    public async Task Check_in_before_the_utc_eve_of_the_arrival_date_is_refused()
    {
        await using var harness = await HarnessAsync();

        // Arrival the day after tomorrow: outside the one-day timezone tolerance, refused.
        var dayAfterTomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
        var created = await CreateReservationAsync(harness, dayAfterTomorrow, dayAfterTomorrow.AddDays(2));
        Assert.True(created.Succeeded, created.Error);

        var result = await harness.Service.CheckInAsync(created.Value!.Id, Context, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);
        Assert.Contains("before its arrival date", result.Error);

        // The refusal left no folio behind.
        Assert.Equal(0, await harness.DbContext.Set<Folio>().CountAsync());
    }

    [Fact]
    public async Task Check_in_on_the_utc_eve_of_the_arrival_date_is_accepted()
    {
        await using var harness = await HarnessAsync();

        // Arrival tomorrow (UTC): a guest east of UTC arriving just after local midnight on the
        // arrival day is still on today's UTC date - the eve - and must be let in.
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var created = await CreateReservationAsync(harness, tomorrow, tomorrow.AddDays(2));
        Assert.True(created.Succeeded, created.Error);

        var result = await harness.Service.CheckInAsync(created.Value!.Id, Context, CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(ReservationStatus.CheckedIn, result.Value!.Status);
    }

    [Fact]
    public async Task Check_in_after_the_departure_date_is_refused()
    {
        await using var harness = await HarnessAsync();

        // A stale Booked stay whose whole period is behind us: checking it in would open a
        // folio billing every original night months after the fact.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var created = await CreateReservationAsync(harness, today.AddDays(-10), today.AddDays(-7));
        Assert.True(created.Succeeded, created.Error);

        var result = await harness.Service.CheckInAsync(created.Value!.Id, Context, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);
        Assert.Contains("departure date has passed", result.Error);
        Assert.Equal(0, await harness.DbContext.Set<Folio>().CountAsync());
    }

    [Fact]
    public async Task Check_out_is_refused_while_the_folio_balance_is_not_zero()
    {
        await using var harness = await HarnessAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var created = await CreateReservationAsync(harness, today, today.AddDays(2));
        Assert.True(created.Succeeded, created.Error);

        var reservationId = created.Value!.Id;

        Assert.True((await harness.Service.CheckInAsync(reservationId, Context, CancellationToken.None)).Succeeded);

        // 2 nights at 12000 are on the folio: leaving now would leave 24000 unpaid.
        var refused = await harness.Service.CheckOutAsync(reservationId, Context, CancellationToken.None);
        Assert.False(refused.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, refused.ErrorType);
        Assert.Contains("not zero", refused.Error);

        // The normal path: the payment goes through treasury, then a Settlement line
        // referencing the receipt brings the folio to zero.
        var settled = await harness.Service.AddFolioChargeAsync(
            reservationId,
            new AddFolioChargeRequest(today, "Reglement especes", -2 * NightlyRate, ChargeKind.Settlement, "REC-0042"),
            Context,
            CancellationToken.None);

        Assert.True(settled.Succeeded, settled.Error);
        Assert.Equal(0m, settled.Value!.Balance);

        var checkedOut = await harness.Service.CheckOutAsync(reservationId, Context, CancellationToken.None);
        Assert.True(checkedOut.Succeeded, checkedOut.Error);
        Assert.Equal(ReservationStatus.CheckedOut, checkedOut.Value!.Status);

        // The folio is closed with the stay: no line lands on a checked-out reservation.
        var lateCharge = await harness.Service.AddFolioChargeAsync(
            reservationId,
            new AddFolioChargeRequest(today, "Minibar", 500m, ChargeKind.Extra),
            Context,
            CancellationToken.None);

        Assert.False(lateCharge.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, lateCharge.ErrorType);
    }

    [Fact]
    public async Task Folio_charges_require_a_checked_in_reservation()
    {
        await using var harness = await HarnessAsync();

        var created = await CreateReservationAsync(
            harness,
            new DateOnly(2030, 7, 1),
            new DateOnly(2030, 7, 3));
        Assert.True(created.Succeeded, created.Error);

        var result = await harness.Service.AddFolioChargeAsync(
            created.Value!.Id,
            new AddFolioChargeRequest(new DateOnly(2030, 7, 1), "Minibar", 500m, ChargeKind.Extra),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task No_show_is_only_accepted_once_the_arrival_date_has_passed()
    {
        await using var harness = await HarnessAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Arrival today: the guest may still arrive tonight, so no-show is premature.
        var arrivingToday = await CreateReservationAsync(harness, today, today.AddDays(1));
        Assert.True(arrivingToday.Succeeded, arrivingToday.Error);

        var premature = await harness.Service.MarkNoShowAsync(arrivingToday.Value!.Id, Context, CancellationToken.None);
        Assert.False(premature.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, premature.ErrorType);

        // A stay whose arrival date is behind us can be written off.
        var missed = await CreateReservationAsync(harness, today.AddDays(-2), today.AddDays(-1));
        Assert.True(missed.Succeeded, missed.Error);

        var noShow = await harness.Service.MarkNoShowAsync(missed.Value!.Id, Context, CancellationToken.None);
        Assert.True(noShow.Succeeded, noShow.Error);
        Assert.Equal(ReservationStatus.NoShow, noShow.Value!.Status);
    }

    [Fact]
    public async Task Occupancy_is_exact_on_a_constructed_set()
    {
        await using var harness = await HarnessAsync();

        // Two more rooms: 102 active (counts in the denominator), 103 inactive (does not).
        var room102 = new Room(UnitCode, "102", "DBL");
        var room103 = new Room(UnitCode, "103", "DBL");
        room103.Deactivate();
        harness.DbContext.Set<Room>().AddRange(room102, room103);

        var d1 = new DateOnly(2030, 8, 1);

        // Room 101, Booked over [d1, d1+2): occupies the nights of d1 and d1+1.
        var booked = new Reservation(UnitCode, harness.RoomId, CustomerCode, d1, d1.AddDays(2), 1, NightlyRate, "STD");

        // Room 102, CheckedIn over [d1+1, d1+3): occupies the nights of d1+1 and d1+2.
        var checkedIn = new Reservation(UnitCode, room102.Id, CustomerCode, d1.AddDays(1), d1.AddDays(3), 1, NightlyRate, "STD");
        checkedIn.CheckIn(d1.AddDays(1), "receptionist", DateTimeOffset.UtcNow);

        // Room 101 again, CheckedOut over [d1+2, d1+3): a PAST stay whose guest has left. It
        // still occupied the night of d1+2 - historical occupancy must not be retroactively
        // under-counted once guests check out.
        var checkedOut = new Reservation(UnitCode, harness.RoomId, CustomerCode, d1.AddDays(2), d1.AddDays(3), 1, NightlyRate, "STD");
        checkedOut.CheckIn(d1.AddDays(2), "receptionist", DateTimeOffset.UtcNow);
        checkedOut.CheckOut("receptionist", DateTimeOffset.UtcNow);

        // Room 101 again, Cancelled over the whole window: must not count anywhere.
        var cancelled = new Reservation(UnitCode, harness.RoomId, CustomerCode, d1, d1.AddDays(5), 1, NightlyRate, "STD");
        cancelled.Cancel("Cancelled for the occupancy test.", "receptionist", DateTimeOffset.UtcNow);

        harness.DbContext.Set<Reservation>().AddRange(booked, checkedIn, checkedOut, cancelled);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetOccupancyAsync(UnitCode, d1, d1.AddDays(2), CancellationToken.None);
        Assert.True(result.Succeeded, result.Error);

        var days = result.Value!.Days.OrderBy(day => day.Date).ToArray();
        Assert.Equal(3, days.Length);

        Assert.All(days, day => Assert.Equal(2, day.TotalActiveRooms));

        Assert.Equal(1, days[0].OccupiedRooms);
        Assert.Equal(50.00m, days[0].OccupancyRatePercent);

        Assert.Equal(2, days[1].OccupiedRooms);
        Assert.Equal(100.00m, days[1].OccupancyRatePercent);

        // d1+2: room 102 (in house) AND room 101 (stay finished, night consumed anyway).
        Assert.Equal(2, days[2].OccupiedRooms);
        Assert.Equal(100.00m, days[2].OccupancyRatePercent);
    }

    [Fact]
    public async Task Occupancy_validates_its_window_and_unit()
    {
        await using var harness = await HarnessAsync();

        var inverted = await harness.Service.GetOccupancyAsync(
            UnitCode, new DateOnly(2030, 8, 2), new DateOnly(2030, 8, 1), CancellationToken.None);
        Assert.Equal(ApplicationErrorType.Validation, inverted.ErrorType);

        var unknownUnit = await harness.Service.GetOccupancyAsync(
            "NOPE", new DateOnly(2030, 8, 1), new DateOnly(2030, 8, 2), CancellationToken.None);
        Assert.Equal(ApplicationErrorType.NotFound, unknownUnit.ErrorType);

        var tooWide = await harness.Service.GetOccupancyAsync(
            UnitCode, new DateOnly(2030, 1, 1), new DateOnly(2031, 6, 1), CancellationToken.None);
        Assert.Equal(ApplicationErrorType.Validation, tooWide.ErrorType);
    }

    private static Task<ApplicationResult<ReservationResponse>> CreateReservationAsync(
        Harness harness,
        DateOnly arrival,
        DateOnly departure)
    {
        return harness.Service.CreateReservationAsync(
            new CreateReservationRequest(UnitCode, harness.RoomId, CustomerCode, arrival, departure, 2),
            Context,
            CancellationToken.None);
    }

    /// <summary>
    /// One isolated database per test: unit HTL1, room type DBL (capacity 2), room 101 and
    /// customer CLI1, plus the service wired to the stub resolver at 12000/night on plan STD.
    /// </summary>
    private static async Task<Harness> HarnessAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var dbContext = new RaqmiDbContext(
            new DbContextOptionsBuilder<RaqmiDbContext>()
                .UseSqlite(connection)
                .Options);

        await dbContext.Database.EnsureCreatedAsync();

        var room = new Room(UnitCode, "101", "DBL");

        dbContext.Set<HotelUnit>().Add(new HotelUnit(UnitCode, "Hotel Test", HotelUnitType.Hotel));
        dbContext.Set<RoomType>().Add(new RoomType(UnitCode, "DBL", "Chambre double", 2));
        dbContext.Set<Room>().Add(room);
        dbContext.Set<Customer>().Add(new Customer(CustomerCode, "Client Un", CustomerType.Individual));
        await dbContext.SaveChangesAsync();

        var resolver = new StubTariffResolutionService(NightlyRate, "STD");

        return new Harness(
            connection,
            dbContext,
            resolver,
            new LodgingService(dbContext, new AuditLogWriter(dbContext), resolver),
            room.Id);
    }

    private sealed class Harness(
        SqliteConnection connection,
        RaqmiDbContext dbContext,
        StubTariffResolutionService resolver,
        LodgingService service,
        Guid roomId) : IAsyncDisposable
    {
        public RaqmiDbContext DbContext { get; } = dbContext;

        public StubTariffResolutionService Resolver { get; } = resolver;

        public LodgingService Service { get; } = service;

        public Guid RoomId { get; } = roomId;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
