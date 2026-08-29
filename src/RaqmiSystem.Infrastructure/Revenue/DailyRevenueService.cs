using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Infrastructure.Persistence;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Revenue;

public sealed class DailyRevenueService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter) : IDailyRevenueService
{
    public async Task<IReadOnlyCollection<DailyRevenueResponse>> ListAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        DailyRevenueStatus? status,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilters(
            dbContext.DailyRevenues.AsNoTracking(),
            from,
            to,
            hotelUnitCode,
            status);

        var rows = await query
            .GroupJoin(
                dbContext.HotelUnits.AsNoTracking(),
                revenue => revenue.HotelUnitCode,
                unit => unit.Code,
                (revenue, units) => new { Revenue = revenue, UnitName = units.Select(unit => unit.Name).FirstOrDefault() })
            .OrderByDescending(row => row.Revenue.BusinessDate)
            .ThenBy(row => row.Revenue.HotelUnitCode)
            .ToArrayAsync(cancellationToken);

        return rows.Select(row => Map(row.Revenue, row.UnitName)).ToArray();
    }

    public async Task<ApplicationResult<DailyRevenueResponse>> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var revenue = await dbContext.DailyRevenues
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (revenue is null)
        {
            return ApplicationResult<DailyRevenueResponse>.NotFound("Daily revenue entry was not found.");
        }

        var unitName = await dbContext.HotelUnits
            .AsNoTracking()
            .Where(unit => unit.Code == revenue.HotelUnitCode)
            .Select(unit => unit.Name)
            .SingleOrDefaultAsync(cancellationToken);

        return ApplicationResult<DailyRevenueResponse>.Success(Map(revenue, unitName));
    }

    public async Task<ApplicationResult<DailyRevenueResponse>> CreateAsync(
        CreateDailyRevenueRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedUnitCode = NormalizeCodeOrEmpty(request.HotelUnitCode);

        if (string.IsNullOrWhiteSpace(normalizedUnitCode))
        {
            return ApplicationResult<DailyRevenueResponse>.Validation("Hotel unit code is required.");
        }

        var unit = await dbContext.HotelUnits
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedUnitCode, cancellationToken);

        if (unit is null)
        {
            return ApplicationResult<DailyRevenueResponse>.NotFound("Hotel unit was not found.");
        }

        if (!unit.IsActive)
        {
            return ApplicationResult<DailyRevenueResponse>.Validation("Daily revenue cannot be created for an inactive hotel unit.");
        }

        var exists = await dbContext.DailyRevenues.AnyAsync(
            current => current.BusinessDate == request.BusinessDate && current.HotelUnitCode == normalizedUnitCode,
            cancellationToken);

        if (exists)
        {
            return ApplicationResult<DailyRevenueResponse>.Conflict("Daily revenue already exists for this date and hotel unit.");
        }

        DailyRevenue revenue;

        try
        {
            revenue = new DailyRevenue(
                request.BusinessDate,
                normalizedUnitCode,
                request.Accommodation,
                request.Food,
                request.Beverage,
                request.Other,
                request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<DailyRevenueResponse>.Validation(ex.Message);
        }

        var now = DateTimeOffset.UtcNow;
        revenue.MarkCreated(context.UserName, now);
        dbContext.DailyRevenues.Add(revenue);

        await WriteAuditAsync(
            "exploitation.daily_revenue.created",
            revenue,
            context,
            new { revenue.BusinessDate, revenue.HotelUnitCode, revenue.Total },
            cancellationToken);

        return ApplicationResult<DailyRevenueResponse>.Success(Map(revenue, unit.Name));
    }

    public async Task<ApplicationResult<DailyRevenueResponse>> UpdateAsync(
        Guid id,
        UpdateDailyRevenueRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var revenue = await dbContext.DailyRevenues
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (revenue is null)
        {
            return ApplicationResult<DailyRevenueResponse>.NotFound("Daily revenue entry was not found.");
        }

        try
        {
            revenue.UpdateAmounts(
                request.Accommodation,
                request.Food,
                request.Beverage,
                request.Other,
                request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<DailyRevenueResponse>.Validation(ex.Message);
        }

        revenue.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "exploitation.daily_revenue.updated",
            revenue,
            context,
            new { revenue.BusinessDate, revenue.HotelUnitCode, revenue.Total, Status = revenue.Status.ToString() },
            cancellationToken);

        return ApplicationResult<DailyRevenueResponse>.Success(await MapWithUnitNameAsync(revenue, cancellationToken));
    }

    public async Task<ApplicationResult<DailyRevenueResponse>> SubmitAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await ChangeStatusAsync(
            id,
            context,
            "exploitation.daily_revenue.submitted",
            revenue => revenue.Submit(context.UserName, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    public async Task<ApplicationResult<DailyRevenueResponse>> ValidateAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await ChangeStatusAsync(
            id,
            context,
            "exploitation.daily_revenue.validated",
            revenue => revenue.Validate(context.UserName, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    public async Task<ApplicationResult<DailyRevenueResponse>> RejectAsync(
        Guid id,
        RejectDailyRevenueRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return await ChangeStatusAsync(
            id,
            context,
            "exploitation.daily_revenue.rejected",
            revenue => revenue.Reject(request.Reason, context.UserName, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    public async Task<ApplicationResult<DailyRevenueSummaryResponse>> GetSummaryAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        DailyRevenueStatus? status,
        CancellationToken cancellationToken)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return ApplicationResult<DailyRevenueSummaryResponse>.Validation("The from date cannot be after the to date.");
        }

        var rows = await ApplyFilters(
                dbContext.DailyRevenues.AsNoTracking(),
                from,
                to,
                hotelUnitCode,
                status)
            .ToArrayAsync(cancellationToken);

        var accommodation = rows.Sum(row => row.Accommodation);
        var food = rows.Sum(row => row.Food);
        var beverage = rows.Sum(row => row.Beverage);
        var other = rows.Sum(row => row.Other);

        var summary = new DailyRevenueSummaryResponse(
            from,
            to,
            NormalizeNullableCode(hotelUnitCode),
            status,
            rows.Length,
            rows.Count(row => row.Status == DailyRevenueStatus.Draft),
            rows.Count(row => row.Status == DailyRevenueStatus.Submitted),
            rows.Count(row => row.Status == DailyRevenueStatus.Validated),
            rows.Count(row => row.Status == DailyRevenueStatus.Rejected),
            accommodation,
            food,
            beverage,
            other,
            accommodation + food + beverage + other);

        return ApplicationResult<DailyRevenueSummaryResponse>.Success(summary);
    }

    public async Task<UnitDashboardResponse> GetUnitDashboardAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        var activeUnits = await dbContext.HotelUnits
            .AsNoTracking()
            .Where(unit => unit.IsActive)
            .ToArrayAsync(cancellationToken);

        var revenuesForDate = await dbContext.DailyRevenues
            .AsNoTracking()
            .Where(revenue => revenue.BusinessDate == businessDate)
            .ToArrayAsync(cancellationToken);

        // HotelUnit.IsActive only reflects the current state - there is no activation/deactivation
        // timestamp - so filtering strictly by "currently active" silently drops any revenue entry
        // recorded for a unit that has since been deactivated (the row still exists in the database,
        // FK is Restrict, but UnitDashboardCalculator only loops over the units it is given). Widen
        // the unit roster with any unit referenced by a revenue row for this date, even if it is no
        // longer active, so the dashboard's GrandTotal/UnitsWithEntry never silently under-report a
        // real recorded revenue. This does not retroactively fix units created/activated after
        // businessDate still showing as "missing" for that date - that would require persisting an
        // activation history on HotelUnit, which is out of scope here.
        var activeUnitCodes = activeUnits.Select(unit => unit.Code).ToHashSet();
        var missingUnitCodes = revenuesForDate
            .Select(revenue => revenue.HotelUnitCode)
            .Distinct()
            .Where(code => !activeUnitCodes.Contains(code))
            .ToArray();

        var units = activeUnits;

        if (missingUnitCodes.Length > 0)
        {
            var inactiveUnitsWithEntries = await dbContext.HotelUnits
                .AsNoTracking()
                .Where(unit => missingUnitCodes.Contains(unit.Code))
                .ToArrayAsync(cancellationToken);

            units = activeUnits.Concat(inactiveUnitsWithEntries).ToArray();
        }

        return new UnitDashboardCalculator().Build(businessDate, units, revenuesForDate);
    }

    private async Task<ApplicationResult<DailyRevenueResponse>> ChangeStatusAsync(
        Guid id,
        OperationContext context,
        string auditAction,
        Action<DailyRevenue> change,
        CancellationToken cancellationToken)
    {
        var revenue = await dbContext.DailyRevenues
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (revenue is null)
        {
            return ApplicationResult<DailyRevenueResponse>.NotFound("Daily revenue entry was not found.");
        }

        try
        {
            change(revenue);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ApplicationResult<DailyRevenueResponse>.Validation(ex.Message);
        }

        revenue.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            auditAction,
            revenue,
            context,
            new { revenue.BusinessDate, revenue.HotelUnitCode, Status = revenue.Status.ToString(), revenue.RejectionReason },
            cancellationToken);

        return ApplicationResult<DailyRevenueResponse>.Success(await MapWithUnitNameAsync(revenue, cancellationToken));
    }

    private static IQueryable<DailyRevenue> ApplyFilters(
        IQueryable<DailyRevenue> query,
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        DailyRevenueStatus? status)
    {
        if (from.HasValue)
        {
            query = query.Where(revenue => revenue.BusinessDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(revenue => revenue.BusinessDate <= to.Value);
        }

        var normalizedUnitCode = NormalizeNullableCode(hotelUnitCode);

        if (!string.IsNullOrWhiteSpace(normalizedUnitCode))
        {
            query = query.Where(revenue => revenue.HotelUnitCode == normalizedUnitCode);
        }

        if (status.HasValue)
        {
            query = query.Where(revenue => revenue.Status == status.Value);
        }

        return query;
    }

    private async Task<DailyRevenueResponse> MapWithUnitNameAsync(
        DailyRevenue revenue,
        CancellationToken cancellationToken)
    {
        var unitName = await dbContext.HotelUnits
            .AsNoTracking()
            .Where(unit => unit.Code == revenue.HotelUnitCode)
            .Select(unit => unit.Name)
            .SingleOrDefaultAsync(cancellationToken);

        return Map(revenue, unitName);
    }

    private static DailyRevenueResponse Map(DailyRevenue revenue, string? unitName)
    {
        return new DailyRevenueResponse(
            revenue.Id,
            revenue.BusinessDate,
            revenue.HotelUnitCode,
            unitName,
            revenue.Accommodation,
            revenue.Food,
            revenue.Beverage,
            revenue.Other,
            revenue.Total,
            revenue.Notes,
            revenue.Status,
            revenue.CanEdit,
            revenue.SubmittedAt,
            revenue.SubmittedBy,
            revenue.ValidatedAt,
            revenue.ValidatedBy,
            revenue.RejectionReason,
            revenue.CreatedAt,
            revenue.CreatedBy,
            revenue.UpdatedAt,
            revenue.UpdatedBy);
    }

    private static string NormalizeCodeOrEmpty(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
    }

    private static string? NormalizeNullableCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
    }

    private async Task WriteAuditAsync(
        string action,
        DailyRevenue revenue,
        OperationContext context,
        object details,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                "exploitation.daily_revenues",
                revenue.Id.ToString(),
                context.IpAddress,
                JsonSerializer.Serialize(details)),
            cancellationToken);
    }
}
