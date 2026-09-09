using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Fiscalite;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Fiscalite;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Fiscalite;

/// <summary>
/// Annual fiscal return ("liasse fiscale"). Aggregates the year's registers, then delegates the
/// actual line generation to the pure domain factories - this class only owns persistence,
/// referential aggregation and the upsert-by-(Year,Kind) replacement.
/// </summary>
public sealed class FiscalReturnService(RaqmiDbContext dbContext, IAuditLogWriter auditLogWriter) : IFiscalReturnService
{
    private const string FiscalReturnEntity = "fiscalite.fiscal_returns";

    public async Task<IReadOnlyCollection<FiscalReturnResponse>> GetAsync(int year, CancellationToken cancellationToken)
    {
        var returns = await dbContext.Set<FiscalReturn>()
            .AsNoTracking()
            .Include(fiscalReturn => fiscalReturn.Lines)
            .Where(fiscalReturn => fiscalReturn.Year == year)
            .ToArrayAsync(cancellationToken);

        return returns.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<FiscalReturnResponse>> GenerateSimpleAsync(
        int year, OperationContext context, CancellationToken cancellationToken)
    {
        var (caHt, tvaCollectee, _, _) = await AggregateYearAsync(year, cancellationToken);
        var fiscalReturn = FiscalReturn.GenerateSimple(year, caHt, tvaCollectee);

        return await ReplaceAndSaveAsync(fiscalReturn, context, cancellationToken);
    }

    public async Task<ApplicationResult<FiscalReturnResponse>> GenerateAdvancedAsync(
        int year, OperationContext context, CancellationToken cancellationToken)
    {
        var (caHt, tvaCollectee, achatsHt, tvaDeductible) = await AggregateYearAsync(year, cancellationToken);

        // Credit de TVA anterieur cumule : somme des soldes negatifs de toutes les declarations
        // mensuelles calculees de l'exercice - la meme donnee que celle deja tenue mois par mois.
        var creditTvaAnterieurCumule = await dbContext.Set<VatDeclaration>()
            .Where(declaration => declaration.Year == year && declaration.Solde < 0)
            .SumAsync(declaration => (decimal?)-declaration.Solde, cancellationToken) ?? 0m;

        var fiscalReturn = FiscalReturn.GenerateAdvanced(year, caHt, achatsHt, tvaCollectee, tvaDeductible, creditTvaAnterieurCumule);

        return await ReplaceAndSaveAsync(fiscalReturn, context, cancellationToken);
    }

    private async Task<(decimal CaHt, decimal TvaCollectee, decimal AchatsHt, decimal TvaDeductible)> AggregateYearAsync(
        int year, CancellationToken cancellationToken)
    {
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        var caHt = await dbContext.Set<VatSalesRegisterEntry>()
            .Where(entry => entry.PieceDate >= yearStart && entry.PieceDate <= yearEnd)
            .SumAsync(entry => (decimal?)entry.BaseHt, cancellationToken) ?? 0m;

        var tvaCollectee = await dbContext.Set<VatSalesRegisterEntry>()
            .Where(entry => entry.PieceDate >= yearStart && entry.PieceDate <= yearEnd)
            .SumAsync(entry => (decimal?)entry.VatAmount, cancellationToken) ?? 0m;

        var achatsHt = await dbContext.Set<VatPurchaseRegisterEntry>()
            .Where(entry => entry.PieceDate >= yearStart && entry.PieceDate <= yearEnd)
            .SumAsync(entry => (decimal?)entry.BaseHt, cancellationToken) ?? 0m;

        var tvaDeductible = await dbContext.Set<VatPurchaseRegisterEntry>()
            .Where(entry => entry.PieceDate >= yearStart && entry.PieceDate <= yearEnd)
            .SumAsync(entry => (decimal?)entry.VatAmount, cancellationToken) ?? 0m;

        return (caHt, tvaCollectee, achatsHt, tvaDeductible);
    }

    private async Task<ApplicationResult<FiscalReturnResponse>> ReplaceAndSaveAsync(
        FiscalReturn fiscalReturn, OperationContext context, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Set<FiscalReturn>()
            .SingleOrDefaultAsync(current => current.Year == fiscalReturn.Year && current.Kind == fiscalReturn.Kind, cancellationToken);

        if (existing is not null)
        {
            dbContext.Set<FiscalReturn>().Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        fiscalReturn.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<FiscalReturn>().Add(fiscalReturn);

        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId, context.UserName, "fiscalite.fiscal_return.generated", FiscalReturnEntity,
                fiscalReturn.Id.ToString(), context.IpAddress,
                JsonSerializer.Serialize(new { fiscalReturn.Year, Kind = fiscalReturn.Kind.ToString() })),
            cancellationToken);

        return ApplicationResult<FiscalReturnResponse>.Success(Map(fiscalReturn));
    }

    private static FiscalReturnResponse Map(FiscalReturn fiscalReturn) => new(
        fiscalReturn.Id, fiscalReturn.Year, fiscalReturn.Kind, fiscalReturn.GeneratedAt,
        fiscalReturn.Lines.Select(line => new FiscalReturnLineResponse(line.Code, line.Label, line.Amount)).ToArray());
}
