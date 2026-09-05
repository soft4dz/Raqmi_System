using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Closing;
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
    IAuditLogWriter auditLogWriter,
    IDailyClosingReadService dailyClosingReadService) : IDailyRevenueService
{
    private const string BusinessDayClosedMessage = "The business day is closed for this hotel unit.";

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

        var categories = await LoadCategoriesAsync(cancellationToken);

        return rows.Select(row => Map(row.Revenue, row.UnitName, categories)).ToArray();
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

        return ApplicationResult<DailyRevenueResponse>.Success(await MapWithUnitNameAsync(revenue, cancellationToken));
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

        if (await dailyClosingReadService.IsClosedAsync(request.BusinessDate, normalizedUnitCode, cancellationToken))
        {
            return ApplicationResult<DailyRevenueResponse>.Validation(BusinessDayClosedMessage);
        }

        var exists = await dbContext.DailyRevenues.AnyAsync(
            current => current.BusinessDate == request.BusinessDate && current.HotelUnitCode == normalizedUnitCode,
            cancellationToken);

        if (exists)
        {
            return ApplicationResult<DailyRevenueResponse>.Conflict("Daily revenue already exists for this date and hotel unit.");
        }

        var categories = await LoadCategoriesAsync(cancellationToken);

        DailyRevenue revenue;

        try
        {
            var requested = DailyRevenueRequestLines.Resolve(
                request.Lines,
                request.Accommodation,
                request.Food,
                request.Beverage,
                request.Other);

            var lines = BuildLines(requested, categories, alreadyCarried: new HashSet<string>(StringComparer.Ordinal), out var error);

            if (error is not null)
            {
                return ApplicationResult<DailyRevenueResponse>.Validation(error);
            }

            revenue = new DailyRevenue(request.BusinessDate, normalizedUnitCode, lines, request.Notes);
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
            new { revenue.BusinessDate, revenue.HotelUnitCode, revenue.Total, LineCount = revenue.Lines.Count },
            cancellationToken);

        return ApplicationResult<DailyRevenueResponse>.Success(Map(revenue, unit.Name, categories));
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

        if (await dailyClosingReadService.IsClosedAsync(revenue.BusinessDate, revenue.HotelUnitCode, cancellationToken))
        {
            return ApplicationResult<DailyRevenueResponse>.Validation(BusinessDayClosedMessage);
        }

        var categories = await LoadCategoriesAsync(cancellationToken);

        try
        {
            var requested = DailyRevenueRequestLines.Resolve(
                request.Lines,
                request.Accommodation,
                request.Food,
                request.Beverage,
                request.Other);

            // Une catégorie désactivée depuis la saisie reste modifiable sur la recette qui la
            // porte déjà : la désactivation ferme l'avenir, pas le passé.
            var alreadyCarried = revenue.Lines.Select(line => line.CategoryCode).ToHashSet(StringComparer.Ordinal);

            var lines = BuildLines(requested, categories, alreadyCarried, out var error);

            if (error is not null)
            {
                return ApplicationResult<DailyRevenueResponse>.Validation(error);
            }

            revenue.UpdateLines(lines, request.Notes);
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
            new { revenue.BusinessDate, revenue.HotelUnitCode, revenue.Total, LineCount = revenue.Lines.Count, Status = revenue.Status.ToString() },
            cancellationToken);

        return ApplicationResult<DailyRevenueResponse>.Success(await MapWithUnitNameAsync(revenue, cancellationToken, categories));
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

        var categories = await LoadCategoriesAsync(cancellationToken);

        // Seules les catégories effectivement présentes dans les recettes retenues figurent dans
        // la synthèse : une synthèse d'hôtel n'a pas à afficher les catégories d'un négociant à zéro.
        var codesInRows = rows
            .SelectMany(row => row.Lines)
            .Select(line => line.CategoryCode)
            .ToHashSet(StringComparer.Ordinal);

        var presentCategories = categories.Values
            .Where(category => codesInRows.Contains(category.Code))
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Code, StringComparer.Ordinal)
            .ToArray();

        var drafts = rows.Select(row => new DailyRevenueDraft(
            row.BusinessDate,
            row.HotelUnitCode,
            row.Lines.Select(line => new DailyRevenueLineRequest(line.CategoryCode, line.Amount)).ToArray()));

        var totals = new RevenueSummaryService().Calculate(drafts, presentCategories);

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
            totals.Accommodation,
            totals.Food,
            totals.Beverage,
            totals.Other,
            totals.Total)
        {
            Categories = totals.Categories
        };

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

        if (await dailyClosingReadService.IsClosedAsync(revenue.BusinessDate, revenue.HotelUnitCode, cancellationToken))
        {
            return ApplicationResult<DailyRevenueResponse>.Validation(BusinessDayClosedMessage);
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

    /// <summary>
    /// Transforme les montants demandés en lignes du domaine après contrôle du paramétrage :
    /// une catégorie inconnue ou désactivée est refusée avec un message qui la nomme (400),
    /// plutôt que par une violation de clé étrangère après l'aller-retour. Les montants nuls ne
    /// sont pas contrôlés au-delà de leur signe : le domaine ne crée aucune ligne pour eux, et
    /// l'ancien corps à quatre montants en envoie toujours pour les catégories non renseignées.
    /// </summary>
    private static List<DailyRevenueLine> BuildLines(
        IReadOnlyList<DailyRevenueLineRequest> requested,
        IReadOnlyDictionary<string, RevenueCategory> categories,
        IReadOnlySet<string> alreadyCarried,
        out string? error)
    {
        error = null;
        var lines = new List<DailyRevenueLine>(requested.Count);

        foreach (var request in requested)
        {
            if (request.Amount < 0m)
            {
                error = $"Amount for revenue category '{request.CategoryCode}' cannot be negative.";
                return lines;
            }

            var code = RevenueCategoryCodes.Normalize(request.CategoryCode, nameof(requested));

            if (request.Amount == 0m)
            {
                continue;
            }

            if (!categories.TryGetValue(code, out var category))
            {
                error = $"Revenue category '{code}' is unknown.";
                return lines;
            }

            if (!category.IsActive && !alreadyCarried.Contains(code))
            {
                error = $"Revenue category '{code}' is inactive and cannot receive new revenue.";
                return lines;
            }

            lines.Add(new DailyRevenueLine(code, request.Amount));
        }

        return lines;
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

    private async Task<IReadOnlyDictionary<string, RevenueCategory>> LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = await dbContext.Set<RevenueCategory>()
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        return categories.ToDictionary(category => category.Code, StringComparer.Ordinal);
    }

    private async Task<DailyRevenueResponse> MapWithUnitNameAsync(
        DailyRevenue revenue,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, RevenueCategory>? categories = null)
    {
        var unitName = await dbContext.HotelUnits
            .AsNoTracking()
            .Where(unit => unit.Code == revenue.HotelUnitCode)
            .Select(unit => unit.Name)
            .SingleOrDefaultAsync(cancellationToken);

        return Map(revenue, unitName, categories ?? await LoadCategoriesAsync(cancellationToken));
    }

    private static DailyRevenueResponse Map(
        DailyRevenue revenue,
        string? unitName,
        IReadOnlyDictionary<string, RevenueCategory> categories)
    {
        // Ordre d'affichage du paramétrage ; un code sans catégorie (désactivée puis supprimée
        // en base par un tiers) reste rendu, libellé par son code, en fin de liste.
        var lines = revenue.Lines
            .OrderBy(line => categories.TryGetValue(line.CategoryCode, out var category) ? category.DisplayOrder : int.MaxValue)
            .ThenBy(line => line.CategoryCode, StringComparer.Ordinal)
            .Select(line => new DailyRevenueLineResponse(
                line.CategoryCode,
                categories.TryGetValue(line.CategoryCode, out var category) ? category.Label : line.CategoryCode,
                line.Amount))
            .ToArray();

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
            revenue.UpdatedBy,
            lines);
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
