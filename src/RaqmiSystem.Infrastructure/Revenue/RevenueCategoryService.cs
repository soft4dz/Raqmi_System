using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Budgeting;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Infrastructure.Persistence;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Revenue;

public sealed class RevenueCategoryService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter) : IRevenueCategoryService
{
    private const string EntityName = "exploitation.revenue_categories";

    public async Task<ApplicationResult<IReadOnlyCollection<RevenueCategoryResponse>>> ListAsync(
        BusinessSector? sector,
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var filterBySector = sector.HasValue;
        var effectiveSector = sector;

        if (!string.IsNullOrWhiteSpace(hotelUnitCode))
        {
            var normalizedUnitCode = hotelUnitCode.Trim().ToUpperInvariant();

            var unit = await dbContext.HotelUnits
                .AsNoTracking()
                .SingleOrDefaultAsync(current => current.Code == normalizedUnitCode, cancellationToken);

            if (unit is null)
            {
                return ApplicationResult<IReadOnlyCollection<RevenueCategoryResponse>>.NotFound("Hotel unit was not found.");
            }

            // L'unité impose son secteur : un appelant qui donne les deux ne peut pas obtenir pour
            // une unité des catégories qui ne la concernent pas.
            effectiveSector = RevenueCategoryCatalog.SectorOf(unit.UnitType);
            filterBySector = true;
        }

        IQueryable<RevenueCategory> query = dbContext.Set<RevenueCategory>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(category => category.IsActive);
        }

        var categories = await query.ToArrayAsync(cancellationToken);

        IEnumerable<RevenueCategory> selected = filterBySector
            ? RevenueCategoryCatalog.Applicable(categories, effectiveSector)
            : categories
                .OrderBy(category => category.DisplayOrder)
                .ThenBy(category => category.Code, StringComparer.Ordinal);

        return ApplicationResult<IReadOnlyCollection<RevenueCategoryResponse>>.Success(
            selected.Select(Map).ToArray());
    }

    public async Task<ApplicationResult<RevenueCategoryResponse>> GetAsync(
        string code,
        CancellationToken cancellationToken)
    {
        if (!TryNormalize(code, out var normalized, out var error))
        {
            return ApplicationResult<RevenueCategoryResponse>.Validation(error);
        }

        var category = await dbContext.Set<RevenueCategory>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalized, cancellationToken);

        return category is null
            ? ApplicationResult<RevenueCategoryResponse>.NotFound("Revenue category was not found.")
            : ApplicationResult<RevenueCategoryResponse>.Success(Map(category));
    }

    public async Task<ApplicationResult<RevenueCategoryResponse>> CreateAsync(
        CreateRevenueCategoryRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        RevenueCategory category;

        try
        {
            category = new RevenueCategory(request.Code, request.Label, request.DisplayOrder, request.Sector);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RevenueCategoryResponse>.Validation(ex.Message);
        }

        var exists = await dbContext.Set<RevenueCategory>()
            .AnyAsync(current => current.Code == category.Code, cancellationToken);

        if (exists)
        {
            return ApplicationResult<RevenueCategoryResponse>.Conflict("A revenue category with this code already exists.");
        }

        dbContext.Set<RevenueCategory>().Add(category);

        try
        {
            await WriteAuditAsync(
                "exploitation.revenue_category.created",
                category.Code,
                context,
                new { category.Code, category.Label, category.DisplayOrder, Sector = category.Sector?.ToString() },
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Le contrôle d'existence et l'insertion ne sont pas atomiques : deux créations
            // simultanées du même code se départagent sur la clé primaire.
            return ApplicationResult<RevenueCategoryResponse>.Conflict("A revenue category with this code already exists.");
        }

        return ApplicationResult<RevenueCategoryResponse>.Success(Map(category));
    }

    public async Task<ApplicationResult<RevenueCategoryResponse>> UpdateAsync(
        string code,
        UpdateRevenueCategoryRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (!TryNormalize(code, out var normalized, out var error))
        {
            return ApplicationResult<RevenueCategoryResponse>.Validation(error);
        }

        var category = await dbContext.Set<RevenueCategory>()
            .SingleOrDefaultAsync(current => current.Code == normalized, cancellationToken);

        if (category is null)
        {
            return ApplicationResult<RevenueCategoryResponse>.NotFound("Revenue category was not found.");
        }

        try
        {
            category.Update(request.Label, request.DisplayOrder, request.Sector, request.IsActive);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RevenueCategoryResponse>.Validation(ex.Message);
        }

        await WriteAuditAsync(
            "exploitation.revenue_category.updated",
            category.Code,
            context,
            new { category.Code, category.Label, category.DisplayOrder, Sector = category.Sector?.ToString(), category.IsActive },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RevenueCategoryResponse>.Success(Map(category));
    }

    public async Task<ApplicationResult<bool>> DeleteAsync(
        string code,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (!TryNormalize(code, out var normalized, out var error))
        {
            return ApplicationResult<bool>.Validation(error);
        }

        var category = await dbContext.Set<RevenueCategory>()
            .SingleOrDefaultAsync(current => current.Code == normalized, cancellationToken);

        if (category is null)
        {
            return ApplicationResult<bool>.NotFound("Revenue category was not found.");
        }

        // Une catégorie déjà utilisée porte de l'historique : la retirer effacerait le sens de
        // lignes enregistrées. Le refus arrive ici, avec un message, plutôt que sous la forme d'une
        // violation de clé étrangère après l'aller-retour.
        var referencedByRevenue = await dbContext.Set<DailyRevenueLine>()
            .AnyAsync(line => line.CategoryCode == normalized, cancellationToken);

        var referencedByBudget = await dbContext.Set<BudgetLine>()
            .AnyAsync(line => line.Category == normalized, cancellationToken);

        if (referencedByRevenue || referencedByBudget)
        {
            return ApplicationResult<bool>.Conflict(
                "This revenue category is referenced by recorded revenue or budget lines: deactivate it instead of deleting it.");
        }

        dbContext.Set<RevenueCategory>().Remove(category);

        await WriteAuditAsync(
            "exploitation.revenue_category.deleted",
            category.Code,
            context,
            new { category.Code, category.Label },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<bool>.Success(true);
    }

    private static bool TryNormalize(string code, out string normalized, out string error)
    {
        try
        {
            normalized = RevenueCategoryCodes.Normalize(code);
            error = string.Empty;
            return true;
        }
        catch (ArgumentException ex)
        {
            normalized = string.Empty;
            error = ex.Message;
            return false;
        }
    }

    private static RevenueCategoryResponse Map(RevenueCategory category)
    {
        return new RevenueCategoryResponse(
            category.Code,
            category.Label,
            category.DisplayOrder,
            category.IsActive,
            category.Sector);
    }

    private async Task WriteAuditAsync(
        string action,
        string code,
        OperationContext context,
        object details,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                EntityName,
                code,
                context.IpAddress,
                JsonSerializer.Serialize(details)),
            cancellationToken);
    }
}
