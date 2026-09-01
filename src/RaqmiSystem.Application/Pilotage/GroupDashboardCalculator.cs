using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Closing;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Receivables;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// Builds the CEO dashboard from raw facts. Pure in-memory combination (no database access) so
/// every figure the direction reads can be unit tested independently of the EF-backed service,
/// following the same shape as <c>UnitDashboardCalculator</c> and
/// <c>BudgetVarianceCalculator</c>: the caller fetches the facts, this class only computes.
///
/// THE COUNTING RULES - each one is the owning module's established rule, applied HERE so the
/// dashboard can never show the direction a figure that deviates from the module it summarizes:
/// - REVENUE: only DailyRevenue at status Validated counts as actual (the budget module's rule -
///   a Draft is an uncontrolled keystroke, a Submitted entry is awaiting control, a Rejected one
///   was refused; see BudgetVarianceCalculator's header).
/// - RECEIPTS: only CashReceipt at status Confirmed counts as money in (the treasury rule).
/// - RECEIVABLES: only invoices at status Issued, dated on or before the period's end, are
///   outstanding (the receivables rule); their age runs from the INVOICE date, because the
///   system holds no due dates (see Domain.Receivables.AgingCalculator).
/// - OCCUPANCY: a night is occupied when a stay that is neither Cancelled nor NoShow covers it -
///   Booked, CheckedIn AND CheckedOut all count (Reservation.IsBlocking; excluding CheckedOut
///   would retroactively empty history). Rooms are counted distinct per night, like
///   LodgingService.GetOccupancyAsync. Available nights = active rooms x days of the period.
/// - CLOSING: a business day is closed only by a DailyClosing at status Closed for that
///   (date, unit); a Reopened day is open again. Only days already PAST (strictly before today)
///   can be reproached for not being closed - the running day is still being operated.
///
/// DIVISION BY ZERO - one rule for every percentage of this dashboard, taken verbatim from
/// BudgetVarianceCalculator.Percentage: when the denominator is zero the percentage is null
/// (displayed as a dash), never 0 ("on target"/"no change") and never an invented large number.
/// This covers the N/N-1 variations against an empty previous year, the group share of a group
/// that produced nothing, and the occupancy of a unit without rooms.
///
/// BUDGET PRORATA - deliberately simple and stated: the target a unit is measured against over
/// [from, to] is the SUM OF THE MONTHLY TARGETS of every calendar month the period touches, a
/// partially covered month counting in full. Budget targets are monthly by design (BudgetLine);
/// slicing them by day would invent a daily seasonality nobody budgeted. Only approved (or
/// closed) plans participate; a unit without one shows null budget columns, never a zero target
/// (same reasoning as BudgetService.GetVarianceAsync answering NotFound).
///
/// N/N-1 - the previous period is the same calendar window one year earlier
/// (<see cref="PreviousPeriod"/>). The previous receivables figure is measured with today's
/// invoice statuses at the earlier cutoff (no as-of reconstruction of payment history exists in
/// the system), and the previous active-unit count cannot be computed at all (no activation
/// history) - see GroupKpiVariations.
/// </summary>
public sealed class GroupDashboardCalculator
{
    private const int PendingValidationAgeHours = 48;
    private const int OverdueInvoiceAgeDays = 60;

    private const string UnclosedDaysRule =
        "Business days of the period already past (strictly before today) without a daily closing " +
        "at status Closed for this unit; a Reopened day counts as not closed until closed again.";

    private const string PendingValidationRule =
        "Daily revenue entries at status Submitted for more than 48 hours; submitted revenue is " +
        "not counted as actual until it is validated.";

    private const string OverdueInvoicesRule =
        "Invoices at status Issued whose age at the period's end falls in the aging module's " +
        "61-90 days or over-90-days brackets (more than 60 days after the invoice date; the " +
        "system holds no due dates, so age runs from the invoice date).";

    /// <summary>
    /// The single definition of "the equivalent period one year earlier", shared by the service
    /// (which fetches the previous facts) and this calculator (which reports the bounds).
    /// DateOnly.AddYears clamps 29 February to 28 February on non-leap years.
    /// </summary>
    public static (DateOnly From, DateOnly To) PreviousPeriod(DateOnly from, DateOnly to)
    {
        return (from.AddYears(-1), to.AddYears(-1));
    }

    public GroupDashboardResponse Calculate(
        DateOnly from,
        DateOnly to,
        DateOnly today,
        DateTimeOffset nowUtc,
        IReadOnlyCollection<GroupUnitInfo> units,
        GroupPeriodFacts currentFacts,
        GroupPeriodFacts previousFacts,
        IReadOnlyCollection<GroupBudgetMonthTarget> budgetTargets,
        IReadOnlyCollection<GroupClosedDayFact> closedDays)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(currentFacts);
        ArgumentNullException.ThrowIfNull(previousFacts);
        ArgumentNullException.ThrowIfNull(budgetTargets);
        ArgumentNullException.ThrowIfNull(closedDays);

