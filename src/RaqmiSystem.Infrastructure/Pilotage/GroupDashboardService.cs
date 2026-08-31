using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Pilotage;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Budgeting;
using RaqmiSystem.Domain.Closing;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Domain.Treasury;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Pilotage;

/// <summary>
/// EF-backed side of the CEO dashboard: fetches the raw facts of the requested period and of
/// the equivalent period one year earlier, then hands everything to the pure
/// <see cref="GroupDashboardCalculator"/>, which owns every counting rule. This service never
/// writes anything (a read-only screen needs no audit trail) and owns no table: it only READS
/// the other modules' entities.
///
/// The SQL-side status filters below are an OPTIMIZATION mirroring the calculator's rules
/// (loading every draft receipt or paid invoice ever recorded would be waste), never their
/// home: the calculator re-applies each rule on whatever it receives, and the calculator's unit
/// tests prove the rules on unfiltered data.
/// </summary>
public sealed class GroupDashboardService(RaqmiDbContext dbContext) : IGroupDashboardService
{
    /// <summary>
    /// Occupancy and unclosed days are computed day by day in memory; the cap mirrors
    /// LodgingService.MaxOccupancyWindowDays so an unbounded window cannot turn one request
    /// into an unbounded loop.
    /// </summary>
    private const int MaxWindowDays = 366;

