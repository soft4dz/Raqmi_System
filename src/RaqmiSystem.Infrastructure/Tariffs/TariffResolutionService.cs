using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Tariffs;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Tariffs;
using RaqmiSystem.Infrastructure.Persistence;
using System.Globalization;

namespace RaqmiSystem.Infrastructure.Tariffs;

/// <summary>
/// Resolves the price of one night for a unit + room type + date, applying the customer's
/// convention when one is valid on that night. Read-only: the PMS module calls this on every
/// stay quote, so it deliberately opens no transaction and tracks nothing.
///
/// <para>
/// Plan selection: when <c>customerCode</c> is provided and the customer has an ACTIVE
/// convention whose validity window covers the night, the convention's plan is used - provided
/// that plan is itself active AND belongs to the requested unit; otherwise the resolution falls
/// back to the unit's default active plan, exactly as if no convention existed (a convention
/// negotiated on another unit's plan must not silently price this unit's rooms). The response's
/// <c>ConventionCustomerCode</c> is only filled when the convention actually applied.
/// </para>
///
/// <para>
/// The two NotFound outcomes are the most common OPERATIONS errors (a unit whose default plan
/// was never designated, a season nobody priced yet), so their messages name exactly what is
/// missing and what to do about it.
/// </para>
/// </summary>
public sealed class TariffResolutionService(RaqmiDbContext dbContext) : ITariffResolutionService
{
    public async Task<ApplicationResult<ResolvedNightlyRate>> ResolveAsync(
        string hotelUnitCode,
        string roomTypeCode,
        DateOnly night,
        string? customerCode,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeNullableCode(hotelUnitCode);

        if (normalizedUnitCode is null)
        {
            return ApplicationResult<ResolvedNightlyRate>.Validation("Hotel unit code is required.");
        }

        var normalizedRoomTypeCode = NormalizeNullableCode(roomTypeCode);

        if (normalizedRoomTypeCode is null)
        {
            return ApplicationResult<ResolvedNightlyRate>.Validation("Room type code is required.");
        }

        var unitExists = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .AnyAsync(unit => unit.Code == normalizedUnitCode, cancellationToken);

        if (!unitExists)
        {
            return ApplicationResult<ResolvedNightlyRate>.NotFound("Hotel unit was not found.");
        }

        RatePlan? plan = null;
        CustomerConvention? appliedConvention = null;

        var normalizedCustomerCode = NormalizeNullableCode(customerCode);

        if (normalizedCustomerCode is not null)
        {
            // The management invariant guarantees at most one active convention per customer and
            // per day; ordering makes the pick deterministic even if data predating the invariant
            // ever violated it.
            var convention = await dbContext.Set<CustomerConvention>()
                .AsNoTracking()
                .Where(current =>
                    current.CustomerCode == normalizedCustomerCode &&
                    current.IsActive &&
                    current.FromDate <= night &&
                    night <= current.ToDate)
                .OrderBy(current => current.FromDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (convention is not null)
            {
                var conventionPlan = await dbContext.Set<RatePlan>()
                    .AsNoTracking()
                    .SingleOrDefaultAsync(current => current.Code == convention.RatePlanCode, cancellationToken);

                if (conventionPlan is not null
                    && conventionPlan.IsActive
                    && conventionPlan.HotelUnitCode == normalizedUnitCode)
                {
                    plan = conventionPlan;
                    appliedConvention = convention;
                }
            }
        }

        if (plan is null)
        {
            plan = await dbContext.Set<RatePlan>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    current => current.HotelUnitCode == normalizedUnitCode && current.IsDefault && current.IsActive,
                    cancellationToken);

            if (plan is null)
            {
                return ApplicationResult<ResolvedNightlyRate>.NotFound(
                    $"Hotel unit '{normalizedUnitCode}' has no active default rate plan. " +
                    "Designate a default plan (POST /tariffs/plans/{code}/set-default) before resolving rates.");
            }
        }

        var period = await dbContext.Set<RatePeriod>()
            .AsNoTracking()
            .Where(current =>
                current.RatePlanId == plan.Id &&
                current.RoomTypeCode == normalizedRoomTypeCode &&
                current.FromDate <= night &&
                night <= current.ToDate)
            .OrderBy(current => current.FromDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (period is null)
        {
            return ApplicationResult<ResolvedNightlyRate>.NotFound(
                $"Rate plan '{plan.Code}' has no period covering the night of " +
                $"{night.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} for room type " +
                $"'{normalizedRoomTypeCode}'. Complete the plan's rate periods for that season.");
        }

        var amount = period.NightlyAmount;
        var discountPercent = appliedConvention?.DiscountPercent;

        if (discountPercent.HasValue)
        {
            // Money rounding: 2 decimals, midpoints away from zero (the repository's money
            // convention - see InvoiceLine.RoundMoney). 250.25 with a 10% discount is 225.225,
            // charged as 225.23, not banker's-rounded down to 225.22.
            amount = Math.Round(
                amount * (1m - (discountPercent.Value / 100m)),
                2,
                MidpointRounding.AwayFromZero);
        }

        return ApplicationResult<ResolvedNightlyRate>.Success(
            new ResolvedNightlyRate(
                amount,
                plan.Code,
                appliedConvention?.CustomerCode,
                discountPercent));
    }

    private static string? NormalizeNullableCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
    }
}
