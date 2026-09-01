using RaqmiSystem.Application.Pilotage;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Closing;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Tests;

/// <summary>
/// Pure-calculation coverage of the CEO dashboard. What is proven here is exactly what makes
/// this screen trustworthy: every figure obeys the OWNING module's counting rule, and never a
/// rule invented for the dashboard.
///
/// - revenue counts Validated entries only (a draft, a submitted or a rejected entry is not
///   realised revenue - the budget module's rule);
/// - receipts count Confirmed ones only (the treasury rule);
/// - receivables count Issued invoices only, dated on or before the period's end, aged from the
///   invoice date (the receivables rule - the system holds no due dates);
/// - occupancy counts every non-cancelled / non-no-show stay (Booked, CheckedIn AND CheckedOut),
///   distinct rooms per night, over the active rooms - LodgingService.GetOccupancyAsync's own
///   numerator and denominator;
/// - a business day is closed only by a closing at status Closed - a Reopened day is open again -
///   and only days already past can be reproached for not being closed;
/// - every percentage is null (a dash), never zero, when its reference is zero - the single
///   division-by-zero rule taken from BudgetVarianceCalculator.
///
/// The facts handed to the calculator are deliberately UNFILTERED (drafts, cancelled receipts,
/// paid invoices and cancelled stays are all present): the EF service's SQL status filters are
/// an optimisation, and the rules must hold without them.
/// </summary>
public sealed class GroupDashboardCalculatorTests
{
    private const string ManarCode = "EL-MANAR";
    private const string MarsaCode = "EL-MARSA";
    private const string DjazairCode = "EL-DJAZAIR";

    private static readonly DateOnly From = new(2026, 8, 1);
    private static readonly DateOnly To = new(2026, 8, 3);

    // Two days after the period's end: the three days of the period are all past, so all three
    // can be reproached for not being closed.
    private static readonly DateOnly Today = new(2026, 8, 5);
    private static readonly DateTimeOffset NowUtc = new(2026, 8, 5, 9, 0, 0, TimeSpan.Zero);

    private static readonly Guid RoomOne = Guid.NewGuid();
    private static readonly Guid RoomTwo = Guid.NewGuid();
    private static readonly Guid RoomThree = Guid.NewGuid();
    private static readonly Guid RoomFour = Guid.NewGuid();

    private static readonly IReadOnlyCollection<GroupUnitInfo> Units =
    [
        new GroupUnitInfo(ManarCode, "Hotel El Manar", IsActive: true, ActiveRoomCount: 2),
        new GroupUnitInfo(MarsaCode, "Hotel El Marsa", IsActive: true, ActiveRoomCount: 1),
        new GroupUnitInfo(DjazairCode, "Residence El Djazair", IsActive: false, ActiveRoomCount: 0)
    ];

    private static GroupDashboardResponse Calculate(
        GroupPeriodFacts? current = null,
        GroupPeriodFacts? previous = null,
        IReadOnlyCollection<GroupBudgetMonthTarget>? budgetTargets = null,
        IReadOnlyCollection<GroupClosedDayFact>? closedDays = null,
        IReadOnlyCollection<GroupUnitInfo>? units = null,
        DateOnly? from = null,
        DateOnly? to = null,
        DateOnly? today = null,
        DateTimeOffset? nowUtc = null)
    {
        return new GroupDashboardCalculator().Calculate(
            from ?? From,
            to ?? To,
            today ?? Today,
            nowUtc ?? NowUtc,
            units ?? Units,
            current ?? GroupPeriodFacts.Empty,
            previous ?? GroupPeriodFacts.Empty,
            budgetTargets ?? [],
            closedDays ?? []);
    }