    public async Task<ApplicationResult<GroupDashboardResponse>> GetGroupDashboardAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return ApplicationResult<GroupDashboardResponse>.Validation(
                "The from date cannot be after the to date.");
        }

        if (to.DayNumber - from.DayNumber + 1 > MaxWindowDays)
        {
            return ApplicationResult<GroupDashboardResponse>.Validation(
                $"The dashboard window cannot exceed {MaxWindowDays} days.");
        }

        var (previousFrom, previousTo) = GroupDashboardCalculator.PreviousPeriod(from, to);

        var activeUnits = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .Where(unit => unit.IsActive)
            .ToArrayAsync(cancellationToken);

        var currentFacts = await LoadPeriodFactsAsync(from, to, cancellationToken);
        var previousFacts = await LoadPeriodFactsAsync(previousFrom, previousTo, cancellationToken);

        // HotelUnit.IsActive only reflects the current state - same widening as
        // DailyRevenueService.GetUnitDashboardAsync, same reason: a validated revenue or a
        // confirmed receipt recorded before its unit was deactivated must never silently drop
        // out of a group total the direction reads.
        var activeUnitCodes = activeUnits.Select(unit => unit.Code).ToHashSet();
        var missingUnitCodes = currentFacts.Revenues.Select(revenue => revenue.HotelUnitCode)
            .Concat(currentFacts.Receipts.Select(receipt => receipt.HotelUnitCode))
            .Distinct()
            .Where(code => !activeUnitCodes.Contains(code))
            .ToArray();

        var units = activeUnits;

        if (missingUnitCodes.Length > 0)
        {
            var inactiveUnitsWithFacts = await dbContext.Set<HotelUnit>()
                .AsNoTracking()
                .Where(unit => missingUnitCodes.Contains(unit.Code))
                .ToArrayAsync(cancellationToken);

            units = activeUnits.Concat(inactiveUnitsWithFacts).ToArray();
        }

        var roomCountsByUnit = await dbContext.Set<Room>()
            .AsNoTracking()
            .Where(room => room.IsActive)
            .GroupBy(room => room.HotelUnitCode)
            .Select(group => new { Code = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Code, row => row.Count, cancellationToken);

        var unitInfos = units
            .OrderBy(unit => unit.DisplayOrder)
            .ThenBy(unit => unit.Name)
            .Select(unit => new GroupUnitInfo(
                unit.Code,
                unit.Name,
                unit.IsActive,
                roomCountsByUnit.GetValueOrDefault(unit.Code)))
            .ToArray();

        var closedDays = await dbContext.Set<DailyClosing>()
            .AsNoTracking()
            .Where(closing => closing.BusinessDate >= from && closing.BusinessDate <= to)
            .Select(closing => new GroupClosedDayFact(closing.HotelUnitCode, closing.BusinessDate, closing.Status))
            .ToArrayAsync(cancellationToken);

        // Only frozen plans (Approved, or Closed - a closed plan was approved and remains a
        // frozen reference for its exercise) participate in the budget columns: a draft budget
        // is not a reference anyone committed to. Monthly totals only; the month-grained
        // prorata is the calculator's documented rule.
        var scopedYears = Enumerable.Range(from.Year, to.Year - from.Year + 1).ToArray();

        // A JOIN, not a SelectMany over the Lines navigation: projecting a collection navigation
        // this way compiles to a correlated subquery (SQL APPLY / LATERAL), which SQLite refuses
        // outright. A flat join over the BudgetLine set says exactly the same thing and every
        // provider translates it.
        var budgetTargets = await (
                from plan in dbContext.Set<BudgetPlan>().AsNoTracking()
                where scopedYears.Contains(plan.Year)
                    && (plan.Status == BudgetStatus.Approved || plan.Status == BudgetStatus.Closed)
                join line in dbContext.Set<BudgetLine>().AsNoTracking()
                    on plan.Id equals line.BudgetPlanId
                select new GroupBudgetMonthTarget(
                    plan.HotelUnitCode,
                    plan.Year,
                    line.Month,
                    line.AmountTarget))
            .ToArrayAsync(cancellationToken);

        // "Today" is the UTC business day - the codebase's convention for every UtcNow-based
        // decision (see Reservation.CheckIn) - and bounds the unclosed-days count.
        var nowUtc = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(nowUtc.UtcDateTime);

        var response = new GroupDashboardCalculator().Calculate(
            from,
            to,
            today,
            nowUtc,
            unitInfos,
            currentFacts,
            previousFacts,
            budgetTargets,
            closedDays);

        return ApplicationResult<GroupDashboardResponse>.Success(response);
    }

    private async Task<GroupPeriodFacts> LoadPeriodFactsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        // Validated feeds the KPIs, Submitted feeds the pending-validation alert; Draft and
        // Rejected rows are used by nothing on this dashboard.
        var revenues = await dbContext.Set<DailyRevenue>()
            .AsNoTracking()
            .Where(revenue => revenue.BusinessDate >= from
                && revenue.BusinessDate <= to
                && (revenue.Status == DailyRevenueStatus.Validated
                    || revenue.Status == DailyRevenueStatus.Submitted))
            .Select(revenue => new GroupRevenueFact(
                revenue.HotelUnitCode,
                revenue.BusinessDate,
                revenue.Accommodation + revenue.Food + revenue.Beverage + revenue.Other,
                revenue.Status,
                revenue.SubmittedAt))
            .ToArrayAsync(cancellationToken);

        var receipts = await dbContext.Set<CashReceipt>()
            .AsNoTracking()
            .Where(receipt => receipt.ReceiptDate >= from
                && receipt.ReceiptDate <= to
                && receipt.Status == ReceiptStatus.Confirmed)
            .Select(receipt => new GroupReceiptFact(
                receipt.HotelUnitCode,
                receipt.Amount,
                receipt.Status))
            .ToArrayAsync(cancellationToken);

        // The whole outstanding ledger up to the period's end, not just the period's invoices:
        // an old unpaid invoice predates the period yet is still owed at its end.
        var invoices = await dbContext.Set<Invoice>()
            .AsNoTracking()
            .Where(invoice => invoice.InvoiceDate <= to && invoice.Status == InvoiceStatus.Issued)
            .Select(invoice => new GroupInvoiceFact(
                invoice.HotelUnitCode,
                invoice.InvoiceDate,
                invoice.TotalInclVat,
                invoice.Status))
            .ToArrayAsync(cancellationToken);

        // Blocking stays overlapping the window (half-open [arrival, departure): the departure
        // day's night is not part of the stay) - the same filter as
        // LodgingService.GetOccupancyAsync.
        var stays = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.Status != ReservationStatus.Cancelled
                && reservation.Status != ReservationStatus.NoShow
                && reservation.ArrivalDate <= to
                && reservation.DepartureDate > from)
            .Select(reservation => new GroupStayFact(
                reservation.HotelUnitCode,
                reservation.RoomId,
                reservation.ArrivalDate,
                reservation.DepartureDate,
                reservation.Status))
            .ToArrayAsync(cancellationToken);

        return new GroupPeriodFacts(revenues, receipts, invoices, stays);
    }
}
