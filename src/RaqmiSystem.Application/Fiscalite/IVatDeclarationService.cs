using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Fiscalite;

namespace RaqmiSystem.Application.Fiscalite;

/// <summary>
/// Monthly VAT declaration and G50 tele-declaration export. The declaration itself is only ever
/// calculated/recalculated (upserted per period) from the registers - deposit on the DGI portal
/// happens outside the application, the DGI reference is then pasted back via
/// <see cref="MarkDeclaredAsync"/>.
/// </summary>
public interface IVatDeclarationService
{
    Task<IReadOnlyCollection<VatDeclarationResponse>> GetHistoryAsync(CancellationToken cancellationToken);

    Task<ApplicationResult<VatDeclarationResponse>> CalculateAsync(
        int year, int month, OperationContext context, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TeleDeclarationResponse>> GetTeleDeclarationsAsync(CancellationToken cancellationToken);

    /// <summary>Recalculates the period, then produces the fixed 6-field G50 CSV.</summary>
    Task<ApplicationResult<G50ExportResult>> ExportG50Async(
        int year, int month, OperationContext context, CancellationToken cancellationToken);

    Task<ApplicationResult<TeleDeclarationResponse>> MarkDeclaredAsync(
        Guid teleDeclarationId, string dgiReference, OperationContext context, CancellationToken cancellationToken);
}

public sealed record VatDeclarationResponse(
    Guid Id, int Year, int Month, decimal BaseHtVentes, decimal TvaCollectee, decimal TvaDeductible,
    decimal CreditAnterieur, decimal Solde, DeclarationStatus Status, string? DgiReference);

public sealed record TeleDeclarationResponse(
    Guid Id, Guid VatDeclarationId, TeleDeclarationType Type, int Year, int Month, decimal Amount,
    DeclarationStatus Status, string? DgiReference, DateTimeOffset ExportedAt);

public sealed record G50ExportResult(TeleDeclarationResponse TeleDeclaration, string CsvContent);
