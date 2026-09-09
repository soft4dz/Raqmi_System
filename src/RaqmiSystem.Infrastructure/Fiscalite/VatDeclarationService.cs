using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Fiscalite;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Fiscalite;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Fiscalite;

/// <summary>
/// Monthly VAT declaration and G50 export. <see cref="CalculateAsync"/> is a pure aggregation of
/// the two registers for the period - it never invents data the registers do not have (a period
/// with no register lines yields a zero declaration, on purpose: see the legacy troubleshooting
/// note "Déclaration TVA à 0").
/// </summary>
public sealed class VatDeclarationService(RaqmiDbContext dbContext, IAuditLogWriter auditLogWriter) : IVatDeclarationService
{
    private const string DeclarationsEntity = "fiscalite.vat_declarations";
    private const string TeleDeclarationsEntity = "fiscalite.tele_declarations";

    public async Task<IReadOnlyCollection<VatDeclarationResponse>> GetHistoryAsync(CancellationToken cancellationToken)
    {
        var declarations = await dbContext.Set<VatDeclaration>()
            .AsNoTracking()
            .OrderByDescending(d => d.Year).ThenByDescending(d => d.Month)
            .ToArrayAsync(cancellationToken);

        return declarations.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<VatDeclarationResponse>> CalculateAsync(
        int year, int month, OperationContext context, CancellationToken cancellationToken)
    {
        var result = await CalculateInternalAsync(year, month, context, cancellationToken);

        return result.Succeeded && result.Value is not null
            ? ApplicationResult<VatDeclarationResponse>.Success(Map(result.Value))
            : Fail<VatDeclaration, VatDeclarationResponse>(result);
    }

    public async Task<IReadOnlyCollection<TeleDeclarationResponse>> GetTeleDeclarationsAsync(CancellationToken cancellationToken)
    {
        var teleDeclarations = await dbContext.Set<TeleDeclaration>()
            .AsNoTracking()
            .OrderByDescending(t => t.ExportedAt)
            .ToArrayAsync(cancellationToken);

        return teleDeclarations.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<G50ExportResult>> ExportG50Async(
        int year, int month, OperationContext context, CancellationToken cancellationToken)
    {
        var declarationResult = await CalculateInternalAsync(year, month, context, cancellationToken);

        if (!declarationResult.Succeeded || declarationResult.Value is null)
        {
            return Fail<VatDeclaration, G50ExportResult>(declarationResult);
        }

        var declaration = declarationResult.Value;
        declaration.MarkExported();

        var teleDeclaration = new TeleDeclaration(declaration.Id, TeleDeclarationType.Tva, year, month, declaration.Solde);
        teleDeclaration.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<TeleDeclaration>().Add(teleDeclaration);

        var csv = BuildG50Csv(declaration);

        await WriteAuditAsync(
            "fiscalite.teledeclaration.exported", TeleDeclarationsEntity, teleDeclaration.Id, context,
            new { Year = year, Month = month, teleDeclaration.Amount }, cancellationToken);

        return ApplicationResult<G50ExportResult>.Success(new G50ExportResult(Map(teleDeclaration), csv));
    }

    public async Task<ApplicationResult<TeleDeclarationResponse>> MarkDeclaredAsync(
        Guid teleDeclarationId, string dgiReference, OperationContext context, CancellationToken cancellationToken)
    {
        var teleDeclaration = await dbContext.Set<TeleDeclaration>()
            .SingleOrDefaultAsync(t => t.Id == teleDeclarationId, cancellationToken);

        if (teleDeclaration is null)
        {
            return ApplicationResult<TeleDeclarationResponse>.NotFound("Tele-declaration was not found.");
        }

        try
        {
            teleDeclaration.MarkDeclared(dgiReference);
        }
        catch (ArgumentException ex)
        {
            return ApplicationResult<TeleDeclarationResponse>.Validation(ex.Message);
        }

        // Repercute sur la declaration TVA correspondante, comme le fait l'ancien produit pour
        // typeDecl = 'tva' (seul type existant ici).
        if (teleDeclaration.Type == TeleDeclarationType.Tva)
        {
            var vatDeclaration = await dbContext.Set<VatDeclaration>()
                .SingleOrDefaultAsync(d => d.Id == teleDeclaration.VatDeclarationId, cancellationToken);

            if (vatDeclaration is not null && vatDeclaration.Status != DeclarationStatus.Declaree)
            {
                vatDeclaration.MarkDeclared(dgiReference);
            }
        }

        await WriteAuditAsync(
            "fiscalite.teledeclaration.declared", TeleDeclarationsEntity, teleDeclaration.Id, context,
            new { DgiReference = dgiReference }, cancellationToken);

        return ApplicationResult<TeleDeclarationResponse>.Success(Map(teleDeclaration));
    }

    private async Task<ApplicationResult<VatDeclaration>> CalculateInternalAsync(
        int year, int month, OperationContext context, CancellationToken cancellationToken)
    {
        if (month is < 1 or > 12)
        {
            return ApplicationResult<VatDeclaration>.Validation("Month must be between 1 and 12.");
        }

        var existing = await dbContext.Set<VatDeclaration>()
            .SingleOrDefaultAsync(d => d.Year == year && d.Month == month, cancellationToken);

        if (existing?.Status == DeclarationStatus.Declaree)
        {
            return ApplicationResult<VatDeclaration>.Conflict(
                "This period has already been declared to the DGI and cannot be recalculated.");
        }

        var (start, end) = PeriodBounds(year, month);

        var baseHtVentes = await dbContext.Set<VatSalesRegisterEntry>()
            .Where(entry => entry.PieceDate >= start && entry.PieceDate <= end)
            .SumAsync(entry => (decimal?)entry.BaseHt, cancellationToken) ?? 0m;

        var tvaCollectee = await dbContext.Set<VatSalesRegisterEntry>()
            .Where(entry => entry.PieceDate >= start && entry.PieceDate <= end)
            .SumAsync(entry => (decimal?)entry.VatAmount, cancellationToken) ?? 0m;

        var tvaDeductible = await dbContext.Set<VatPurchaseRegisterEntry>()
            .Where(entry => entry.PieceDate >= start && entry.PieceDate <= end)
            .SumAsync(entry => (decimal?)entry.VatAmount, cancellationToken) ?? 0m;

        var previous = await dbContext.Set<VatDeclaration>()
            .Where(d => d.Year < year || (d.Year == year && d.Month < month))
            .OrderByDescending(d => d.Year).ThenByDescending(d => d.Month)
            .FirstOrDefaultAsync(cancellationToken);

        // Le prealable existant est efface et rejoue ci-dessous dans un SaveChangesAsync SEPARE
        // du nouvel insert : les deux portent la meme cle unique (Year, Month), et un delete+insert
        // dans le meme lot de commandes risquerait un ordre d'execution qui viole temporairement
        // la contrainte.
        if (existing is not null)
        {
            var relatedTeleDeclarations = await dbContext.Set<TeleDeclaration>()
                .Where(t => t.VatDeclarationId == existing.Id)
                .ToArrayAsync(cancellationToken);
            dbContext.Set<TeleDeclaration>().RemoveRange(relatedTeleDeclarations);
            dbContext.Set<VatDeclaration>().Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var declaration = VatDeclaration.Calculate(year, month, baseHtVentes, tvaCollectee, tvaDeductible, previous?.Solde);
        declaration.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<VatDeclaration>().Add(declaration);

        await WriteAuditAsync(
            "fiscalite.vat_declaration.calculated", DeclarationsEntity, declaration.Id, context,
            new { Year = year, Month = month, declaration.Solde }, cancellationToken);

        return ApplicationResult<VatDeclaration>.Success(declaration);
    }

    /// <summary>Fixed 6-field format, a preparation of the G50 form - not a direct electronic deposit.</summary>
    private static string BuildG50Csv(VatDeclaration declaration)
    {
        var builder = new StringBuilder();
        builder.AppendLine("BASE_HT_VENTES,TVA_COLLECTEE,TVA_DEDUCTIBLE,CREDIT_ANTERIEUR,SOLDE_A_PAYER");
        builder.AppendLine(string.Join(',',
            declaration.BaseHtVentes.ToString("F2"),
            declaration.TvaCollectee.ToString("F2"),
            declaration.TvaDeductible.ToString("F2"),
            declaration.CreditAnterieur.ToString("F2"),
            declaration.Solde.ToString("F2")));
        return builder.ToString();
    }

    private static (DateOnly Start, DateOnly End) PeriodBounds(int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        return (start, start.AddMonths(1).AddDays(-1));
    }

    private static VatDeclarationResponse Map(VatDeclaration declaration) => new(
        declaration.Id, declaration.Year, declaration.Month, declaration.BaseHtVentes, declaration.TvaCollectee,
        declaration.TvaDeductible, declaration.CreditAnterieur, declaration.Solde, declaration.Status, declaration.DgiReference);

    private static TeleDeclarationResponse Map(TeleDeclaration teleDeclaration) => new(
        teleDeclaration.Id, teleDeclaration.VatDeclarationId, teleDeclaration.Type, teleDeclaration.Year, teleDeclaration.Month,
        teleDeclaration.Amount, teleDeclaration.Status, teleDeclaration.DgiReference, teleDeclaration.ExportedAt);

    private static ApplicationResult<TOut> Fail<TIn, TOut>(ApplicationResult<TIn> source) => source.ErrorType switch
    {
        ApplicationErrorType.NotFound => ApplicationResult<TOut>.NotFound(source.Error ?? "Not found."),
        ApplicationErrorType.Conflict => ApplicationResult<TOut>.Conflict(source.Error ?? "Conflict."),
        _ => ApplicationResult<TOut>.Validation(source.Error ?? "Validation error."),
    };

    private async Task WriteAuditAsync(
        string action, string entityName, Guid entityId, OperationContext context, object details, CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(context.UserId, context.UserName, action, entityName, entityId.ToString(), context.IpAddress, JsonSerializer.Serialize(details)),
            cancellationToken);
    }
}