        if (to < from)
        {
            throw new ArgumentException("The from date cannot be after the to date.", nameof(to));
        }

        var (previousFrom, previousTo) = PreviousPeriod(from, to);
        var activeUnitCount = units.Count(unit => unit.IsActive);

        var kpis = BuildKpis(from, to, to, units, currentFacts, activeUnitCount);

        // The previous unit-count KPI reuses the CURRENT roster: HotelUnit keeps no activation
        // history, so this is the only honest value available - and GroupKpiVariations therefore
        // carries no variation for it.
        var previousKpis = BuildKpis(previousFrom, previousTo, previousTo, units, previousFacts, activeUnitCount);

        var variations = new GroupKpiVariations(
            Percentage(previousKpis.ValidatedRevenue, kpis.ValidatedRevenue - previousKpis.ValidatedRevenue),
            Percentage(previousKpis.ConfirmedReceipts, kpis.ConfirmedReceipts - previousKpis.ConfirmedReceipts),
            Percentage(previousKpis.OutstandingReceivables, kpis.OutstandingReceivables - previousKpis.OutstandingReceivables),
            previousKpis.OccupancyRatePercent is null or 0m || kpis.OccupancyRatePercent is null
                ? null
                : Percentage(previousKpis.OccupancyRatePercent.Value, kpis.OccupancyRatePercent.Value - previousKpis.OccupancyRatePercent.Value));

        var rows = BuildUnitRows(from, to, today, units, currentFacts, budgetTargets, closedDays, kpis.ValidatedRevenue);
        var alerts = BuildAlerts(to, nowUtc, units, currentFacts, rows);

