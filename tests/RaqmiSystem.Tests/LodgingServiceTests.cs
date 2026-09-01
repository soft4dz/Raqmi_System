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
        Assert.Equal(ReservationStatus.Confirmed, result.Value!.Status);
        Assert.Equal(NightlyRate, result.Value.NightlyRateSnapshot);
        Assert.Equal("STD", result.Value.RatePlanCodeSnapshot);
        Assert.Equal(3, result.Value.Nights);
        Assert.Equal("101", result.Value.RoomNumber);

        // One resolution per night: the whole stay is priced at creation, not just the arrival
        // night flattened over every night.
        Assert.Equal(3, harness.Resolver.ResolveCallCount);
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
        Assert.Contains("depasse ce que le type", result.Error);
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
    public async Task Check_in_opens_the_folio_and_pose_la_nuit_d_arrivee()
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

        // L'ARRIVEE NE POSE QUE LA NUIT D'ARRIVEE. Les nuits suivantes appartiennent a leur
        // propre journee d'exploitation et sont posees par le night audit, nuit apres nuit ; le
        // depart rattrape ce qui manquerait. Poser tout le sejour a l'arrivee rattacherait trois
        // nuitees a la meme journee et fausserait toute recette journaliere.
        var nightLine = Assert.Single(folio.Value!.Charges);
        Assert.Equal(ChargeKind.Night, nightLine.Kind);
        Assert.Equal(NightlyRate, nightLine.Amount);
        Assert.Equal(today, nightLine.ChargeDate);
        Assert.Equal(NightlyRate, folio.Value.Balance);

        // La cle de geste est ce qui rend le posting idempotent : c'est elle que le night audit
        // retrouvera pour ne pas reposer cette nuit.
        Assert.NotNull(nightLine.SourceReference);

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
        Assert.Contains("avant sa date d'arrivee", result.Error);

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
        Assert.Contains("date de depart est passee", result.Error);
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
        Assert.Contains("ne sont pas soldes", refused.Error);

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
        var booked = TestReservations.Create(UnitCode, harness.RoomId, CustomerCode, d1, d1.AddDays(2), 1, NightlyRate, "STD");

        // Room 102, CheckedIn over [d1+1, d1+3): occupies the nights of d1+1 and d1+2.
        var checkedIn = TestReservations.Create(UnitCode, room102.Id, CustomerCode, d1.AddDays(1), d1.AddDays(3), 1, NightlyRate, "STD");
        checkedIn.CheckIn(d1.AddDays(1), "receptionist", DateTimeOffset.UtcNow);

        // Room 101 again, CheckedOut over [d1+2, d1+3): a PAST stay whose guest has left. It
        // still occupied the night of d1+2 - historical occupancy must not be retroactively
        // under-counted once guests check out.
        var checkedOut = TestReservations.Create(UnitCode, harness.RoomId, CustomerCode, d1.AddDays(2), d1.AddDays(3), 1, NightlyRate, "STD");
        checkedOut.CheckIn(d1.AddDays(2), "receptionist", DateTimeOffset.UtcNow);
        checkedOut.CheckOut("receptionist", DateTimeOffset.UtcNow);

        // Room 101 again, Cancelled over the whole window: must not count anywhere.
        var cancelled = TestReservations.Create(UnitCode, harness.RoomId, CustomerCode, d1, d1.AddDays(5), 1, NightlyRate, "STD");
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

    // Availability ---------------------------------------------------------------------------

    [Fact]
    public async Task Availability_lists_free_rooms_and_excludes_overlapping_undersized_and_inactive_ones()
    {
        await using var harness = await HarnessAsync();

        // 102: same type, free - must be listed. 103: inactive - never listed. 201: single
        // (capacity 1) - filtered out for a party of 2.
        var room102 = new Room(UnitCode, "102", "DBL");
        var room103 = new Room(UnitCode, "103", "DBL");
        room103.Deactivate();
        var room201 = new Room(UnitCode, "201", "SGL");
        harness.DbContext.Set<RoomType>().Add(new RoomType(UnitCode, "SGL", "Chambre simple", 1));
        harness.DbContext.Set<Room>().AddRange(room102, room103, room201);
        await harness.DbContext.SaveChangesAsync();

        // 101 is taken over [d, d+2).
        var d = new DateOnly(2030, 5, 1);
        var created = await CreateReservationAsync(harness, d, d.AddDays(2));
        Assert.True(created.Succeeded, created.Error);

        var result = await harness.Service.GetAvailabilityAsync(
            UnitCode, d, d.AddDays(2), 2, null, CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(2, result.Value!.Nights);

        var onlyRoom = Assert.Single(result.Value.Rooms);
        Assert.Equal("102", onlyRoom.RoomNumber);
        Assert.True(onlyRoom.HasRate);
        Assert.Equal(2 * NightlyRate, onlyRoom.TotalStayAmount);
        Assert.Equal(2, onlyRoom.NightlyRates.Count);
        Assert.Equal("STD", onlyRoom.RatePlanCode);

        // Same boundary rule as the creation guard: the departure day is free, so a search
        // starting on the departure date sees 101 again.
        var backToBack = await harness.Service.GetAvailabilityAsync(
            UnitCode, d.AddDays(2), d.AddDays(3), 2, null, CancellationToken.None);

        Assert.True(backToBack.Succeeded, backToBack.Error);
        Assert.Contains(backToBack.Value!.Rooms, room => room.RoomNumber == "101");
    }

    [Fact]
    public async Task Availability_totals_a_stay_crossing_two_rate_periods_night_by_night()
    {
        await using var harness = await HarnessAsync();

        // Low season until the 31st of May at 10000, high season from the 1st of June at 15000.
        var boundary = new DateOnly(2030, 6, 1);

        harness.Resolver.RateByNight = (night, _) =>
            ApplicationResult<ResolvedNightlyRate>.Success(night < boundary
                ? new ResolvedNightlyRate(10_000.00m, "STD", null, null)
                : new ResolvedNightlyRate(15_000.00m, "STD", null, null));

        var result = await harness.Service.GetAvailabilityAsync(
            UnitCode, boundary.AddDays(-1), boundary.AddDays(1), 2, null, CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);

        var room = Assert.Single(result.Value!.Rooms);
        Assert.True(room.HasRate);

        // The total is the SUM of the per-night rates, not one rate flattened over the stay.
        Assert.Equal(25_000.00m, room.TotalStayAmount);
        Assert.Equal(
            new[] { 10_000.00m, 15_000.00m },
            room.NightlyRates.OrderBy(rate => rate.Night).Select(rate => rate.Amount).ToArray());
    }

    [Fact]
    public async Task Availability_flags_a_room_without_rate_coverage_instead_of_hiding_it()
    {
        await using var harness = await HarnessAsync();

        var d = new DateOnly(2030, 5, 1);

        // The second night has no rate period: a real tariff-setup hole.
        harness.Resolver.RateByNight = (night, _) => night == d.AddDays(1)
            ? ApplicationResult<ResolvedNightlyRate>.NotFound(
                "Rate plan 'STD' has no period covering the night of 2030-05-02 for room type 'DBL'.")
            : ApplicationResult<ResolvedNightlyRate>.Success(
                new ResolvedNightlyRate(NightlyRate, "STD", null, null));

        var result = await harness.Service.GetAvailabilityAsync(
            UnitCode, d, d.AddDays(3), 2, null, CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);

        // The room stays VISIBLE - the operator must see the pricing hole, not a full hotel.
        var room = Assert.Single(result.Value!.Rooms);
        Assert.False(room.HasRate);
        Assert.Null(room.TotalStayAmount);
        Assert.Contains("2030-05-02", room.RateIssue);
        Assert.Contains("has no period covering", room.RateIssue);

        // The nights priced before the hole are listed, showing exactly where coverage stops.
        var pricedNight = Assert.Single(room.NightlyRates);
        Assert.Equal(d, pricedNight.Night);
    }

    [Fact]
    public async Task Availability_applies_the_customer_convention_through_the_resolver()
    {
        await using var harness = await HarnessAsync();

        harness.Resolver.RateByNight = (_, customerCode) => customerCode == CustomerCode
            ? ApplicationResult<ResolvedNightlyRate>.Success(
                new ResolvedNightlyRate(10_800.00m, "CONV", CustomerCode, 10m))
            : ApplicationResult<ResolvedNightlyRate>.Success(
                new ResolvedNightlyRate(NightlyRate, "STD", null, null));

        var conventioned = await harness.Service.GetAvailabilityAsync(
            UnitCode, new DateOnly(2030, 5, 1), new DateOnly(2030, 5, 3), 2, CustomerCode, CancellationToken.None);

        Assert.True(conventioned.Succeeded, conventioned.Error);

        var room = Assert.Single(conventioned.Value!.Rooms);
        Assert.Equal(2 * 10_800.00m, room.TotalStayAmount);
        Assert.Equal("CONV", room.RatePlanCode);
        Assert.Equal(CustomerCode, room.ConventionCustomerCode);
        Assert.Equal(10m, room.DiscountPercent);

        // An unknown customer cannot be quoted convention rates it could never book at.
        var unknownCustomer = await harness.Service.GetAvailabilityAsync(
            UnitCode, new DateOnly(2030, 5, 1), new DateOnly(2030, 5, 3), 2, "NOPE", CancellationToken.None);

        Assert.Equal(ApplicationErrorType.NotFound, unknownCustomer.ErrorType);
    }

    [Fact]
    public async Task Availability_validates_window_guests_and_unit()
    {
        await using var harness = await HarnessAsync();

        var inverted = await harness.Service.GetAvailabilityAsync(
            UnitCode, new DateOnly(2030, 5, 2), new DateOnly(2030, 5, 2), 2, null, CancellationToken.None);
        Assert.Equal(ApplicationErrorType.Validation, inverted.ErrorType);

        var tooWide = await harness.Service.GetAvailabilityAsync(
            UnitCode, new DateOnly(2030, 5, 1), new DateOnly(2030, 9, 1), 2, null, CancellationToken.None);
        Assert.Equal(ApplicationErrorType.Validation, tooWide.ErrorType);

        var noGuests = await harness.Service.GetAvailabilityAsync(
            UnitCode, new DateOnly(2030, 5, 1), new DateOnly(2030, 5, 2), 0, null, CancellationToken.None);
        Assert.Equal(ApplicationErrorType.Validation, noGuests.ErrorType);

        var unknownUnit = await harness.Service.GetAvailabilityAsync(
            "NOPE", new DateOnly(2030, 5, 1), new DateOnly(2030, 5, 2), 2, null, CancellationToken.None);
        Assert.Equal(ApplicationErrorType.NotFound, unknownUnit.ErrorType);
    }

    /// <summary>
    /// The coherence contract of this wave: the total the availability search announces for a
    /// room is EXACTLY what the folio bills after booking that room and checking the guest in -
    /// including when the rates differ from night to night across rate periods.
    /// </summary>
    [Fact]
    public async Task Availability_total_is_exactly_what_the_folio_bills_after_check_in()
    {
        await using var harness = await HarnessAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Three nights, three different prices (a boundary plus a one-night event rate).
        var scriptedRates = new Dictionary<DateOnly, decimal>
        {
            [today] = 12_000.00m,
            [today.AddDays(1)] = 13_500.50m,
            [today.AddDays(2)] = 11_000.00m
        };

        harness.Resolver.RateByNight = (night, _) => scriptedRates.TryGetValue(night, out var rate)
            ? ApplicationResult<ResolvedNightlyRate>.Success(new ResolvedNightlyRate(rate, "STD", null, null))
            : ApplicationResult<ResolvedNightlyRate>.NotFound("No rate scripted for this night.");

        var availability = await harness.Service.GetAvailabilityAsync(
            UnitCode, today, today.AddDays(3), 2, CustomerCode, CancellationToken.None);

        Assert.True(availability.Succeeded, availability.Error);

        var announcedRoom = Assert.Single(availability.Value!.Rooms);
        Assert.True(announcedRoom.HasRate);
        Assert.Equal(36_500.50m, announcedRoom.TotalStayAmount);

        // Book the announced room over the announced dates, then check the guest in.
        var created = await CreateReservationAsync(harness, today, today.AddDays(3));
        Assert.True(created.Succeeded, created.Error);

        // The flat snapshot stays the ARRIVAL night's rate.
        Assert.Equal(12_000.00m, created.Value!.NightlyRateSnapshot);

        var checkedIn = await harness.Service.CheckInAsync(created.Value.Id, Context, CancellationToken.None);
        Assert.True(checkedIn.Succeeded, checkedIn.Error);

        // LE DEPART RATTRAPE LES NUITS NON POSEES. Le night audit n'a pas tourne dans ce test :
        // sans rattrapage, le client partirait en n'ayant paye que sa premiere nuit. C'est
        // exactement l'ecart que ce test existe pour interdire.
        var caughtUp = await harness.Service.CheckOutAsync(created.Value.Id, Context, CancellationToken.None);
        Assert.False(caughtUp.Succeeded);
        Assert.Contains("ne sont pas soldes", caughtUp.Error);

        var folio = await harness.Service.GetFolioAsync(created.Value.Id, CancellationToken.None);
        Assert.True(folio.Succeeded, folio.Error);

        // Le folio facture le total annonce, nuit par nuit, aux tarifs annonces.
        Assert.Equal(announcedRoom.TotalStayAmount, folio.Value!.Balance);
        Assert.Equal(
            announcedRoom.NightlyRates.OrderBy(rate => rate.Night).Select(rate => rate.Amount).ToArray(),
            folio.Value.Charges
                .Where(charge => charge.Kind == ChargeKind.Night)
                .OrderBy(charge => charge.ChargeDate)
                .Select(charge => charge.Amount)
                .ToArray());
    }

    // Front desk -----------------------------------------------------------------------------

    [Fact]
    public async Task Front_desk_splits_arrivals_departures_and_overdue_lists_with_folio_balances()
    {
        await using var harness = await HarnessAsync();

        var rooms = new[]
        {
            new Room(UnitCode, "102", "DBL"),
            new Room(UnitCode, "103", "DBL"),
            new Room(UnitCode, "104", "DBL"),
            new Room(UnitCode, "105", "DBL")
        };
        harness.DbContext.Set<Room>().AddRange(rooms);

        var day = new DateOnly(2030, 9, 10);

        // Arrival of the day: Booked, arriving on the 10th (room 101).
        var arrivingToday = TestReservations.Create(
            UnitCode, harness.RoomId, CustomerCode, day, day.AddDays(2), 2, NightlyRate, "STD");

        // Overdue arrival: Booked, should have arrived on the 8th - a no-show candidate (102).
        var overdueArrival = TestReservations.Create(
            UnitCode, rooms[0].Id, CustomerCode, day.AddDays(-2), day.AddDays(1), 1, NightlyRate, "STD");

        // Departure of the day: CheckedIn, leaving on the 10th, folio NOT settled (103).
        var departingToday = TestReservations.Create(
            UnitCode, rooms[1].Id, CustomerCode, day.AddDays(-2), day, 2, NightlyRate, "STD");
        departingToday.CheckIn(day.AddDays(-2), "receptionist", DateTimeOffset.UtcNow);

        var departingFolio = new Folio(departingToday.Id, UnitCode, TestReservations.NextNumber());
        departingFolio.AddCharge(new FolioCharge(day.AddDays(-2), "Night", NightlyRate, ChargeKind.Night));
        departingFolio.AddCharge(new FolioCharge(day.AddDays(-1), "Night", NightlyRate, ChargeKind.Night));
        departingFolio.AddCharge(new FolioCharge(
            day.AddDays(-1), "Acompte", -NightlyRate, ChargeKind.Settlement, "REC-0100"));

        // Overdue departure: CheckedIn, should have left on the 9th, folio unpaid (104).
        var overdueDeparture = TestReservations.Create(
            UnitCode, rooms[2].Id, CustomerCode, day.AddDays(-3), day.AddDays(-1), 1, NightlyRate, "STD");
        overdueDeparture.CheckIn(day.AddDays(-3), "receptionist", DateTimeOffset.UtcNow);

        var overdueFolio = new Folio(overdueDeparture.Id, UnitCode, TestReservations.NextNumber());
        overdueFolio.AddCharge(new FolioCharge(day.AddDays(-3), "Night", NightlyRate, ChargeKind.Night));
        overdueFolio.AddCharge(new FolioCharge(day.AddDays(-2), "Night", NightlyRate, ChargeKind.Night));

        // In house: CheckedIn, sleeping the night of the 10th, leaving later (105).
        var inHouse = TestReservations.Create(
            UnitCode, rooms[3].Id, CustomerCode, day.AddDays(-1), day.AddDays(2), 2, NightlyRate, "STD");
        inHouse.CheckIn(day.AddDays(-1), "receptionist", DateTimeOffset.UtcNow);

        harness.DbContext.Set<Reservation>().AddRange(
            arrivingToday, overdueArrival, departingToday, overdueDeparture, inHouse);
        harness.DbContext.Set<Folio>().AddRange(departingFolio, overdueFolio);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetFrontDeskAsync(UnitCode, day, CancellationToken.None);
        Assert.True(result.Succeeded, result.Error);

        var frontDesk = result.Value!;

        var arrival = Assert.Single(frontDesk.Arrivals);
        Assert.Equal(arrivingToday.Id, arrival.ReservationId);
        Assert.Equal("101", arrival.RoomNumber);
        Assert.Equal("Client Un", arrival.CustomerName);
        Assert.Equal(2, arrival.Nights);
        Assert.Equal(NightlyRate, arrival.NightlyRateSnapshot);
        Assert.Equal(2 * NightlyRate, arrival.TotalStayAmount);

        var lateArrival = Assert.Single(frontDesk.OverdueArrivals);
        Assert.Equal(overdueArrival.Id, lateArrival.ReservationId);
        Assert.Equal("102", lateArrival.RoomNumber);

        // The receptionist sees WHO STILL OWES WHAT before letting anyone leave.
        var departure = Assert.Single(frontDesk.Departures);
        Assert.Equal(departingToday.Id, departure.ReservationId);
        Assert.Equal("103", departure.RoomNumber);
        Assert.Equal(NightlyRate, departure.FolioBalance);

        var lateDeparture = Assert.Single(frontDesk.OverdueDepartures);
        Assert.Equal(overdueDeparture.Id, lateDeparture.ReservationId);
        Assert.Equal(2 * NightlyRate, lateDeparture.FolioBalance);

        // Only the stay actually covering the night of the 10th counts as in house.
        Assert.Equal(1, frontDesk.InHouseCount);

        // The day's occupancy rides along, computed by the exact same logic as /occupancy.
        Assert.Equal(day, frontDesk.Occupancy.Date);
        Assert.Equal(5, frontDesk.Occupancy.TotalActiveRooms);

        var unknownUnit = await harness.Service.GetFrontDeskAsync("NOPE", day, CancellationToken.None);
        Assert.Equal(ApplicationErrorType.NotFound, unknownUnit.ErrorType);
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
