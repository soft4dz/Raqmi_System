using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Budgeting;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Budgeting;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Infrastructure.Persistence;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Budgeting;

public sealed class BudgetService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter) : IBudgetService
{
    private const int MinimumYear = 2000;
    private const int MaximumYear = 2999;

    public async Task<IReadOnlyCollection<BudgetPlanResponse>> ListPlansAsync(
        int? year,
        string? hotelUnitCode,
        BudgetStatus? status,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<BudgetPlan>()
            .AsNoTracking()
            .Include(plan => plan.Lines)
            .AsQueryable();

        if (year.HasValue)
        {
            query = query.Where(plan => plan.Year == year.Value);
        }

        var normalizedUnitCode = NormalizeNullableCode(hotelUnitCode);

        if (normalizedUnitCode is not null)
        {
            query = query.Where(plan => plan.HotelUnitCode == normalizedUnitCode);
        }

        if (status.HasValue)
        {
            query = query.Where(plan => plan.Status == status.Value);
        }

        var plans = await query
            .OrderByDescending(plan => plan.Year)
            .ThenBy(plan => plan.HotelUnitCode)
            .ToArrayAsync(cancellationToken);

        return plans.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<BudgetPlanResponse>> GetPlanAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var plan = await dbContext.Set<BudgetPlan>()
            .AsNoTracking()
            .Include(current => current.Lines)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (plan is null)
        {
            return ApplicationResult<BudgetPlanResponse>.NotFound("Budget plan was not found.");
        }

        return ApplicationResult<BudgetPlanResponse>.Success(Map(plan));
    }

    public async Task<ApplicationResult<BudgetPlanResponse>> CreatePlanAsync(
        CreateBudgetPlanRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(request.HotelUnitCode);

        if (string.IsNullOrWhiteSpace(normalizedUnitCode))
        {
            return ApplicationResult<BudgetPlanResponse>.Validation("Hotel unit code is required.");
        }

        var unit = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedUnitCode, cancellationToken);

        if (unit is null)
        {
            return ApplicationResult<BudgetPlanResponse>.NotFound("Hotel unit was not found.");
        }

        if (!unit.IsActive)
        {
            return ApplicationResult<BudgetPlanResponse>.Validation(
                "A budget plan cannot be created for an inactive hotel unit.");
        }

        var exists = await dbContext.Set<BudgetPlan>()
            .AnyAsync(
                current => current.Year == request.Year && current.HotelUnitCode == normalizedUnitCode,
                cancellationToken);

        if (exists)
        {
            return ApplicationResult<BudgetPlanResponse>.Conflict(
                "A budget plan already exists for this year and hotel unit.");
        }

        BudgetPlan plan;

        try
        {
            plan = new BudgetPlan(request.Year, normalizedUnitCode, request.Label);

            if (request.Lines is { Count: > 0 })
            {
                plan.ReplaceLines(BuildLines(request.Lines));
            }
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<BudgetPlanResponse>.Validation(ex.Message);
        }

        plan.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<BudgetPlan>().Add(plan);

        try
        {
            await WriteAuditAsync(
                "budget.plan.created",
                "budgeting.budget_plans",
                plan.Id,
                context,
                new { plan.Year, plan.HotelUnitCode, plan.Label, LineCount = plan.Lines.Count },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // The exists-check above and this insert are not atomic: a concurrent create for the
            // same (year, unit) loses the race against ux_budget_plans_year_hotel_unit.
            return ApplicationResult<BudgetPlanResponse>.Conflict(
                "A budget plan already exists for this year and hotel unit.");
        }

        return ApplicationResult<BudgetPlanResponse>.Success(Map(plan));
    }

    public async Task<ApplicationResult<BudgetPlanResponse>> UpdatePlanAsync(
        Guid id,
        UpdateBudgetPlanRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await MutatePlanAsync(
            id,
            context,
            "budget.plan.updated",
            plan => plan.Rename(request.Label),
            plan => new { plan.Year, plan.HotelUnitCode, plan.Label },
            cancellationToken);
    }

    public async Task<ApplicationResult<BudgetPlanResponse>> ReplacePlanLinesAsync(
        Guid id,
        ReplaceBudgetLinesRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (request.Lines is null)
        {
            return ApplicationResult<BudgetPlanResponse>.Validation("Budget lines are required.");
        }

        return await MutatePlanAsync(
            id,
            context,
            "budget.plan.lines_replaced",
            plan => plan.ReplaceLines(BuildLines(request.Lines)),
            plan => new { plan.Year, plan.HotelUnitCode, LineCount = plan.Lines.Count, plan.TotalTarget },
            cancellationToken);
    }

