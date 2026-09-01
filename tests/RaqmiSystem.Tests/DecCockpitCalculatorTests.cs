using RaqmiSystem.Application.Pilotage;
using RaqmiSystem.Domain.Closing;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Tests;

/// <summary>
/// Pure-calculation coverage of the DEC cockpit: exact ages at the 48-hour boundary,
/// yesterday vs the day before yesterday in the closing backlog, correct spotting of
/// rejected-awaiting-correction entries (Status == Rejected - a corrected entry goes back to
/// Draft with its RejectionReason cleared and must disappear from the queue), Draft-only
/// payment orders, and the per-unit health rules.
/// </summary>
public sealed class DecCockpitCalculatorTests
{
    private static readonly DateOnly Date = new(2026, 8, 30);
    private static readonly DateTimeOffset UtcNow = new(2026, 8, 30, 8, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyDictionary<string, DateOnly> NoActivity =
        new Dictionary<string, DateOnly>();

    private static DecCockpitResponse Build(
        IReadOnlyCollection<HotelUnit>? units = null,
        IReadOnlyCollection<DailyRevenue>? revenues = null,
        IReadOnlyCollection<DailyClosing>? closings = null,
        IReadOnlyDictionary<string, DateOnly>? firstActivity = null,
        IReadOnlyCollection<PaymentOrder>? paymentOrders = null,
        IReadOnlyCollection<Room>? rooms = null,
        IReadOnlyCollection<Reservation>? reservations = null)
    {
        return DecCockpitCalculator.Build(
            Date,
            UtcNow,
            units ?? [],
            revenues ?? [],
            closings ?? [],
            firstActivity ?? NoActivity,
            paymentOrders ?? [],
            rooms ?? [],
            reservations ?? []);
    }

    private static DailyRevenue SubmittedRevenue(
        string unitCode,
        DateOnly businessDate,
        decimal accommodation,
        DateTimeOffset submittedAt)
    {
        var revenue = new DailyRevenue(businessDate, unitCode, accommodation, 0m, 0m, 0m);
        revenue.Submit("controller", submittedAt);
        return revenue;
    }

    [Fact]
    public void Pending_validations_group_by_unit_with_exact_ages_at_the_48_hour_boundary()
    {
        var unit = new HotelUnit("EL-MANAR", "Hotel El Manar", HotelUnitType.Hotel, 1);
        var otherUnit = new HotelUnit("EL-MARSA", "Hotel El Marsa", HotelUnitType.Hotel, 2);

        // Exactly 48 hours old: 2 whole days. One second less: still 1 day.
        var exactly48h = SubmittedRevenue(unit.Code, Date.AddDays(-2), 100m, UtcNow.AddHours(-48));
        var almost48h = SubmittedRevenue(otherUnit.Code, Date.AddDays(-1), 250m, UtcNow.AddHours(-48).AddSeconds(1));
        var recent = SubmittedRevenue(unit.Code, Date.AddDays(-1), 40m, UtcNow.AddHours(-2));

        // A rejected entry must never enter the validation queue.
        var rejected = SubmittedRevenue(unit.Code, Date.AddDays(-3), 999m, UtcNow.AddDays(-3));
        rejected.Reject("Montant incoherent.", "dec", UtcNow.AddDays(-3));

        var result = Build(
            units: [unit, otherUnit],
            revenues: [exactly48h, almost48h, recent, rejected]);

        Assert.Equal(3, result.PendingValidationCount);
        Assert.Equal(390m, result.PendingValidationAmount);
        Assert.Equal(2, result.PendingValidations.Count);

        // Sorted by oldest age descending: EL-MANAR (2 days) before EL-MARSA (1 day).
        var first = result.PendingValidations.First();
        Assert.Equal("EL-MANAR", first.HotelUnitCode);
        Assert.Equal("Hotel El Manar", first.HotelUnitName);
        Assert.Equal(2, first.Count);
        Assert.Equal(140m, first.TotalAmount);
        Assert.Equal(Date.AddDays(-2), first.OldestBusinessDate);
        Assert.Equal(2, first.OldestAgeDays);

        var second = result.PendingValidations.Last();
        Assert.Equal("EL-MARSA", second.HotelUnitCode);
        Assert.Equal(1, second.OldestAgeDays);
    }

    [Fact]
    public void Rejected_queue_contains_rejected_entries_and_ignores_corrected_ones()
    {
        var unit = new HotelUnit("EL-MANAR", "Hotel El Manar", HotelUnitType.Hotel, 1);

        var awaitingCorrection = SubmittedRevenue(unit.Code, Date.AddDays(-4), 300m, UtcNow.AddDays(-3));
        awaitingCorrection.Reject("Ecart de caisse.", "dec", UtcNow.AddHours(-48));

        // Corrected after rejection: back to Draft, RejectionReason cleared by UpdateAmounts -
        // the exact mechanic of DailyRevenue. It must NOT appear in the queue any more.
        var corrected = SubmittedRevenue(unit.Code, Date.AddDays(-5), 500m, UtcNow.AddDays(-4));
        corrected.Reject("Categorie erronee.", "dec", UtcNow.AddDays(-4));
        corrected.UpdateAmounts(450m, 0m, 0m, 0m, null);

        Assert.Equal(DailyRevenueStatus.Draft, corrected.Status);
        Assert.Null(corrected.RejectionReason);

        var result = Build(units: [unit], revenues: [awaitingCorrection, corrected]);

        Assert.Equal(1, result.RejectedCount);

        var item = Assert.Single(result.RejectedRevenues);
        Assert.Equal(awaitingCorrection.Id, item.Id);
        Assert.Equal("Ecart de caisse.", item.RejectionReason);
        Assert.Equal(300m, item.Total);

        // Rejected exactly 48 hours ago: age is exactly 2 days.
        Assert.Equal(2, item.AgeDays);
    }

    [Fact]
    public void Closing_backlog_flags_the_day_before_yesterday_but_never_yesterday()
    {
        var unit = new HotelUnit("EL-MANAR", "Hotel El Manar", HotelUnitType.Hotel, 1);
        var yesterday = Date.AddDays(-1);
        var dayBeforeYesterday = Date.AddDays(-2);

        // Activity starts the day before yesterday; NOTHING is closed at all.
        var firstActivity = new Dictionary<string, DateOnly> { [unit.Code] = dayBeforeYesterday };

        var result = Build(units: [unit], firstActivity: firstActivity);

        var backlogUnit = Assert.Single(result.ClosingBacklog);
        Assert.Equal(unit.Code, backlogUnit.HotelUnitCode);

        // Only the day before yesterday is late: yesterday is today's normal work.
        Assert.Equal([dayBeforeYesterday], backlogUnit.MissingDates);
        Assert.Equal(dayBeforeYesterday, backlogUnit.OldestMissingDate);
        Assert.Equal(2, backlogUnit.OldestAgeDays);
        Assert.Equal(1, result.ClosingBacklogDayCount);

        Assert.NotNull(result.OldestClosingDelay);
        Assert.Equal(dayBeforeYesterday, result.OldestClosingDelay!.BusinessDate);
        Assert.Equal(2, result.OldestClosingDelay.AgeDays);
    }

    [Fact]
    public void Closing_backlog_treats_a_reopened_day_as_not_closed_and_respects_first_activity()
    {
        var unit = new HotelUnit("EL-MANAR", "Hotel El Manar", HotelUnitType.Hotel, 1);
        var freshUnit = new HotelUnit("EL-JADID", "Hotel El Jadid", HotelUnitType.Hotel, 2);

        var closedDay = Date.AddDays(-3);
        var reopenedDay = Date.AddDays(-2);

        var closed = new DailyClosing(closedDay, unit.Code, "auditor", UtcNow.AddDays(-2));

        var reopened = new DailyClosing(reopenedDay, unit.Code, "auditor", UtcNow.AddDays(-1));
        reopened.Reopen("Ecart a corriger.", "dec", UtcNow);

        var firstActivity = new Dictionary<string, DateOnly> { [unit.Code] = closedDay };

        // freshUnit has NO recorded activity: it must not be flagged at all.
        var result = Build(
            units: [unit, freshUnit],
            closings: [closed, reopened],
            firstActivity: firstActivity);

        var backlogUnit = Assert.Single(result.ClosingBacklog);
        Assert.Equal(unit.Code, backlogUnit.HotelUnitCode);

        // The closed day is fine; the reopened day must be re-closed and re-enters the backlog.
        Assert.Equal([reopenedDay], backlogUnit.MissingDates);
    }

    [Fact]
    public void Payment_order_queue_keeps_draft_orders_only_sorted_by_age()
    {
        var oldDraft = new PaymentOrder(Date.AddDays(-6), "Fournisseur A", 1000m, Date.AddDays(-1), "BNA-01");
        var recentDraft = new PaymentOrder(Date.AddDays(-1), "Fournisseur B", 500m, Date.AddDays(5), "BNA-01");

        var approved = new PaymentOrder(Date.AddDays(-9), "Fournisseur C", 700m, Date, "BNA-01");
        approved.Approve("dec", UtcNow);

        var result = Build(paymentOrders: [oldDraft, recentDraft, approved]);

        Assert.Equal(2, result.PendingPaymentOrderCount);
        Assert.Equal(1500m, result.PendingPaymentOrderAmount);

        Assert.Equal(
            new[] { oldDraft.Id, recentDraft.Id },
            result.PendingPaymentOrders.Select(order => order.Id).ToArray());

        Assert.Equal(6, result.PendingPaymentOrders.First().AgeDays);
        Assert.Equal(1, result.PendingPaymentOrders.Last().AgeDays);
    }

    [Fact]
    public void Unit_health_marks_provisional_submitted_revenue_and_attention_units()
    {
        var yesterday = Date.AddDays(-1);

        var validatedUnit = new HotelUnit("EL-MANAR", "Hotel El Manar", HotelUnitType.Hotel, 1);
        var submittedUnit = new HotelUnit("EL-MARSA", "Hotel El Marsa", HotelUnitType.Hotel, 2);
        var draftUnit = new HotelUnit("EL-RIADH", "Hotel El Riadh", HotelUnitType.Hotel, 3);
        var silentClosedUnit = new HotelUnit("EL-AMANE", "Hotel El Amane", HotelUnitType.Hotel, 4);

        var validated = SubmittedRevenue(validatedUnit.Code, yesterday, 900m, UtcNow.AddHours(-10));
        validated.Validate("dec", UtcNow.AddHours(-9));

        var submitted = SubmittedRevenue(submittedUnit.Code, yesterday, 400m, UtcNow.AddHours(-5));

        var draft = new DailyRevenue(yesterday, draftUnit.Code, 100m, 0m, 0m, 0m);

        // silentClosedUnit has no revenue for yesterday but its day IS closed:
        // no usable figure, yet not an attention case.
        var closedYesterday = new DailyClosing(yesterday, silentClosedUnit.Code, "auditor", UtcNow);

        var result = Build(
            units: [validatedUnit, submittedUnit, draftUnit, silentClosedUnit],
            revenues: [validated, submitted, draft],
            closings: [closedYesterday]);

        Assert.Equal(4, result.UnitHealth.Count);

        var validatedRow = result.UnitHealth.Single(row => row.HotelUnitCode == validatedUnit.Code);
        Assert.Equal(DailyRevenueStatus.Validated, validatedRow.YesterdayRevenueStatus);
        Assert.Equal(900m, validatedRow.YesterdayRevenueTotal);
        Assert.False(validatedRow.YesterdayRevenueIsProvisional);
        Assert.False(validatedRow.NeedsAttention); // a usable (validated) figure exists

        // Submitted: the figure is usable but FLAGGED AS PROVISIONAL.
        var submittedRow = result.UnitHealth.Single(row => row.HotelUnitCode == submittedUnit.Code);
        Assert.Equal(DailyRevenueStatus.Submitted, submittedRow.YesterdayRevenueStatus);
        Assert.Equal(400m, submittedRow.YesterdayRevenueTotal);
        Assert.True(submittedRow.YesterdayRevenueIsProvisional);
        Assert.False(submittedRow.NeedsAttention);

        // Draft: not a usable figure, day not closed -> attention.
        var draftRow = result.UnitHealth.Single(row => row.HotelUnitCode == draftUnit.Code);
        Assert.Equal(DailyRevenueStatus.Draft, draftRow.YesterdayRevenueStatus);
        Assert.Null(draftRow.YesterdayRevenueTotal);
        Assert.True(draftRow.NeedsAttention);

        // No revenue at all but yesterday IS closed -> not an attention case.
        var closedRow = result.UnitHealth.Single(row => row.HotelUnitCode == silentClosedUnit.Code);
        Assert.Null(closedRow.YesterdayRevenueStatus);
        Assert.Null(closedRow.YesterdayRevenueTotal);
        Assert.True(closedRow.YesterdayClosed);
        Assert.False(closedRow.NeedsAttention);
    }

    [Fact]
    public void Unit_health_attention_rule_and_occupancy()
    {
        var yesterday = Date.AddDays(-1);

        var healthyUnit = new HotelUnit("EL-MANAR", "Hotel El Manar", HotelUnitType.Hotel, 1);
        var attentionUnit = new HotelUnit("EL-MARSA", "Hotel El Marsa", HotelUnitType.Hotel, 2);
        var roomlessUnit = new HotelUnit("EL-RIADH", "Hotel El Riadh", HotelUnitType.Hotel, 3);

        var validated = SubmittedRevenue(healthyUnit.Code, yesterday, 900m, UtcNow.AddHours(-10));
        validated.Validate("dec", UtcNow.AddHours(-9));

        // attentionUnit: only a DRAFT entry for yesterday (not a usable figure) and no closing.
        var draft = new DailyRevenue(yesterday, attentionUnit.Code, 100m, 0m, 0m, 0m);

        var roomA = new Room(healthyUnit.Code, "101", "STD");
        var roomB = new Room(healthyUnit.Code, "102", "STD");
        var inactiveRoom = new Room(healthyUnit.Code, "103", "STD");
        inactiveRoom.Deactivate();

        // Covers tonight (arrival today, departure tomorrow): blocks roomA.
        var staying = TestReservations.Create(healthyUnit.Code, roomA.Id, "CUST-01", Date, Date.AddDays(1), 2, 100m, "STD-PLAN", "STD");

        // Cancelled: does not block.
        var cancelled = TestReservations.Create(healthyUnit.Code, roomB.Id, "CUST-02", Date, Date.AddDays(2), 1, 100m, "STD-PLAN", "STD");
        cancelled.Cancel("Annulation client.", "reception", UtcNow);

        // Departs today: the departure night is not part of the stay - does not cover tonight.
        var departing = TestReservations.Create(healthyUnit.Code, roomB.Id, "CUST-03", Date.AddDays(-2), Date, 1, 100m, "STD-PLAN", "STD");

        var result = Build(
            units: [healthyUnit, attentionUnit, roomlessUnit],
            revenues: [validated, draft],
            rooms: [roomA, roomB, inactiveRoom],
            reservations: [staying, cancelled, departing]);

        var healthyRow = result.UnitHealth.Single(row => row.HotelUnitCode == healthyUnit.Code);
        Assert.False(healthyRow.NeedsAttention); // validated figure exists
        Assert.Equal(1, healthyRow.OccupiedRooms);
        Assert.Equal(2, healthyRow.ActiveRooms); // the deactivated room leaves the denominator
        Assert.Equal(50m, healthyRow.OccupancyRatePercent);

        var attentionRow = result.UnitHealth.Single(row => row.HotelUnitCode == attentionUnit.Code);
        Assert.Equal(DailyRevenueStatus.Draft, attentionRow.YesterdayRevenueStatus);
        Assert.Null(attentionRow.YesterdayRevenueTotal);
        Assert.True(attentionRow.NeedsAttention);

        var roomlessRow = result.UnitHealth.Single(row => row.HotelUnitCode == roomlessUnit.Code);
        Assert.Equal(0, roomlessRow.ActiveRooms);
        Assert.Null(roomlessRow.OccupancyRatePercent);
    }

    /// <summary>
    /// The occupancy of a unit must be the SAME figure as the one the lodging module publishes
    /// for that unit and that night (LodgingService.GetOccupancyAsync): distinct rooms blocked -
    /// every room, not only the active ones - over the currently active room count. Nothing
    /// forbids deactivating a room a guest still sleeps in, so restricting the numerator to the
    /// active rooms would make the cockpit read lower than the occupancy screen of the very unit
    /// it describes - a direction figure contradicting its own module.
    /// </summary>
    [Fact]
    public void Occupancy_counts_every_blocked_room_like_the_lodging_module_even_a_deactivated_one()
    {
        var unit = new HotelUnit("EL-MANAR", "Hotel El Manar", HotelUnitType.Hotel, 1);

        var activeRoom = new Room(unit.Code, "101", "STD");
        var deactivatedRoom = new Room(unit.Code, "102", "STD");
        deactivatedRoom.Deactivate();

        var inActiveRoom = TestReservations.Create(unit.Code, activeRoom.Id, "CUST-01", Date, Date.AddDays(1), 2, 100m, "STD-PLAN", "STD");
        var inDeactivatedRoom = TestReservations.Create(unit.Code, deactivatedRoom.Id, "CUST-02", Date, Date.AddDays(1), 1, 100m, "STD-PLAN", "STD");

        var result = Build(
            units: [unit],
            rooms: [activeRoom, deactivatedRoom],
            reservations: [inActiveRoom, inDeactivatedRoom]);

        var row = Assert.Single(result.UnitHealth);

        // Two rooms are really busy tonight; only one of them is still an active room.
        Assert.Equal(2, row.OccupiedRooms);
        Assert.Equal(1, row.ActiveRooms);

        // The consequence is the lodging module's own, and it is meaningful: more guests than
        // active rooms is exactly what a rate above 100 % says.
        Assert.Equal(200m, row.OccupancyRatePercent);
    }
}
