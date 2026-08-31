using RaqmiSystem.Domain.Closing;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// Builds the DEC cockpit from in-memory data. Pure combination (no database access) so every
/// business rule below can be unit tested independently of the EF-backed service, following the
/// shape of <c>UnitDashboardCalculator</c> and <c>BudgetVarianceCalculator</c>: the caller
/// fetches, this class only computes.
///
/// RULES THIS CLASS RE-APPLIES DEFENSIVELY (they are the modules' established rules, and a
/// direction figure that deviates from them is a serious defect):
/// - pending validation = daily revenue at the SUBMITTED status only;
/// - rejected awaiting correction = daily revenue at the REJECTED status only. Verified
///   mechanic in DailyRevenue: Reject keeps Status == Rejected together with RejectionReason,
///   and it is UpdateAmounts (the correction itself) that returns the entry to Draft while
///   CLEARING the reason - so "Draft + RejectionReason" never exists and must not be looked for;
/// - a business day is closed only when a DailyClosing row exists at the Closed status for
///   (date, unit); a Reopened day is NOT closed and re-enters the backlog;
/// - pending payment orders = the DRAFT status only;
/// - occupancy = every non-cancelled / non-no-show reservation (Booked, CheckedIn, CheckedOut)
///   covering the night of the cockpit date - the recently hardened LodgingService rule,
///   expressed through Reservation.IsBlocking / CoversNight.
///
/// AGES: timestamp-based ages (submitted, rejected) are whole elapsed days -
/// floor((utcNow - moment) / 24h) - so an entry submitted exactly 48 hours ago is 2 days old
/// and one submitted 47 hours ago is 1 day old. Date-based ages (closing backlog, payment
/// orders) are calendar-day differences against the cockpit date.
///
/// CLOSING BACKLOG WINDOW: the scan covers the <see cref="ClosingLookbackDays"/> days ending
/// the day BEFORE yesterday (yesterday's closing is today's normal work, reported in the
/// health table instead), and never reaches before a unit's first recorded activity (first
/// daily revenue or first closing) so a freshly onboarded unit is not flagged for days it did
/// not exist. A unit with no recorded activity at all has nothing to close yet.
/// </summary>
public static class DecCockpitCalculator
{
    /// <summary>How many days before the cockpit date the closing backlog scan reaches.</summary>
    public const int ClosingLookbackDays = 14;

    public static DecCockpitResponse Build(
        DateOnly date,
        DateTimeOffset utcNow,
        IReadOnlyCollection<HotelUnit> units,
        IReadOnlyCollection<DailyRevenue> revenues,
        IReadOnlyCollection<DailyClosing> closings,
        IReadOnlyDictionary<string, DateOnly> firstActivityByUnitCode,
        IReadOnlyCollection<PaymentOrder> paymentOrders,
        IReadOnlyCollection<Room> rooms,
        IReadOnlyCollection<Reservation> reservations)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(revenues);
        ArgumentNullException.ThrowIfNull(closings);
        ArgumentNullException.ThrowIfNull(firstActivityByUnitCode);
        ArgumentNullException.ThrowIfNull(paymentOrders);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(reservations);

        var yesterday = date.AddDays(-1);

        var unitNamesByCode = units.ToDictionary(unit => unit.Code, unit => unit.Name);

        var pendingValidations = BuildPendingValidations(date, utcNow, revenues, unitNamesByCode);
        var closingBacklog = BuildClosingBacklog(date, units, closings, firstActivityByUnitCode);
        var rejectedRevenues = BuildRejectedRevenues(date, utcNow, revenues, unitNamesByCode);
        var pendingOrders = BuildPendingPaymentOrders(date, paymentOrders);
        var unitHealth = BuildUnitHealth(date, yesterday, units, revenues, closings, rooms, reservations);

        var oldestDelay = closingBacklog
            .OrderByDescending(unit => unit.OldestAgeDays)
            .ThenBy(unit => unit.HotelUnitCode, StringComparer.Ordinal)
            .Select(unit => new DecClosingDelay(
                unit.HotelUnitCode,
                unit.HotelUnitName,
                unit.OldestMissingDate,
                unit.OldestAgeDays))
            .FirstOrDefault();

