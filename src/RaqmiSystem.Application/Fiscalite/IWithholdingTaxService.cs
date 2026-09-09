using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Fiscalite;

public interface IWithholdingTaxService
{
    Task<IReadOnlyCollection<WithholdingTaxEntryResponse>> ListAsync(CancellationToken cancellationToken);

    Task<ApplicationResult<WithholdingTaxEntryResponse>> RegisterAsync(
        CreateWithholdingTaxEntryRequest request, OperationContext context, CancellationToken cancellationToken);
}

/// <param name="Rate">Percentage as an integer-scale value (e.g. 15 for 15%). Null defaults to 15.</param>
public sealed record CreateWithholdingTaxEntryRequest(string SupplierName, decimal BaseHt, decimal? Rate, DateOnly Date);

public sealed record WithholdingTaxEntryResponse(
    Guid Id, string SupplierName, decimal BaseHt, decimal Rate, decimal MontantRetenu, DateOnly Date);