    /// <summary>
    /// The period's facts, with every status the calculator must reject present alongside the
    /// ones it must count.
    /// </summary>
    private static GroupPeriodFacts CurrentFacts()
    {
        return new GroupPeriodFacts(
            [
                new GroupRevenueFact(ManarCode, From, 1000m, DailyRevenueStatus.Validated, null),
                // Submitted 72 hours ago: excluded from the revenue, and past the 48-hour wait.
                new GroupRevenueFact(ManarCode, From.AddDays(1), 500m, DailyRevenueStatus.Submitted, NowUtc.AddHours(-72)),
                new GroupRevenueFact(ManarCode, To, 300m, DailyRevenueStatus.Draft, null),
                new GroupRevenueFact(MarsaCode, From, 400m, DailyRevenueStatus.Validated, null),
                new GroupRevenueFact(MarsaCode, From.AddDays(1), 200m, DailyRevenueStatus.Rejected, null)
            ],
            [
                new GroupReceiptFact(ManarCode, 700m, ReceiptStatus.Confirmed),
                new GroupReceiptFact(ManarCode, 100m, ReceiptStatus.Draft),
                new GroupReceiptFact(MarsaCode, 250m, ReceiptStatus.Confirmed),
                new GroupReceiptFact(MarsaCode, 90m, ReceiptStatus.Cancelled)
            ],
            [
                // 33 days old at the period's end: outstanding, but not yet in an alerting bracket.
                new GroupInvoiceFact(ManarCode, new DateOnly(2026, 7, 1), 1200m, InvoiceStatus.Issued),
                // 94 days old: bracket "over 90".
                new GroupInvoiceFact(ManarCode, new DateOnly(2026, 5, 1), 800m, InvoiceStatus.Issued),
                // Already paid: not owed any more.
                new GroupInvoiceFact(MarsaCode, new DateOnly(2026, 8, 2), 300m, InvoiceStatus.Paid),
                // 63 days old: bracket "61-90".
                new GroupInvoiceFact(MarsaCode, new DateOnly(2026, 6, 1), 500m, InvoiceStatus.Issued)
            ],
            [
                // Booked, nights of 1 and 2 August (departure night excluded).
                new GroupStayFact(ManarCode, RoomOne, From, To, ReservationStatus.Confirmed),
                // CheckedOut still blocks: nights of 2 and 3 August.
                new GroupStayFact(ManarCode, RoomTwo, From.AddDays(1), To.AddDays(1), ReservationStatus.CheckedOut),
                new GroupStayFact(ManarCode, RoomOne, To, To.AddDays(2), ReservationStatus.Cancelled),
                new GroupStayFact(MarsaCode, RoomThree, From, From.AddDays(1), ReservationStatus.NoShow),
                new GroupStayFact(MarsaCode, RoomFour, To, To.AddDays(3), ReservationStatus.CheckedIn)
            ]);
    }

    private static GroupPeriodFacts PreviousFacts()
    {
        return new GroupPeriodFacts(
            [new GroupRevenueFact(ManarCode, new DateOnly(2025, 8, 1), 700m, DailyRevenueStatus.Validated, null)],
            [new GroupReceiptFact(ManarCode, 500m, ReceiptStatus.Confirmed)],
            [new GroupInvoiceFact(ManarCode, new DateOnly(2025, 7, 1), 1000m, InvoiceStatus.Issued)],
            []);
    }

    [Fact]
    public void Group_kpis_count_only_validated_revenue_confirmed_receipts_and_issued_invoices()
    {
        var result = Calculate(CurrentFacts());

        // 1000 + 400. The submitted 500, the draft 300 and the rejected 200 are not realised.
        Assert.Equal(1400m, result.Kpis.ValidatedRevenue);

        // 700 + 250. The draft 100 and the cancelled 90 are not money in.
        Assert.Equal(950m, result.Kpis.ConfirmedReceipts);

        // 1200 + 800 + 500. The paid invoice is no longer owed.
        Assert.Equal(2500m, result.Kpis.OutstandingReceivables);
        Assert.Equal(3, result.Kpis.OutstandingInvoiceCount);

        // Only the currently active units are counted; the deactivated one is still listed in
        // the table (its facts must never vanish) but is not an active unit.
        Assert.Equal(2, result.Kpis.ActiveUnitCount);
    }

    [Fact]
    public void Group_occupancy_counts_every_blocking_stay_over_the_active_rooms()
    {
        var result = Calculate(CurrentFacts());

        // El Manar: room 1 on the 1st and 2nd, room 2 on the 2nd and 3rd = 4 nights.
        // El Marsa: room 4 on the 3rd = 1 night. The cancelled and no-show stays block nothing.
        Assert.Equal(5, result.Kpis.OccupiedNights);

        // (2 + 1 + 0) active rooms x 3 days.
        Assert.Equal(9, result.Kpis.AvailableNights);
        Assert.Equal(55.56m, result.Kpis.OccupancyRatePercent);
    }