    public async Task<ApplicationResult<BudgetPlanResponse>> SetPlanLineAsync(
        Guid id,
        BudgetLineRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await MutatePlanAsync(
            id,
            context,
            "budget.plan.line_set",
            plan => plan.SetLine(request.Month, request.Category, request.AmountTarget),
            plan => new
            {
                plan.Year,
                plan.HotelUnitCode,
                request.Month,
                Category = request.Category.ToString(),
                request.AmountTarget
            },
            cancellationToken);
    }

    public async Task<ApplicationResult<BudgetPlanResponse>> RemovePlanLineAsync(
        Guid id,
        Guid lineId,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var plan = await dbContext.Set<BudgetPlan>()
            .Include(current => current.Lines)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (plan is null)
        {
            return ApplicationResult<BudgetPlanResponse>.NotFound("Budget plan was not found.");
        }

        bool removed;

        try
        {
            removed = plan.RemoveLine(lineId);
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResult<BudgetPlanResponse>.Validation(ex.Message);
        }

        if (!removed)
        {
            return ApplicationResult<BudgetPlanResponse>.NotFound("Budget line was not found on this plan.");
        }

        plan.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "budget.plan.line_removed",
            "budgeting.budget_plans",
            plan.Id,
            context,
            new { plan.Year, plan.HotelUnitCode, LineId = lineId, LineCount = plan.Lines.Count },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<BudgetPlanResponse>.Success(Map(plan));
    }

