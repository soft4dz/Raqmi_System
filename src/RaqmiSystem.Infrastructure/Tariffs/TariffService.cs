using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Tariffs;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Tariffs;
using RaqmiSystem.Infrastructure.Persistence;
using System.Data;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Tariffs;

/// <summary>
/// Management side of the tariffs module. Two invariants here are range conditions that span
/// rows and therefore cannot be a unique index:
/// <list type="bullet">
/// <item>two rate periods of the same plan and room type must never overlap (bounds inclusive -
/// a night has exactly one price), and</item>
/// <item>a customer has at most one active convention covering any given day.</item>
/// </list>
/// Both are enforced with the repository's atomic-guard pattern (see
/// <c>AccountingService.UpdateEntryLinesAsync</c>): the check and the write share one
/// Serializable transaction, the parent row is claimed with a conditional single-statement
/// UPDATE where one exists (rate periods claim their plan's row, so concurrent period writes
/// under the same plan serialize on that row), and a serialization abort surfaces as a
/// retryable 409 instead of a silent double booking. Conventions have no single parent row of
/// this module to claim (the customer row belongs to billing), so they rely on the Serializable
/// isolation itself: PostgreSQL's SSI aborts one of two racing check-then-insert transactions,
/// and the SQLite test provider serializes writers with its database lock - both outcomes are
/// classified by <see cref="DbUpdateExceptionExtensions.IsSerializationFailure"/>.
/// The one-default-active-plan-per-unit invariant IS an index
/// (ux_rate_plans_default_per_unit, filtered); the service pre-checks for friendly messages and
/// treats the index as the concurrency backstop.
/// </summary>
public sealed class TariffService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter) : ITariffService
{
    private const string RatePlansEntity = "tariffs.rate_plans";

    private const string RatePeriodsEntity = "tariffs.rate_periods";

    private const string ConventionsEntity = "tariffs.customer_conventions";

    private const string ConcurrentTariffMutationRefused =
        "A concurrent operation modified the same tariff data, so this change was rolled back " +
        "and nothing was modified. Reload and try again.";

    public async Task<IReadOnlyCollection<RatePlanResponse>> ListPlansAsync(
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<RatePlan>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(plan => plan.IsActive);
        }

        var normalizedUnitCode = NormalizeNullableCode(hotelUnitCode);

        if (normalizedUnitCode is not null)
        {
            query = query.Where(plan => plan.HotelUnitCode == normalizedUnitCode);
        }

        var plans = await query
            .OrderBy(plan => plan.HotelUnitCode)
            .ThenBy(plan => plan.Code)
            .ToArrayAsync(cancellationToken);

        return plans.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<RatePlanResponse>> GetPlanAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var plan = await LoadPlanAsync(code, track: false, cancellationToken);

        if (plan is null)
        {
            return ApplicationResult<RatePlanResponse>.NotFound("Rate plan was not found.");
        }

