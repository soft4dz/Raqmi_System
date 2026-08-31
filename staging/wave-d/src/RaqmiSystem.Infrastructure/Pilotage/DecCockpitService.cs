using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Pilotage;
using RaqmiSystem.Domain.Closing;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Domain.Treasury;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Pilotage;

/// <summary>
/// EF-backed reader behind the DEC cockpit. PURE AGGREGATION: no table of its own, no write -
/// it reads the existing modules' entities through <c>dbContext.Set&lt;T&gt;()</c> and hands
/// them, already filtered to the relevant slices, to <see cref="DecCockpitCalculator"/> which
/// re-applies the status rules defensively and does all the arithmetic.
/// </summary>
public sealed class DecCockpitService(RaqmiDbContext dbContext) : IDecCockpitService
{
    public async Task<DecCockpitResponse> GetCockpitAsync(
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var yesterday = date.AddDays(-1);
        var closingWindowStart = date.AddDays(-DecCockpitCalculator.ClosingLookbackDays);

        var units = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .Where(unit => unit.IsActive)
            .ToArrayAsync(cancellationToken);

        // One slice covers the three revenue needs of the cockpit: the submitted queue and the
        // rejected queue are status-wide (whatever the business date - an old submitted entry
        // is precisely what must not be missed), and the health table needs yesterday's entry
        // whatever its status.
        var revenues = await dbContext.Set<DailyRevenue>()
            .AsNoTracking()
            .Where(revenue =>
                revenue.Status == DailyRevenueStatus.Submitted ||
                revenue.Status == DailyRevenueStatus.Rejected ||
                revenue.BusinessDate == yesterday)
            .ToArrayAsync(cancellationToken);

        var closings = await dbContext.Set<DailyClosing>()
            .AsNoTracking()
            .Where(closing => closing.BusinessDate >= closingWindowStart && closing.BusinessDate <= yesterday)
            .ToArrayAsync(cancellationToken);

        var firstActivityByUnitCode = await LoadFirstActivityByUnitAsync(cancellationToken);

        var paymentOrders = await dbContext.Set<PaymentOrder>()
            .AsNoTracking()
            .Where(order => order.Status == PaymentOrderStatus.Draft)
            .ToArrayAsync(cancellationToken);

        var rooms = await dbContext.Set<Room>()
            .AsNoTracking()
            .Where(room => room.IsActive)
            .ToArrayAsync(cancellationToken);

        // Occupancy of the cockpit date: the calculator re-checks IsBlocking / CoversNight;
        // this query only narrows the rows brought back from the database.
        var reservations = await dbContext.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation =>
                reservation.Status != ReservationStatus.Cancelled &&
                reservation.Status != ReservationStatus.NoShow &&
                reservation.ArrivalDate <= date &&
                reservation.DepartureDate > date)
            .ToArrayAsync(cancellationToken);

        return DecCockpitCalculator.Build(
            date,
            DateTimeOffset.UtcNow,
            units,
            revenues,
            closings,
            firstActivityByUnitCode,
            paymentOrders,
            rooms,
            reservations);
    }

    /// <summary>
    /// Earliest recorded activity per unit (first daily revenue or first closing, whichever is
    /// older): the lower bound of the closing-backlog scan, so a freshly onboarded unit is not
    /// flagged for days it did not exist.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, DateOnly>> LoadFirstActivityByUnitAsync(
        CancellationToken cancellationToken)
    {
        var firstRevenueDates = await dbContext.Set<DailyRevenue>()
            .AsNoTracking()
            .GroupBy(revenue => revenue.HotelUnitCode)
            .Select(group => new { Code = group.Key, First = group.Min(revenue => revenue.BusinessDate) })
            .ToArrayAsync(cancellationToken);

        var firstClosingDates = await dbContext.Set<DailyClosing>()
            .AsNoTracking()
            .GroupBy(closing => closing.HotelUnitCode)
            .Select(group => new { Code = group.Key, First = group.Min(closing => closing.BusinessDate) })
            .ToArrayAsync(cancellationToken);

        var firstActivity = new Dictionary<string, DateOnly>(StringComparer.Ordinal);

        foreach (var entry in firstRevenueDates.Concat(firstClosingDates))
        {
            if (!firstActivity.TryGetValue(entry.Code, out var existing) || entry.First < existing)
            {
                firstActivity[entry.Code] = entry.First;
            }
        }

        return firstActivity;
    }
}