    public async Task<ApplicationResult<BudgetPlanResponse>> ApprovePlanAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await MutatePlanAsync(
            id,
            context,
            "budget.plan.approved",
            plan => plan.Approve(context.UserName, DateTimeOffset.UtcNow),
            plan => new { plan.Year, plan.HotelUnitCode, plan.TotalTarget, LineCount = plan.Lines.Count },
            cancellationToken);
    }

    public async Task<ApplicationResult<BudgetPlanResponse>> ClosePlanAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await MutatePlanAsync(
            id,
            context,
            "budget.plan.closed",
            plan => plan.Close(context.UserName, DateTimeOffset.UtcNow),
            plan => new { plan.Year, plan.HotelUnitCode, Status = plan.Status.ToString() },
            cancellationToken);
    }

    public async Task<ApplicationResult<BudgetVarianceResponse>> GetVarianceAsync(
        int year,
        string hotelUnitCode,
        int? month,
        CancellationToken cancellationToken)
    {
        if (year is < MinimumYear or > MaximumYear)
        {
            return ApplicationResult<BudgetVarianceResponse>.Validation(
                $"Year must be between {MinimumYear} and {MaximumYear}.");
        }

        if (month.HasValue && month.Value is < 1 or > 12)
        {
            return ApplicationResult<BudgetVarianceResponse>.Validation("Month must be between 1 and 12.");
        }

        var normalizedUnitCode = NormalizeCodeOrEmpty(hotelUnitCode);

        if (string.IsNullOrWhiteSpace(normalizedUnitCode))
        {
            return ApplicationResult<BudgetVarianceResponse>.Validation("Hotel unit code is required.");
        }

        var unitExists = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .AnyAsync(current => current.Code == normalizedUnitCode, cancellationToken);

        if (!unitExists)
        {
            return ApplicationResult<BudgetVarianceResponse>.NotFound("Hotel unit was not found.");
        }

        var plan = await dbContext.Set<BudgetPlan>()
            .AsNoTracking()
            .Include(current => current.Lines)
            .SingleOrDefaultAsync(
                current => current.Year == year && current.HotelUnitCode == normalizedUnitCode,
                cancellationToken);

        // Without a budget there is nothing to compare the actual against, and answering with a
        // grid of zeroed targets would make a mistyped year look like a catastrophically
        // over-performing unit. The absence is reported as such instead.
        if (plan is null)
        {
            return ApplicationResult<BudgetVarianceResponse>.NotFound(
                "No budget plan exists for this year and hotel unit.");
        }

        var from = month.HasValue
            ? new DateOnly(year, month.Value, 1)
            : new DateOnly(year, 1, 1);

        var to = month.HasValue
            ? new DateOnly(year, month.Value, DateTime.DaysInMonth(year, month.Value))
            : new DateOnly(year, 12, 31);

        // ONLY VALIDATED REVENUE IS ACTUAL. A Draft entry has not been controlled yet, a Submitted
        // one is still awaiting that control, and a Rejected one was refused outright: none of the
        // three is money the establishment can claim to have made, and letting any of them in
        // would let an un-reviewed - or explicitly refused - figure close a budget gap on paper.
        // Same rule, same reason, as the treasury summary counting only Confirmed receipts. The
        // filter is applied here, in the query, so no code path can compute a variance without it.
        var actuals = await dbContext.Set<DailyRevenue>()
            .AsNoTracking()
            .Where(revenue =>
                revenue.HotelUnitCode == normalizedUnitCode &&
                revenue.Status == DailyRevenueStatus.Validated &&
                revenue.BusinessDate >= from &&
                revenue.BusinessDate <= to)
            .Select(revenue => new BudgetActualRevenue(
                revenue.BusinessDate,
                revenue.Accommodation,
                revenue.Food,
                revenue.Beverage,
                revenue.Other))
            .ToArrayAsync(cancellationToken);

        var targets = plan.Lines
            .Select(line => new BudgetTargetLine(line.Month, line.Category, line.AmountTarget))
            .ToArray();

        var report = new BudgetVarianceCalculator().Calculate(
            year,
            normalizedUnitCode,
            month,
            plan.Id,
            plan.Status,
            targets,
            actuals);

        return ApplicationResult<BudgetVarianceResponse>.Success(report);
    }

    /// <summary>
    /// Shared shape of every single-plan mutation: load with its lines, apply the change inside
    /// the entity - which is where the "an approved budget is frozen" invariant lives, so that no
    /// caller here can weaken it - then stamp the audit trail and save. A refused transition
    /// surfaces as the entity's own InvalidOperationException and is turned into a 400.
    /// </summary>
    private async Task<ApplicationResult<BudgetPlanResponse>> MutatePlanAsync(
        Guid id,
        OperationContext context,
        string auditAction,
        Action<BudgetPlan> change,
        Func<BudgetPlan, object> auditDetails,
        CancellationToken cancellationToken)
    {
        var plan = await dbContext.Set<BudgetPlan>()
            .Include(current => current.Lines)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (plan is null)
        {
            return ApplicationResult<BudgetPlanResponse>.NotFound("Budget plan was not found.");
        }

        try
        {
            change(plan);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<BudgetPlanResponse>.Validation(ex.Message);
        }

        plan.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        try
        {
            await WriteAuditAsync(
                auditAction,
                "budgeting.budget_plans",
                plan.Id,
                context,
                auditDetails(plan),
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // ux_budget_lines_plan_month_category: two concurrent writes targeting the same
            // (month, category) cell of the same plan.
            return ApplicationResult<BudgetPlanResponse>.Conflict(
                "The budget plan was modified by a concurrent operation. Please retry.");
        }

        return ApplicationResult<BudgetPlanResponse>.Success(Map(plan));
    }

    private static List<BudgetLine> BuildLines(IReadOnlyCollection<BudgetLineRequest> requests)
    {
        return requests
            .Select(line => new BudgetLine(line.Month, line.Category, line.AmountTarget))
            .ToList();
    }

    private static BudgetPlanResponse Map(BudgetPlan plan)
    {
        var lines = plan.Lines
            .OrderBy(line => line.Month)
            .ThenBy(line => line.Category)
            .Select(line => new BudgetLineResponse(line.Id, line.Month, line.Category, line.AmountTarget))
            .ToArray();

        return new BudgetPlanResponse(
            plan.Id,
            plan.Year,
            plan.HotelUnitCode,
            plan.Label,
            plan.Status,
            plan.TotalTarget,
            lines,
            plan.CanEdit,
            plan.ApprovedAt,
            plan.ApprovedBy,
            plan.ClosedAt,
            plan.ClosedBy,
            plan.CreatedAt,
            plan.CreatedBy,
            plan.UpdatedAt,
            plan.UpdatedBy);
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
    /// SaveChangesAsync internally (persisting the pending entity changes together with the audit
    /// row), so this call is usually a no-op - it exists so persistence never silently depends on
    /// the audit writer's internals.
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