        return ApplicationResult<RatePlanResponse>.Success(Map(plan));
    }

    public async Task<ApplicationResult<RatePlanResponse>> CreatePlanAsync(
        CreateRatePlanRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        RatePlan plan;

        try
        {
            plan = new RatePlan(request.Code, request.Label, request.HotelUnitCode, request.IsDefault);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RatePlanResponse>.Validation(ex.Message);
        }

        var unit = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == plan.HotelUnitCode, cancellationToken);

        if (unit is null)
        {
            return ApplicationResult<RatePlanResponse>.NotFound("Hotel unit was not found.");
        }

        if (!unit.IsActive)
        {
            return ApplicationResult<RatePlanResponse>.Validation(
                "Rate plans cannot be created for an inactive hotel unit.");
        }

        var codeExists = await dbContext.Set<RatePlan>()
            .AnyAsync(current => current.Code == plan.Code, cancellationToken);

        if (codeExists)
        {
            return ApplicationResult<RatePlanResponse>.Conflict("A rate plan with this code already exists.");
        }

        if (plan.IsDefault)
        {
            var defaultExists = await dbContext.Set<RatePlan>()
                .AnyAsync(
                    current => current.HotelUnitCode == plan.HotelUnitCode && current.IsDefault && current.IsActive,
                    cancellationToken);

            if (defaultExists)
            {
                return ApplicationResult<RatePlanResponse>.Conflict(
                    "The hotel unit already has an active default rate plan. " +
                    "Use POST /tariffs/plans/{code}/set-default to change it.");
            }
        }

        plan.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<RatePlan>().Add(plan);

        try
        {
            await WriteAuditAsync(
                "tariffs.rate_plan.created",
                RatePlansEntity,
                plan.Id,
                context,
                new { plan.Code, plan.Label, plan.HotelUnitCode, plan.IsDefault },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // The pre-checks above and this insert are not atomic: a concurrent create loses the
            // race against ux_rate_plans_code or ux_rate_plans_default_per_unit. Which one cannot
            // be told apart on the SQLite test provider, so the message names both.
            return ApplicationResult<RatePlanResponse>.Conflict(
                "A concurrent operation already created a rate plan with this code, or the hotel " +
                "unit already has an active default rate plan.");
        }

        return ApplicationResult<RatePlanResponse>.Success(Map(plan));
    }

    public async Task<ApplicationResult<RatePlanResponse>> UpdatePlanAsync(
        string code,
        UpdateRatePlanRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var plan = await LoadPlanAsync(code, track: true, cancellationToken);

        if (plan is null)
        {
            return ApplicationResult<RatePlanResponse>.NotFound("Rate plan was not found.");
        }

        try
        {
            plan.UpdateDetails(request.Label);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RatePlanResponse>.Validation(ex.Message);
        }

        plan.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "tariffs.rate_plan.updated",
            RatePlansEntity,
            plan.Id,
            context,
            new { plan.Code, plan.Label, plan.HotelUnitCode },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<RatePlanResponse>.Success(Map(plan));
    }

    public async Task<ApplicationResult<RatePlanResponse>> SetPlanDefaultAsync(
        string code,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        // The default swap (clear the current default, flag the new one) must be atomic with the
        // read that found the current default, or two concurrent swaps could each clear the
        // other's target and flag their own - two active defaults, or none. Serializable
        // transaction plus the filtered unique index as backstop.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var plan = await LoadPlanAsync(code, track: true, cancellationToken);

            if (plan is null)
            {
                return ApplicationResult<RatePlanResponse>.NotFound("Rate plan was not found.");
            }

            if (!plan.IsActive)
            {
                return ApplicationResult<RatePlanResponse>.Validation(
                    "An inactive rate plan cannot be the default plan of its unit. Activate it first.");
            }

            var now = DateTimeOffset.UtcNow;

            // Atomic claim of the plan row (the repository's claim-in-one-statement pattern, see
            // AccountingService.TryClaimDraftEntryAsync): re-asserts IsActive as the WHERE clause
            // of a conditional UPDATE so a concurrent deactivation makes the claim miss instead of
            // letting an inactive plan become the default.
            if (!await TryClaimActivePlanAsync(plan.Id, now, cancellationToken))
            {
                return ApplicationResult<RatePlanResponse>.Conflict(ConcurrentTariffMutationRefused);
            }

            var currentDefaults = await dbContext.Set<RatePlan>()
                .Where(current =>
                    current.HotelUnitCode == plan.HotelUnitCode &&
                    current.IsDefault &&
                    current.Id != plan.Id)
                .ToArrayAsync(cancellationToken);

            foreach (var currentDefault in currentDefaults)
            {
                currentDefault.ClearDefault();
                currentDefault.MarkUpdated(context.UserName, now);
            }

            // The clear is flushed BEFORE the new flag is written: both updates travel in the
            // same transaction (atomic for every other connection), but the unique filtered
            // index is checked per statement, and EF orders a single SaveChanges batch
            // arbitrarily - setting the new default while the old row still carries its flag
            // would trip ux_rate_plans_default_per_unit mid-swap.
            if (currentDefaults.Length > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            plan.SetAsDefault();
            plan.MarkUpdated(context.UserName, now);

            await WriteAuditAsync(
                "tariffs.rate_plan.set_default",
                RatePlansEntity,
                plan.Id,
                context,
                new
                {
                    plan.Code,
                    plan.HotelUnitCode,
                    PreviousDefaults = currentDefaults.Select(current => current.Code).ToArray()
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<RatePlanResponse>.Success(Map(plan));
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<RatePlanResponse>.Conflict(ConcurrentTariffMutationRefused);
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<RatePlanResponse>.Conflict(ConcurrentTariffMutationRefused);
        }
    }

    public async Task<ApplicationResult<RatePlanResponse>> SetPlanActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var plan = await LoadPlanAsync(code, track: true, cancellationToken);

        if (plan is null)
        {
            return ApplicationResult<RatePlanResponse>.NotFound("Rate plan was not found.");
        }

        if (isActive)
        {
            plan.Activate();
        }
        else
        {
            plan.Deactivate();
        }

        plan.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        try
        {
            await WriteAuditAsync(
                isActive ? "tariffs.rate_plan.activated" : "tariffs.rate_plan.deactivated",
                RatePlansEntity,
                plan.Id,
                context,
                new { plan.Code, plan.HotelUnitCode, plan.IsActive },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Reactivating a plan that still carries its dormant default flag while the unit
            // gained another active default in the meantime trips ux_rate_plans_default_per_unit.
            return ApplicationResult<RatePlanResponse>.Conflict(
                "This plan still carries the default flag and its unit already has another active " +
                "default rate plan. Set the other plan as default first, or activate this plan " +
                "after clearing its default flag via POST /tariffs/plans/{code}/set-default on the other plan.");
        }

        return ApplicationResult<RatePlanResponse>.Success(Map(plan));
    }

    public async Task<ApplicationResult<IReadOnlyCollection<RatePeriodResponse>>> ListPeriodsAsync(
        string planCode,
        string? roomTypeCode,
        CancellationToken cancellationToken)
    {
        var plan = await LoadPlanAsync(planCode, track: false, cancellationToken);

        if (plan is null)
        {
            return ApplicationResult<IReadOnlyCollection<RatePeriodResponse>>.NotFound("Rate plan was not found.");
        }

        var query = dbContext.Set<RatePeriod>()
            .AsNoTracking()
            .Where(period => period.RatePlanId == plan.Id);

        var normalizedRoomType = NormalizeNullableCode(roomTypeCode);

        if (normalizedRoomType is not null)
        {
            query = query.Where(period => period.RoomTypeCode == normalizedRoomType);
        }

        var periods = await query
            .OrderBy(period => period.RoomTypeCode)
            .ThenBy(period => period.FromDate)
            .ToArrayAsync(cancellationToken);

        return ApplicationResult<IReadOnlyCollection<RatePeriodResponse>>.Success(
            periods.Select(period => Map(period, plan.Code)).ToArray());
    }

    public async Task<ApplicationResult<RatePeriodResponse>> AddPeriodAsync(
        string planCode,
        CreateRatePeriodRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var plan = await LoadPlanAsync(planCode, track: false, cancellationToken);

            if (plan is null)
            {
                return ApplicationResult<RatePeriodResponse>.NotFound("Rate plan was not found.");
            }

            RatePeriod period;

            try
            {
                period = new RatePeriod(
                    plan.Id,
                    request.RoomTypeCode,
                    request.FromDate,
                    request.ToDate,
                    request.NightlyAmount);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                return ApplicationResult<RatePeriodResponse>.Validation(ex.Message);
            }

            var now = DateTimeOffset.UtcNow;

            // Claim the plan's row so every concurrent period mutation under the same plan
            // serializes on that one row instead of racing the overlap check below (the
            // claim-in-one-statement pattern of AccountingService.TryClaimDraftEntryAsync).
            if (!await TryClaimPlanAsync(plan.Id, now, cancellationToken))
            {
                return ApplicationResult<RatePeriodResponse>.Conflict(ConcurrentTariffMutationRefused);
            }

            var overlapFailure = await FindOverlapAsync(period, excludedPeriodId: null, cancellationToken);

            if (overlapFailure is not null)
            {
                return overlapFailure;
            }

            period.MarkCreated(context.UserName, now);
            dbContext.Set<RatePeriod>().Add(period);

            await WriteAuditAsync(
                "tariffs.rate_period.created",
                RatePeriodsEntity,
                period.Id,
                context,
                new { PlanCode = plan.Code, period.RoomTypeCode, period.FromDate, period.ToDate, period.NightlyAmount },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<RatePeriodResponse>.Success(Map(period, plan.Code));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<RatePeriodResponse>.Conflict(ConcurrentTariffMutationRefused);
        }
    }

    public async Task<ApplicationResult<RatePeriodResponse>> UpdatePeriodAsync(
        string planCode,
        Guid periodId,
        UpdateRatePeriodRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var plan = await LoadPlanAsync(planCode, track: false, cancellationToken);

            if (plan is null)
            {
                return ApplicationResult<RatePeriodResponse>.NotFound("Rate plan was not found.");
            }

            var period = await dbContext.Set<RatePeriod>()
                .SingleOrDefaultAsync(
                    current => current.Id == periodId && current.RatePlanId == plan.Id,
                    cancellationToken);

            if (period is null)
            {
                return ApplicationResult<RatePeriodResponse>.NotFound("Rate period was not found.");
            }

            // Validation and the overlap check run against a throwaway probe so a refused update
            // leaves the TRACKED period untouched: mutating it first would leave a phantom
            // modification in the change tracker that the next flush on the same context would
            // quietly persist.
            RatePeriod probe;

            try
            {
                probe = new RatePeriod(
                    period.RatePlanId,
                    period.RoomTypeCode,
                    request.FromDate,
                    request.ToDate,
                    request.NightlyAmount);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                return ApplicationResult<RatePeriodResponse>.Validation(ex.Message);
            }

            var now = DateTimeOffset.UtcNow;

            if (!await TryClaimPlanAsync(plan.Id, now, cancellationToken))
            {
                return ApplicationResult<RatePeriodResponse>.Conflict(ConcurrentTariffMutationRefused);
            }

            var overlapFailure = await FindOverlapAsync(probe, excludedPeriodId: period.Id, cancellationToken);

            if (overlapFailure is not null)
            {
                return overlapFailure;
            }

            period.Reschedule(request.FromDate, request.ToDate, request.NightlyAmount);
            period.MarkUpdated(context.UserName, now);

            await WriteAuditAsync(
                "tariffs.rate_period.updated",
                RatePeriodsEntity,
                period.Id,
                context,
                new { PlanCode = plan.Code, period.RoomTypeCode, period.FromDate, period.ToDate, period.NightlyAmount },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<RatePeriodResponse>.Success(Map(period, plan.Code));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<RatePeriodResponse>.Conflict(ConcurrentTariffMutationRefused);
        }
    }

    public async Task<ApplicationResult<RatePeriodResponse>> DeletePeriodAsync(
        string planCode,
        Guid periodId,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var plan = await LoadPlanAsync(planCode, track: false, cancellationToken);

        if (plan is null)
        {
            return ApplicationResult<RatePeriodResponse>.NotFound("Rate plan was not found.");
        }

        var period = await dbContext.Set<RatePeriod>()
            .SingleOrDefaultAsync(
                current => current.Id == periodId && current.RatePlanId == plan.Id,
                cancellationToken);

        if (period is null)
        {
            return ApplicationResult<RatePeriodResponse>.NotFound("Rate period was not found.");
        }

        // No overlap guard: removing a period can only widen the gaps, never create a double
        // price. The removed row is echoed back so the caller can undo a misclick by recreating it.
        dbContext.Set<RatePeriod>().Remove(period);

        await WriteAuditAsync(
            "tariffs.rate_period.deleted",
            RatePeriodsEntity,
            period.Id,
            context,
            new { PlanCode = plan.Code, period.RoomTypeCode, period.FromDate, period.ToDate, period.NightlyAmount },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<RatePeriodResponse>.Success(Map(period, plan.Code));
    }

    public async Task<IReadOnlyCollection<CustomerConventionResponse>> ListConventionsAsync(
        string? customerCode,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<CustomerConvention>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(convention => convention.IsActive);
        }

        var normalizedCustomerCode = NormalizeNullableCode(customerCode);

        if (normalizedCustomerCode is not null)
        {
            query = query.Where(convention => convention.CustomerCode == normalizedCustomerCode);
        }

        var conventions = await query
            .OrderBy(convention => convention.CustomerCode)
            .ThenBy(convention => convention.FromDate)
            .ToArrayAsync(cancellationToken);

        var customerNames = await LoadCustomerNamesAsync(
            conventions.Select(convention => convention.CustomerCode).Distinct().ToArray(),
            cancellationToken);

        return conventions
            .Select(convention => Map(convention, customerNames.GetValueOrDefault(convention.CustomerCode)))
            .ToArray();
    }

    public async Task<ApplicationResult<CustomerConventionResponse>> GetConventionAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var convention = await dbContext.Set<CustomerConvention>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (convention is null)
        {
            return ApplicationResult<CustomerConventionResponse>.NotFound("Customer convention was not found.");
        }

        return ApplicationResult<CustomerConventionResponse>.Success(
            Map(convention, await LoadCustomerNameAsync(convention.CustomerCode, cancellationToken)));
    }

    public async Task<ApplicationResult<CustomerConventionResponse>> CreateConventionAsync(
        CreateCustomerConventionRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            CustomerConvention convention;

            try
            {
                convention = new CustomerConvention(
                    request.CustomerCode,
                    request.RatePlanCode,
                    request.DiscountPercent,
                    request.FromDate,
                    request.ToDate);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                return ApplicationResult<CustomerConventionResponse>.Validation(ex.Message);
            }

            var referenceFailure = await ValidateConventionReferencesAsync(convention, cancellationToken);

            if (referenceFailure is not null)
            {
                return referenceFailure;
            }

            var overlapFailure = await FindConventionOverlapAsync(convention, excludedConventionId: null, cancellationToken);

            if (overlapFailure is not null)
            {
                return overlapFailure;
            }

            var now = DateTimeOffset.UtcNow;

            convention.MarkCreated(context.UserName, now);
            dbContext.Set<CustomerConvention>().Add(convention);

            await WriteAuditAsync(
                "tariffs.customer_convention.created",
                ConventionsEntity,
                convention.Id,
                context,
                new
                {
                    convention.CustomerCode,
                    convention.RatePlanCode,
                    convention.DiscountPercent,
                    convention.FromDate,
                    convention.ToDate
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<CustomerConventionResponse>.Success(
                Map(convention, await LoadCustomerNameAsync(convention.CustomerCode, cancellationToken)));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<CustomerConventionResponse>.Conflict(ConcurrentTariffMutationRefused);
        }
    }

    public async Task<ApplicationResult<CustomerConventionResponse>> UpdateConventionAsync(
        Guid id,
        UpdateCustomerConventionRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var convention = await dbContext.Set<CustomerConvention>()
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (convention is null)
            {
                return ApplicationResult<CustomerConventionResponse>.NotFound("Customer convention was not found.");
            }

            // Validation, the reference checks and the overlap check all run against a throwaway
            // probe so a refused update leaves the TRACKED convention untouched: mutating it
            // first would leave a phantom modification in the change tracker that the next flush
            // on the same context would quietly persist.
            CustomerConvention probe;

            try
            {
                probe = new CustomerConvention(
                    convention.CustomerCode,
                    request.RatePlanCode,
                    request.DiscountPercent,
                    request.FromDate,
                    request.ToDate);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                return ApplicationResult<CustomerConventionResponse>.Validation(ex.Message);
            }

            var referenceFailure = await ValidateConventionReferencesAsync(probe, cancellationToken);

            if (referenceFailure is not null)
            {
                return referenceFailure;
            }

            if (convention.IsActive)
            {
                var overlapFailure = await FindConventionOverlapAsync(
                    probe,
                    excludedConventionId: convention.Id,
                    cancellationToken);

                if (overlapFailure is not null)
                {
                    return overlapFailure;
                }
            }

            convention.UpdateTerms(request.RatePlanCode, request.DiscountPercent, request.FromDate, request.ToDate);
            convention.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

            await WriteAuditAsync(
                "tariffs.customer_convention.updated",
                ConventionsEntity,
                convention.Id,
                context,
                new
                {
                    convention.CustomerCode,
                    convention.RatePlanCode,
                    convention.DiscountPercent,
                    convention.FromDate,
                    convention.ToDate
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<CustomerConventionResponse>.Success(
                Map(convention, await LoadCustomerNameAsync(convention.CustomerCode, cancellationToken)));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<CustomerConventionResponse>.Conflict(ConcurrentTariffMutationRefused);
        }
    }

    public async Task<ApplicationResult<CustomerConventionResponse>> SetConventionActiveAsync(
        Guid id,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        // Reactivation can recreate the very overlap deactivation had resolved, so it runs the
        // same guarded check as create/update. Deactivation cannot create an overlap but shares
        // the transaction for symmetry (and so a concurrent activation cannot slip in between).
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var convention = await dbContext.Set<CustomerConvention>()
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (convention is null)
            {
                return ApplicationResult<CustomerConventionResponse>.NotFound("Customer convention was not found.");
            }

            if (isActive)
            {
                var overlapFailure = await FindConventionOverlapAsync(
                    convention,
                    excludedConventionId: convention.Id,
                    cancellationToken);

                if (overlapFailure is not null)
                {
                    return overlapFailure;
                }

                convention.Activate();
            }
            else
            {
                convention.Deactivate();
            }

            convention.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

            await WriteAuditAsync(
                isActive ? "tariffs.customer_convention.activated" : "tariffs.customer_convention.deactivated",
                ConventionsEntity,
                convention.Id,
                context,
                new { convention.CustomerCode, convention.RatePlanCode, convention.IsActive },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<CustomerConventionResponse>.Success(
                Map(convention, await LoadCustomerNameAsync(convention.CustomerCode, cancellationToken)));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<CustomerConventionResponse>.Conflict(ConcurrentTariffMutationRefused);
        }
    }

    /// <summary>
    /// Atomic claim of a plan row (the claim-in-one-statement pattern of
    /// <c>AccountingService.TryClaimDraftEntryAsync</c>): every period mutation under a plan
    /// writes the plan's own row first, so two concurrent mutations serialize on it and the
    /// loser's overlap check is guaranteed to see the winner's rows (or abort with a
    /// serialization failure, answered as a retryable 409). The single column written,
    /// UpdatedAt, is metadata the mutation would be entitled to stamp anyway.
    /// </summary>
    private async Task<bool> TryClaimPlanAsync(
        Guid planId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var claimedRows = await dbContext.Set<RatePlan>()
            .Where(current => current.Id == planId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(current => current.UpdatedAt, now),
                cancellationToken);

        return claimedRows == 1;
    }

    /// <summary>
    /// Same claim, with IsActive re-asserted in the WHERE clause: used by the default swap so a
    /// concurrent deactivation makes the claim miss instead of letting an inactive plan become
    /// the unit's default.
    /// </summary>
    private async Task<bool> TryClaimActivePlanAsync(
        Guid planId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var claimedRows = await dbContext.Set<RatePlan>()
            .Where(current => current.Id == planId && current.IsActive)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(current => current.UpdatedAt, now),
                cancellationToken);

        return claimedRows == 1;
    }

    /// <summary>
    /// The module's central invariant, queried as a range intersection with INCLUSIVE bounds:
    /// candidate.From &lt;= existing.To AND existing.From &lt;= candidate.To. A period ending on
    /// the 10th and one starting on the 10th DO overlap - the night of the 10th would carry two
    /// prices, and a night has exactly one price. Returns null when the candidate fits.
    /// </summary>
    private async Task<ApplicationResult<RatePeriodResponse>?> FindOverlapAsync(
        RatePeriod candidate,
        Guid? excludedPeriodId,
        CancellationToken cancellationToken)
    {
        var overlapping = await dbContext.Set<RatePeriod>()
            .AsNoTracking()
            .Where(existing =>
                existing.RatePlanId == candidate.RatePlanId &&
                existing.RoomTypeCode == candidate.RoomTypeCode &&
                (excludedPeriodId == null || existing.Id != excludedPeriodId) &&
                existing.FromDate <= candidate.ToDate &&
                candidate.FromDate <= existing.ToDate)
            .OrderBy(existing => existing.FromDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (overlapping is null)
        {
            return null;
        }

        return ApplicationResult<RatePeriodResponse>.Conflict(
            $"The period overlaps an existing period for room type '{candidate.RoomTypeCode}' " +
            $"({Format(overlapping.FromDate)} to {Format(overlapping.ToDate)}). " +
            "Two periods of the same plan and room type cannot cover the same night, bounds included.");
    }

    /// <summary>
    /// At most one ACTIVE convention per customer and per day (bounds inclusive, same range
    /// intersection as <see cref="FindOverlapAsync"/>). Returns null when the candidate fits.
    /// </summary>
    private async Task<ApplicationResult<CustomerConventionResponse>?> FindConventionOverlapAsync(
        CustomerConvention candidate,
        Guid? excludedConventionId,
        CancellationToken cancellationToken)
    {
        var overlapping = await dbContext.Set<CustomerConvention>()
            .AsNoTracking()
            .Where(existing =>
                existing.CustomerCode == candidate.CustomerCode &&
                existing.IsActive &&
                (excludedConventionId == null || existing.Id != excludedConventionId) &&
                existing.FromDate <= candidate.ToDate &&
                candidate.FromDate <= existing.ToDate)
            .OrderBy(existing => existing.FromDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (overlapping is null)
        {
            return null;
        }

        return ApplicationResult<CustomerConventionResponse>.Conflict(
            $"The customer already has an active convention valid from {Format(overlapping.FromDate)} " +
            $"to {Format(overlapping.ToDate)}. A customer cannot have two conventions valid on the same day.");
    }

    private async Task<ApplicationResult<CustomerConventionResponse>?> ValidateConventionReferencesAsync(
        CustomerConvention convention,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.Set<Customer>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == convention.CustomerCode, cancellationToken);

        if (customer is null)
        {
            return ApplicationResult<CustomerConventionResponse>.NotFound("Customer was not found.");
        }

        if (!customer.IsActive)
        {
            return ApplicationResult<CustomerConventionResponse>.Validation(
                "Conventions cannot be created for an inactive customer.");
        }

        var plan = await dbContext.Set<RatePlan>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == convention.RatePlanCode, cancellationToken);

        if (plan is null)
        {
            return ApplicationResult<CustomerConventionResponse>.NotFound("Rate plan was not found.");
        }

        if (!plan.IsActive)
        {
            return ApplicationResult<CustomerConventionResponse>.Validation(
                "Conventions cannot reference an inactive rate plan.");
        }

        return null;
    }

    private async Task<RatePlan?> LoadPlanAsync(string code, bool track, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var query = track
            ? dbContext.Set<RatePlan>().AsQueryable()
            : dbContext.Set<RatePlan>().AsNoTracking();

        return await query.SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);
    }

    private async Task<string?> LoadCustomerNameAsync(string customerCode, CancellationToken cancellationToken)
    {
        return await dbContext.Set<Customer>()
            .AsNoTracking()
            .Where(customer => customer.Code == customerCode)
            .Select(customer => customer.Name)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<Dictionary<string, string>> LoadCustomerNamesAsync(
        string[] customerCodes,
        CancellationToken cancellationToken)
    {
        if (customerCodes.Length == 0)
        {
            return new Dictionary<string, string>();
        }

        return await dbContext.Set<Customer>()
            .AsNoTracking()
            .Where(customer => customerCodes.Contains(customer.Code))
            .ToDictionaryAsync(customer => customer.Code, customer => customer.Name, cancellationToken);
    }

    private static RatePlanResponse Map(RatePlan plan)
    {
        return new RatePlanResponse(
            plan.Id,
            plan.Code,
            plan.Label,
            plan.HotelUnitCode,
            plan.IsDefault,
            plan.IsActive,
            plan.CreatedAt,
            plan.CreatedBy,
            plan.UpdatedAt,
            plan.UpdatedBy);
    }

    private static RatePeriodResponse Map(RatePeriod period, string planCode)
    {
        return new RatePeriodResponse(
            period.Id,
            planCode,
            period.RoomTypeCode,
            period.FromDate,
            period.ToDate,
            period.NightlyAmount,
            period.CreatedAt,
            period.CreatedBy,
            period.UpdatedAt,
            period.UpdatedBy);
    }

    private static CustomerConventionResponse Map(CustomerConvention convention, string? customerName)
    {
        return new CustomerConventionResponse(
            convention.Id,
            convention.CustomerCode,
            customerName,
            convention.RatePlanCode,
            convention.DiscountPercent,
            convention.FromDate,
            convention.ToDate,
            convention.IsActive,
            convention.CreatedAt,
            convention.CreatedBy,
            convention.UpdatedAt,
            convention.UpdatedBy);
    }

    private static string Format(DateOnly date)
    {
        return date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
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