    [Fact]
    public void An_invoice_dated_after_the_period_end_is_not_outstanding_yet()
    {
        var facts = new GroupPeriodFacts(
            [],
            [],
            [
                new GroupInvoiceFact(ManarCode, To, 100m, InvoiceStatus.Issued),
                new GroupInvoiceFact(ManarCode, To.AddDays(1), 999m, InvoiceStatus.Issued)
            ],
            []);

        var result = Calculate(facts);

        Assert.Equal(100m, result.Kpis.OutstandingReceivables);
        Assert.Equal(1, result.Kpis.OutstandingInvoiceCount);
    }

    [Fact]
    public void Year_over_year_variations_are_relative_to_the_previous_period()
    {
        var result = Calculate(CurrentFacts(), PreviousFacts());

        Assert.Equal(new DateOnly(2025, 8, 1), result.PreviousFrom);
        Assert.Equal(new DateOnly(2025, 8, 3), result.PreviousTo);

        Assert.Equal(700m, result.PreviousKpis.ValidatedRevenue);
        Assert.Equal(500m, result.PreviousKpis.ConfirmedReceipts);
        Assert.Equal(1000m, result.PreviousKpis.OutstandingReceivables);

        Assert.Equal(100.00m, result.Variations.RevenuePercent);
        Assert.Equal(90.00m, result.Variations.ReceiptsPercent);
        Assert.Equal(150.00m, result.Variations.ReceivablesPercent);

        // Nothing was occupied a year earlier: a variation relative to zero does not exist.
        Assert.Equal(0m, result.PreviousKpis.OccupancyRatePercent);
        Assert.Null(result.Variations.OccupancyPercent);
    }

    [Fact]
    public void A_variation_against_an_empty_previous_period_is_null_not_zero()
    {
        var result = Calculate(CurrentFacts(), GroupPeriodFacts.Empty);

        Assert.Null(result.Variations.RevenuePercent);
        Assert.Null(result.Variations.ReceiptsPercent);
        Assert.Null(result.Variations.ReceivablesPercent);
        Assert.Null(result.Variations.OccupancyPercent);
    }

    [Fact]
    public void Occupancy_variation_is_computed_when_the_previous_rate_is_not_zero()
    {
        var previous = new GroupPeriodFacts(
            [],
            [],
            [],
            // One room busy for the three nights a year earlier: 3 / 9 = 33.33 %.
            [new GroupStayFact(ManarCode, RoomOne, new DateOnly(2025, 8, 1), new DateOnly(2025, 8, 4), ReservationStatus.CheckedOut)]);

        var result = Calculate(CurrentFacts(), previous);

        Assert.Equal(33.33m, result.PreviousKpis.OccupancyRatePercent);

        // (55.56 - 33.33) / 33.33 = 66.6966...
        Assert.Equal(66.70m, result.Variations.OccupancyPercent);
    }

    [Fact]
    public void The_previous_period_is_the_same_window_one_year_earlier_and_clamps_29_february()
    {
        var (previousFrom, previousTo) = GroupDashboardCalculator.PreviousPeriod(
            new DateOnly(2024, 2, 29),
            new DateOnly(2024, 3, 31));

        Assert.Equal(new DateOnly(2023, 2, 28), previousFrom);
        Assert.Equal(new DateOnly(2023, 3, 31), previousTo);
    }

    [Fact]
    public void Units_are_ranked_by_validated_revenue_with_their_share_of_the_group()
    {
        var result = Calculate(CurrentFacts());

        Assert.Equal([ManarCode, MarsaCode, DjazairCode], result.Units.Select(unit => unit.HotelUnitCode));

        var manar = result.Units.First();
        Assert.Equal(1000m, manar.ValidatedRevenue);
        Assert.Equal(700m, manar.ConfirmedReceipts);
        Assert.Equal(71.43m, manar.GroupSharePercent);
        Assert.Equal(4, manar.OccupiedNights);
        Assert.Equal(6, manar.AvailableNights);
        Assert.Equal(66.67m, manar.OccupancyRatePercent);

        var marsa = result.Units.ElementAt(1);
        Assert.Equal(400m, marsa.ValidatedRevenue);
        Assert.Equal(28.57m, marsa.GroupSharePercent);
        Assert.Equal(33.33m, marsa.OccupancyRatePercent);

        // A unit without an active room has no occupancy rate at all - a rate against no
        // capacity does not exist, and zero would read as "empty".
        var djazair = result.Units.Last();
        Assert.False(djazair.IsActive);
        Assert.Equal(0, djazair.AvailableNights);
        Assert.Null(djazair.OccupancyRatePercent);
    }

