using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Fiscalite;

namespace RaqmiSystem.Application.Fiscalite;

/// <summary>
/// Annual fiscal return ("liasse fiscale"). Generating a kind for a year already generated
/// replaces the previous one (upsert on Year+Kind) - this is a working draft prepared for filing,
/// not an immutable ledger.
/// </summary>
public interface IFiscalReturnService
{
    Task<IReadOnlyCollection<FiscalReturnResponse>> GetAsync(int year, CancellationToken cancellationToken);

    Task<ApplicationResult<FiscalReturnResponse>> GenerateSimpleAsync(
        int year, OperationContext context, CancellationToken cancellationToken);

    /// <summary>Includes the IBS estimate - a simplified figure, never an official tax liquidation.</summary>
    Task<ApplicationResult<FiscalReturnResponse>> GenerateAdvancedAsync(
        int year, OperationContext context, CancellationToken cancellationToken);
}

public sealed record FiscalReturnLineResponse(string Code, string Label, decimal Amount);

public sealed record FiscalReturnResponse(
    Guid Id, int Year, FiscalReturnKind Kind, DateTimeOffset GeneratedAt,
    IReadOnlyCollection<FiscalReturnLineResponse> Lines);