        return new GroupDashboardResponse(
            from,
            to,
            previousFrom,
            previousTo,
            kpis,
            previousKpis,
            variations,
            rows,
            alerts,
            new GroupDashboardBasis(
                "Only daily revenue entries at status Validated are counted (draft, submitted and rejected entries are not actual revenue).",
                "Only cash receipts at status Confirmed are counted (draft and cancelled receipts are not money in).",
                "Only invoices at status Issued, dated on or before the period's end, are outstanding; the previous-period figure is measured with today's statuses at the earlier cutoff.",
                "A night is occupied when a non-cancelled, non-no-show stay covers it (Booked, CheckedIn and CheckedOut all count); available nights = currently active rooms x days of the period.",
                "A business day is closed only by a daily closing at status Closed; a reopened day is open again. Only days strictly before today are counted as unclosed."));
    }

    private static GroupKpiSet BuildKpis(
        DateOnly from,
        DateOnly to,
        DateOnly receivablesAsOf,
        IReadOnlyCollection<GroupUnitInfo> units,
        GroupPeriodFacts facts,
        int activeUnitCount)
    {
        var validatedRevenue = Round(facts.Revenues
            .Where(revenue => revenue.Status == DailyRevenueStatus.Validated)
            .Sum(revenue => revenue.Amount));

        var confirmedReceipts = Round(facts.Receipts
            .Where(receipt => receipt.Status == ReceiptStatus.Confirmed)
            .Sum(receipt => receipt.Amount));

        var outstanding = facts.Invoices
            .Where(invoice => invoice.Status == InvoiceStatus.Issued && invoice.InvoiceDate <= receivablesAsOf)
            .ToArray();

        var dayCount = to.DayNumber - from.DayNumber + 1;
        var availableNights = units.Sum(unit => unit.ActiveRoomCount) * dayCount;
        var occupiedNights = units.Sum(unit => CountOccupiedNights(from, to, unit.Code, facts.Stays));

        return new GroupKpiSet(
            validatedRevenue,
            confirmedReceipts,
            Round(outstanding.Sum(invoice => invoice.TotalInclVat)),
            outstanding.Length,
            occupiedNights,
            availableNights,
            RatePercent(occupiedNights, availableNights),
            activeUnitCount);
    }

    private IReadOnlyCollection<GroupUnitRow> BuildUnitRows(
        DateOnly from,
        DateOnly to,
        DateOnly today,
        IReadOnlyCollection<GroupUnitInfo> units,
        GroupPeriodFacts facts,
        IReadOnlyCollection<GroupBudgetMonthTarget> budgetTargets,
        IReadOnlyCollection<GroupClosedDayFact> closedDays,
        decimal groupValidatedRevenue)
    {
        var dayCount = to.DayNumber - from.DayNumber + 1;

        var closedByUnit = closedDays
            .Where(closing => closing.Status == ClosingStatus.Closed)
            .GroupBy(closing => closing.HotelUnitCode)
            .ToDictionary(group => group.Key, group => group.Select(closing => closing.BusinessDate).ToHashSet());

        return units
            .Select(unit =>
            {
                var validatedRevenue = Round(facts.Revenues
                    .Where(revenue => revenue.HotelUnitCode == unit.Code
                        && revenue.Status == DailyRevenueStatus.Validated)
                    .Sum(revenue => revenue.Amount));

                var confirmedReceipts = Round(facts.Receipts
                    .Where(receipt => receipt.HotelUnitCode == unit.Code
                        && receipt.Status == ReceiptStatus.Confirmed)
                    .Sum(receipt => receipt.Amount));

                var occupiedNights = CountOccupiedNights(from, to, unit.Code, facts.Stays);
                var availableNights = unit.ActiveRoomCount * dayCount;

                var (budgetTarget, varianceAmount, variancePercent) =
                    BuildBudgetColumns(from, to, unit.Code, budgetTargets, validatedRevenue);

                return new GroupUnitRow(
                    unit.Code,
                    unit.Name,
                    unit.IsActive,
                    validatedRevenue,
                    Percentage(groupValidatedRevenue, validatedRevenue),
                    confirmedReceipts,
                    occupiedNights,
                    availableNights,
                    RatePercent(occupiedNights, availableNights),
                    CountUnclosedDays(from, to, today, closedByUnit.GetValueOrDefault(unit.Code)),
                    budgetTarget,
                    varianceAmount,
                    variancePercent);
            })
            // The direction's ranking: by validated revenue, descending; the code is a stable,
            // deterministic tie-break.
            .OrderByDescending(row => row.ValidatedRevenue)
            .ThenBy(row => row.HotelUnitCode, StringComparer.Ordinal)
            .ToArray();
    }

    private IReadOnlyCollection<GroupAlert> BuildAlerts(
        DateOnly to,
        DateTimeOffset nowUtc,
        IReadOnlyCollection<GroupUnitInfo> units,
        GroupPeriodFacts facts,
        IReadOnlyCollection<GroupUnitRow> rows)
    {
        var namesByCode = units.ToDictionary(unit => unit.Code, unit => unit.Name);
        var alerts = new List<GroupAlert>();

        // Unclosed days: the closing module makes locking each business day an obligation, so a
        // hole in that lock is a control gap - Attention.
        foreach (var row in rows.Where(row => row.UnclosedDayCount > 0))
        {
            alerts.Add(new GroupAlert(
                GroupAlertType.UnclosedDays,
                row.HotelUnitCode,
                row.HotelUnitName,
                row.UnclosedDayCount,
                GroupAlertSeverity.Attention,
                UnclosedDaysRule));
        }

        // Revenue awaiting validation for more than 48 hours: the workflow is still running, the
        // figure is merely late rather than wrong - Info. "More than 48 hours" is strict: an
        // entry submitted exactly 48 hours ago is not yet flagged.
        var pendingThreshold = nowUtc.AddHours(-PendingValidationAgeHours);

        var pendingByUnit = facts.Revenues
            .Where(revenue => revenue.Status == DailyRevenueStatus.Submitted
                && revenue.SubmittedAt is not null
                && revenue.SubmittedAt.Value < pendingThreshold)
            .GroupBy(revenue => revenue.HotelUnitCode);

        foreach (var group in pendingByUnit)
        {
            alerts.Add(new GroupAlert(
                GroupAlertType.PendingValidation,
                group.Key,
                namesByCode.GetValueOrDefault(group.Key, group.Key),
                group.Count(),
                GroupAlertSeverity.Info,
                PendingValidationRule));
        }

        // Issued invoices past 60 days: the aging module's own 61-90 / 90+ brackets, i.e. the
        // brackets where a receivable is already a risk - Attention. The bracket boundary comes
        // from Domain.Receivables.AgingCalculator, never re-invented here.
        var overdueByUnit = facts.Invoices
            .Where(invoice => invoice.Status == InvoiceStatus.Issued
                && invoice.InvoiceDate <= to
                && AgingCalculator.ClassifyAge(AgingCalculator.AgeInDays(invoice.InvoiceDate, to))
                    is AgingBucket.Days61To90 or AgingBucket.Over90)
            .GroupBy(invoice => invoice.HotelUnitCode);

        foreach (var group in overdueByUnit)
        {
            alerts.Add(new GroupAlert(
                GroupAlertType.OverdueInvoices,
                group.Key,
                namesByCode.GetValueOrDefault(group.Key, group.Key),
                group.Count(),
                GroupAlertSeverity.Attention,
                OverdueInvoicesRule));
        }

        return alerts
            .OrderByDescending(alert => alert.Severity)
            .ThenBy(alert => alert.Type)
            .ThenBy(alert => alert.HotelUnitCode, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Occupied nights of one unit over [from, to]: for each day, the DISTINCT rooms covered by
    /// a blocking stay that night - the same day-by-day, distinct-room counting as
    /// LodgingService.GetOccupancyAsync, kept even though the anti-double-booking invariant
    /// should make duplicates impossible (data predating the invariant must not inflate the
    /// figure).
    /// </summary>
    private static int CountOccupiedNights(
        DateOnly from,
        DateOnly to,
        string hotelUnitCode,
        IReadOnlyCollection<GroupStayFact> stays)
    {
        var blockingStays = stays
            .Where(stay => stay.HotelUnitCode == hotelUnitCode
                && stay.Status is not (ReservationStatus.Cancelled or ReservationStatus.NoShow))
            .ToArray();

        if (blockingStays.Length == 0)
        {
            return 0;
        }

        var occupiedNights = 0;

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var night = day;

            var covering = blockingStays
                .Where(stay => stay.ArrivalDate <= night && night < stay.DepartureDate)
                .ToArray();

            // Les chambres AFFECTEES ne comptent qu'une fois ; les sejours vendus par type et pas
            // encore affectes comptent tout de meme, un par un. Ils consomment bien une chambre,
            // ils n'ont simplement pas encore de numero - les ignorer ferait lire comme libre un
            // inventaire deja vendu.
            occupiedNights += covering
                .Where(stay => stay.RoomId is not null)
                .Select(stay => stay.RoomId!.Value)
                .Distinct()
                .Count()
                + covering.Count(stay => stay.RoomId is null);
        }

        return occupiedNights;
    }

    /// <summary>
    /// Days of [from, to] that are already past (strictly before <paramref name="today"/>, the
    /// UTC business day - the codebase's convention for every UtcNow-based decision) and have no
    /// closing at status Closed. The bounds are exact: the running day is never counted, and a
    /// period entirely in the future has zero unclosed days.
    /// </summary>
    private static int CountUnclosedDays(
        DateOnly from,
        DateOnly to,
        DateOnly today,
        HashSet<DateOnly>? closedDates)
    {
        var lastExpectedDay = to < today ? to : today.AddDays(-1);

        if (lastExpectedDay < from)
        {
            return 0;
        }

        var unclosed = 0;

        for (var day = from; day <= lastExpectedDay; day = day.AddDays(1))
        {
            if (closedDates is null || !closedDates.Contains(day))
            {
                unclosed++;
            }
        }

        return unclosed;
    }

    /// <summary>
    /// Budget columns of one unit: null across the board when no frozen plan covers the period
    /// (no target row exists for the unit on any year the period touches); otherwise the target
    /// is the sum of the monthly targets of the months the period touches (see the class header
    /// for the deliberately month-grained prorata).
    /// </summary>
    private static (decimal? Target, decimal? VarianceAmount, decimal? VariancePercent) BuildBudgetColumns(
        DateOnly from,
        DateOnly to,
        string hotelUnitCode,
        IReadOnlyCollection<GroupBudgetMonthTarget> budgetTargets,
        decimal validatedRevenue)
    {
        var unitTargets = budgetTargets
            .Where(target => target.HotelUnitCode == hotelUnitCode
                && target.Year >= from.Year
                && target.Year <= to.Year)
            .ToArray();

        if (unitTargets.Length == 0)
        {
            return (null, null, null);
        }

        var target = 0m;

        foreach (var monthTarget in unitTargets)
        {
            var firstMonth = monthTarget.Year == from.Year ? from.Month : 1;
            var lastMonth = monthTarget.Year == to.Year ? to.Month : 12;

            if (monthTarget.Month >= firstMonth && monthTarget.Month <= lastMonth)
            {
                target += monthTarget.AmountTarget;
            }
        }

        target = Round(target);
        var variance = validatedRevenue - target;

        return (target, variance, Percentage(target, variance));
    }

    private static decimal? RatePercent(int numerator, int denominator)
    {
        if (denominator == 0)
        {
            // A rate against no capacity does not exist (see the class header's single
            // division-by-zero rule); the consumer displays a dash.
            return null;
        }

        return Math.Round(numerator * 100m / denominator, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The relative gap in percent, rounded to two decimals - null when the reference is zero.
    /// Taken verbatim from BudgetVarianceCalculator.Percentage; see that method's comment for
    /// the full reasoning behind the null.
    /// </summary>
    private static decimal? Percentage(decimal reference, decimal difference)
    {
        if (reference == 0m)
        {
            return null;
        }

        return Math.Round(difference / reference * 100m, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal Round(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