    [Fact]
    public void The_group_share_is_null_when_the_group_produced_nothing()
    {
        var result = Calculate(GroupPeriodFacts.Empty);

        Assert.All(result.Units, unit => Assert.Null(unit.GroupSharePercent));
    }

    [Fact]
    public void Only_a_closing_at_status_closed_closes_a_past_day()
    {
        var closedDays = new GroupClosedDayFact[]
        {
            new(ManarCode, From, ClosingStatus.Closed),
            // Reopened: the day is open again and must be closed a second time.
            new(ManarCode, From.AddDays(1), ClosingStatus.Reopened),
            new(ManarCode, To, ClosingStatus.Closed)
        };

        var result = Calculate(CurrentFacts(), closedDays: closedDays);

        Assert.Equal(1, result.Units.Single(unit => unit.HotelUnitCode == ManarCode).UnclosedDayCount);

        // Nothing closed at all for the other units: all three past days are late.
        Assert.Equal(3, result.Units.Single(unit => unit.HotelUnitCode == MarsaCode).UnclosedDayCount);
    }

    [Fact]
    public void The_running_day_and_the_future_are_never_counted_as_unclosed()
    {
        // Today is the second day of the period: only the first day is already past.
        var runningPeriod = Calculate(GroupPeriodFacts.Empty, today: From.AddDays(1));
        Assert.Equal(1, runningPeriod.Units.Single(unit => unit.HotelUnitCode == ManarCode).UnclosedDayCount);

        // A period entirely in the future cannot owe a single closing.
        var futurePeriod = Calculate(GroupPeriodFacts.Empty, today: From.AddDays(-1));
        Assert.All(futurePeriod.Units, unit => Assert.Equal(0, unit.UnclosedDayCount));
    }

    [Fact]
    public void Budget_columns_sum_the_monthly_targets_of_the_months_the_period_touches()
    {
        var targets = new GroupBudgetMonthTarget[]
        {
            // August is the only month the period touches: 600 + 300 = 900 (two category lines).
            new(ManarCode, 2026, 8, 600m),
            new(ManarCode, 2026, 8, 300m),
            new(ManarCode, 2026, 9, 5000m)
        };

        var result = Calculate(CurrentFacts(), budgetTargets: targets);
        var manar = result.Units.Single(unit => unit.HotelUnitCode == ManarCode);

        Assert.Equal(900m, manar.BudgetTarget);
        Assert.Equal(100m, manar.BudgetVarianceAmount);
        Assert.Equal(11.11m, manar.BudgetVariancePercent);
    }

    [Fact]
    public void A_period_spanning_two_years_takes_the_months_of_each_year_it_touches()
    {
        var targets = new GroupBudgetMonthTarget[]
        {
            new(ManarCode, 2025, 11, 100m),  // before the window
            new(ManarCode, 2025, 12, 200m),  // inside
            new(ManarCode, 2026, 1, 300m),   // inside
            new(ManarCode, 2026, 2, 400m)    // after the window
        };

        var result = Calculate(
            GroupPeriodFacts.Empty,
            budgetTargets: targets,
            from: new DateOnly(2025, 12, 10),
            to: new DateOnly(2026, 1, 20),
            today: new DateOnly(2026, 1, 25));

        Assert.Equal(500m, result.Units.Single(unit => unit.HotelUnitCode == ManarCode).BudgetTarget);
    }

    [Fact]
    public void A_unit_without_a_frozen_budget_plan_shows_no_target_rather_than_a_target_of_zero()
    {
        var result = Calculate(CurrentFacts(), budgetTargets: [new GroupBudgetMonthTarget(ManarCode, 2026, 8, 900m)]);
        var marsa = result.Units.Single(unit => unit.HotelUnitCode == MarsaCode);

        Assert.Null(marsa.BudgetTarget);
        Assert.Null(marsa.BudgetVarianceAmount);
        Assert.Null(marsa.BudgetVariancePercent);
    }