        return new DecCockpitResponse(
            date,
            yesterday,
            pendingValidations,
            closingBacklog,
            rejectedRevenues,
            pendingOrders,
            unitHealth,
            pendingValidations.Sum(unit => unit.Count),
            pendingValidations.Sum(unit => unit.TotalAmount),
            closingBacklog.Sum(unit => unit.MissingDates.Count),
            rejectedRevenues.Count,
            pendingOrders.Count,
            pendingOrders.Sum(order => order.Amount),
            oldestDelay);
    }

    private static IReadOnlyCollection<DecPendingValidationUnit> BuildPendingValidations(
        DateOnly date,
        DateTimeOffset utcNow,
        IReadOnlyCollection<DailyRevenue> revenues,
        IReadOnlyDictionary<string, string> unitNamesByCode)
    {
        return revenues
            .Where(revenue => revenue.Status == DailyRevenueStatus.Submitted)
            .GroupBy(revenue => revenue.HotelUnitCode)
            .Select(group =>
            {
                // Oldest by submission moment; a missing SubmittedAt (never produced by the
                // domain, defensive only) sorts first so it is never silently ignored.
                var oldest = group
                    .OrderBy(revenue => revenue.SubmittedAt ?? DateTimeOffset.MinValue)
                    .ThenBy(revenue => revenue.BusinessDate)
                    .First();

                var oldestAgeDays = oldest.SubmittedAt is { } submittedAt
                    ? WholeElapsedDays(utcNow, submittedAt)
                    : CalendarAgeDays(date, oldest.BusinessDate);

                return new DecPendingValidationUnit(
                    group.Key,
                    unitNamesByCode.GetValueOrDefault(group.Key),
                    group.Count(),
                    group.Sum(revenue => revenue.Total),
                    oldest.BusinessDate,
                    oldest.SubmittedAt,
                    oldestAgeDays);
            })
            .OrderByDescending(unit => unit.OldestAgeDays)
            .ThenBy(unit => unit.HotelUnitCode, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyCollection<DecClosingBacklogUnit> BuildClosingBacklog(
        DateOnly date,
        IReadOnlyCollection<HotelUnit> units,
        IReadOnlyCollection<DailyClosing> closings,
        IReadOnlyDictionary<string, DateOnly> firstActivityByUnitCode)
    {
        var closedDays = closings
            .Where(closing => closing.IsClosed)
            .Select(closing => (closing.HotelUnitCode, closing.BusinessDate))
            .ToHashSet();

        var windowStart = date.AddDays(-ClosingLookbackDays);
        var lastBacklogDate = date.AddDays(-2);

        var backlog = new List<DecClosingBacklogUnit>();

        foreach (var unit in units)
        {
            if (!firstActivityByUnitCode.TryGetValue(unit.Code, out var firstActivity))
            {
                // No revenue and no closing ever recorded: nothing to close yet.
                continue;
            }

            var scanStart = firstActivity > windowStart ? firstActivity : windowStart;
            var missingDates = new List<DateOnly>();

            for (var day = scanStart; day <= lastBacklogDate; day = day.AddDays(1))
            {
                if (!closedDays.Contains((unit.Code, day)))
                {
                    missingDates.Add(day);
                }
            }

            if (missingDates.Count == 0)
            {
                continue;
            }

            var oldestMissing = missingDates[0];

            backlog.Add(new DecClosingBacklogUnit(
                unit.Code,
                unit.Name,
                missingDates,
                oldestMissing,
                CalendarAgeDays(date, oldestMissing)));
        }

        return backlog
            .OrderByDescending(unit => unit.OldestAgeDays)
            .ThenBy(unit => unit.HotelUnitCode, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyCollection<DecRejectedRevenueItem> BuildRejectedRevenues(
        DateOnly date,
        DateTimeOffset utcNow,
        IReadOnlyCollection<DailyRevenue> revenues,
        IReadOnlyDictionary<string, string> unitNamesByCode)
    {
        return revenues
            .Where(revenue => revenue.Status == DailyRevenueStatus.Rejected)
            .Select(revenue =>
            {
                // Reject stamps ValidatedAt with the refusal moment: that is the moment the
                // correction has been waiting since.
                var ageDays = revenue.ValidatedAt is { } rejectedAt
                    ? WholeElapsedDays(utcNow, rejectedAt)
                    : CalendarAgeDays(date, revenue.BusinessDate);

                return new DecRejectedRevenueItem(
                    revenue.Id,
                    revenue.HotelUnitCode,
                    unitNamesByCode.GetValueOrDefault(revenue.HotelUnitCode),
                    revenue.BusinessDate,
                    revenue.Total,
                    revenue.RejectionReason,
                    revenue.ValidatedAt,
                    ageDays);
            })
            .OrderByDescending(item => item.AgeDays)
            .ThenBy(item => item.HotelUnitCode, StringComparer.Ordinal)
            .ThenBy(item => item.BusinessDate)
            .ToArray();
    }

    private static IReadOnlyCollection<DecPendingPaymentOrderItem> BuildPendingPaymentOrders(
        DateOnly date,
        IReadOnlyCollection<PaymentOrder> paymentOrders)
    {
        return paymentOrders
            .Where(order => order.Status == PaymentOrderStatus.Draft)
            .Select(order => new DecPendingPaymentOrderItem(
                order.Id,
                order.OrderDate,
                order.Beneficiary,
                order.Amount,
                order.DueDate,
                order.BankAccountCode,
                CalendarAgeDays(date, order.OrderDate)))
            .OrderByDescending(order => order.AgeDays)
            .ThenBy(order => order.DueDate)
            .ToArray();
    }

    private static IReadOnlyCollection<DecUnitHealthRow> BuildUnitHealth(
        DateOnly date,
        DateOnly yesterday,
        IReadOnlyCollection<HotelUnit> units,
        IReadOnlyCollection<DailyRevenue> revenues,
        IReadOnlyCollection<DailyClosing> closings,
        IReadOnlyCollection<Room> rooms,
        IReadOnlyCollection<Reservation> reservations)
    {
        // (date, unit) is unique in the revenue module, so a plain lookup is safe.
        var yesterdayRevenueByUnit = revenues
            .Where(revenue => revenue.BusinessDate == yesterday)
            .GroupBy(revenue => revenue.HotelUnitCode)
            .ToDictionary(group => group.Key, group => group.First());

        var yesterdayClosedUnits = closings
            .Where(closing => closing.BusinessDate == yesterday && closing.IsClosed)
            .Select(closing => closing.HotelUnitCode)
            .ToHashSet();

        var activeRoomIdsByUnit = rooms
            .Where(room => room.IsActive)
            .GroupBy(room => room.HotelUnitCode)
            .ToDictionary(group => group.Key, group => group.Select(room => room.Id).ToHashSet());

        var coveringReservationsByUnit = reservations
            .Where(reservation => reservation.IsBlocking && reservation.CoversNight(date))
            .GroupBy(reservation => reservation.HotelUnitCode)
            .ToDictionary(group => group.Key, group => group.ToArray());

        return units
            .OrderBy(unit => unit.DisplayOrder)
            .ThenBy(unit => unit.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(unit =>
            {
                var revenue = yesterdayRevenueByUnit.GetValueOrDefault(unit.Code);
                var status = revenue?.Status;

                // Validated is the realised figure (the budget module's rule); a submitted one
                // is shown FLAGGED AS PROVISIONAL; a draft or rejected entry is not a usable
                // figure at all.
                var usableTotal = status is DailyRevenueStatus.Validated or DailyRevenueStatus.Submitted
                    ? revenue!.Total
                    : (decimal?)null;

                var yesterdayClosed = yesterdayClosedUnits.Contains(unit.Code);

                var activeRoomIds = activeRoomIdsByUnit.GetValueOrDefault(unit.Code);
                var activeRoomCount = activeRoomIds?.Count ?? 0;

                // Distinct active rooms blocked tonight; restricting to active rooms keeps the
                // numerator consistent with the denominator (rate can never exceed 100 %).
                var occupiedRooms = activeRoomIds is null
                    ? 0
                    : coveringReservationsByUnit.GetValueOrDefault(unit.Code, [])
                        .Select(reservation => reservation.RoomId)
                        .Distinct()
                        .Count(activeRoomIds.Contains);

                var occupancyRate = activeRoomCount == 0
                    ? (decimal?)null
                    : Math.Round(occupiedRooms * 100m / activeRoomCount, 1, MidpointRounding.AwayFromZero);

                return new DecUnitHealthRow(
                    unit.Code,
                    unit.Name,
                    status,
                    usableTotal,
                    YesterdayRevenueIsProvisional: status == DailyRevenueStatus.Submitted,
                    yesterdayClosed,
                    occupiedRooms,
                    activeRoomCount,
                    occupancyRate,
                    NeedsAttention: usableTotal is null && !yesterdayClosed);
            })
            .ToArray();
    }

    /// <summary>
    /// Whole elapsed days between a past moment and now: exactly 48 hours is 2 days, 47 hours
    /// is 1 day. A moment in the future (clock skew) is clamped to zero rather than negative.
    /// </summary>
    private static int WholeElapsedDays(DateTimeOffset utcNow, DateTimeOffset moment)
    {
        var elapsed = utcNow - moment;

        return elapsed <= TimeSpan.Zero ? 0 : (int)Math.Floor(elapsed.TotalDays);
    }

    /// <summary>Calendar-day difference against the cockpit date, clamped to zero.</summary>
    private static int CalendarAgeDays(DateOnly date, DateOnly past)
    {
        var days = date.DayNumber - past.DayNumber;

        return days < 0 ? 0 : days;
    }
}