    [Fact]
    public void Alerts_are_factual_and_reuse_the_owning_modules_thresholds()
    {
        var closedDays = new GroupClosedDayFact[]
        {
            new(ManarCode, From, ClosingStatus.Closed),
            new(ManarCode, From.AddDays(1), ClosingStatus.Closed),
            new(ManarCode, To, ClosingStatus.Closed),
            new(MarsaCode, From, ClosingStatus.Closed),
            new(MarsaCode, From.AddDays(1), ClosingStatus.Closed),
            new(MarsaCode, To, ClosingStatus.Closed),
            new(DjazairCode, From, ClosingStatus.Closed),
            new(DjazairCode, From.AddDays(1), ClosingStatus.Closed),
            new(DjazairCode, To, ClosingStatus.Closed)
        };

        var result = Calculate(CurrentFacts(), closedDays: closedDays);

        // Attention first, then by type, then by unit code - a deterministic reading order.
        Assert.Equal(
            [GroupAlertType.OverdueInvoices, GroupAlertType.OverdueInvoices, GroupAlertType.PendingValidation],
            result.Alerts.Select(alert => alert.Type));

        var overdueManar = result.Alerts.First();
        Assert.Equal(GroupAlertSeverity.Attention, overdueManar.Severity);
        Assert.Equal(ManarCode, overdueManar.HotelUnitCode);
        Assert.Equal("Hotel El Manar", overdueManar.HotelUnitName);

        // Only the invoice over 90 days is in an alerting bracket; the 33-day-old one is not.
        Assert.Equal(1, overdueManar.Count);
        Assert.Equal(1, result.Alerts.ElementAt(1).Count);

        var pending = result.Alerts.Last();
        Assert.Equal(GroupAlertSeverity.Info, pending.Severity);
        Assert.Equal(ManarCode, pending.HotelUnitCode);
        Assert.Equal(1, pending.Count);
        Assert.False(string.IsNullOrWhiteSpace(pending.Rule));
    }

    [Fact]
    public void An_unclosed_day_raises_one_attention_alert_per_unit()
    {
        var result = Calculate(GroupPeriodFacts.Empty);

        var unclosed = result.Alerts.Where(alert => alert.Type == GroupAlertType.UnclosedDays).ToArray();

        Assert.Equal(3, unclosed.Length);
        Assert.All(unclosed, alert => Assert.Equal(GroupAlertSeverity.Attention, alert.Severity));
        Assert.All(unclosed, alert => Assert.Equal(3, alert.Count));

        // Attention outranks Info, and the unit codes are ordinal-ordered inside a type.
        Assert.Equal([DjazairCode, ManarCode, MarsaCode], unclosed.Select(alert => alert.HotelUnitCode));
    }

    [Fact]
    public void The_48_hour_pending_validation_wait_is_strict()
    {
        GroupPeriodFacts Submitted(DateTimeOffset submittedAt) => new(
            [new GroupRevenueFact(ManarCode, From, 100m, DailyRevenueStatus.Submitted, submittedAt)],
            [],
            [],
            []);

        // Exactly 48 hours: still inside the wait, nothing to report yet.
        var atTheBoundary = Calculate(Submitted(NowUtc.AddHours(-48)));
        Assert.DoesNotContain(atTheBoundary.Alerts, alert => alert.Type == GroupAlertType.PendingValidation);

        // One second more: the wait is over.
        var pastTheBoundary = Calculate(Submitted(NowUtc.AddHours(-48).AddSeconds(-1)));
        Assert.Contains(pastTheBoundary.Alerts, alert => alert.Type == GroupAlertType.PendingValidation);
    }

    [Fact]
    public void The_overdue_invoice_alert_starts_at_the_aging_modules_61_day_bracket()
    {
        GroupPeriodFacts Issued(int ageInDays) => new(
            [],
            [],
            [new GroupInvoiceFact(ManarCode, To.AddDays(-ageInDays), 100m, InvoiceStatus.Issued)],
            []);

        // 60 days: bracket "31-60", still a normal receivable.
        Assert.DoesNotContain(
            Calculate(Issued(60)).Alerts,
            alert => alert.Type == GroupAlertType.OverdueInvoices);

        // 61 days: bracket "61-90", the first bracket the aging module treats as a risk.
        Assert.Contains(
            Calculate(Issued(61)).Alerts,
            alert => alert.Type == GroupAlertType.OverdueInvoices);
    }

    [Fact]
    public void The_payload_carries_the_basis_of_every_family_of_figures()
    {
        var basis = Calculate().Basis;

        Assert.All(
            new[] { basis.Revenue, basis.Receipts, basis.Receivables, basis.Occupancy, basis.Closing },
            sentence => Assert.False(string.IsNullOrWhiteSpace(sentence)));
    }

    [Fact]
    public void An_inverted_period_is_refused()
    {
        Assert.Throws<ArgumentException>(() => Calculate(from: To, to: From));
    }
}
