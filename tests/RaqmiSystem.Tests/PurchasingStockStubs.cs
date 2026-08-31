using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Inventory;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Tests;

/// <summary>
/// Deterministic stand-in for the stock module's <see cref="IStockOperationService"/>. The
/// purchasing module only CONSUMES that contract: its tests pin what the stock side answers
/// and, above all, CAPTURE what purchasing asked for - the warehouse, the reference and, line
/// by line, the quantity and the unit cost. A receipt is only correct if it hands the stock
/// module the price the line was ORDERED at; recording the calls is the only way to assert it.
///
/// Set <see cref="NextResult"/> to script one refusal for the next call (it then reverts to
/// success), to check that a stock-side refusal aborts the whole reception.
/// </summary>
internal sealed class PurchasingStockOperationStub : IStockOperationService
{
    private readonly List<RegisterPurchaseReceiptRequest> _requests = [];

    public IReadOnlyList<RegisterPurchaseReceiptRequest> Requests => _requests;

    public ApplicationResult<StockEntryResult>? NextResult { get; set; }

    public RegisterPurchaseReceiptRequest? LastRequest => _requests.Count == 0 ? null : _requests[^1];

    public Task<ApplicationResult<StockEntryResult>> RegisterPurchaseReceiptAsync(
        RegisterPurchaseReceiptRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (NextResult is { } scripted)
        {
            NextResult = null;

            // A refused entry is still a call the purchasing side made: recording it lets a
            // test assert that nothing was requested twice after the rollback.
            _requests.Add(request);
            return Task.FromResult(scripted);
        }

        _requests.Add(request);

        return Task.FromResult(ApplicationResult<StockEntryResult>.Success(
            new StockEntryResult(request.Lines.Count)));
    }
}

/// <summary>
/// Deterministic stand-in for the stock module's <see cref="IStockCostProvider"/>. Purchasing
/// uses it for ONE purpose: asserting that an ordered item code exists in the stock referential
/// without ever querying the stock module's own tables. The stub therefore answers NotFound for
/// any code outside <see cref="KnownItemCodes"/>, exactly as the real provider would for an
/// unknown item.
/// </summary>
internal sealed class PurchasingStockCostStub(params string[] knownItemCodes) : IStockCostProvider
{
    public HashSet<string> KnownItemCodes { get; } =
        new(knownItemCodes.Select(code => code.ToUpperInvariant()), StringComparer.OrdinalIgnoreCase);

    public decimal AverageUnitCost { get; set; } = 100.00m;

    public string UnitOfMeasure { get; set; } = "U";

    public Task<ApplicationResult<ItemCost>> GetAverageCostAsync(
        string itemCode,
        CancellationToken cancellationToken)
    {
        var normalized = itemCode.Trim().ToUpperInvariant();

        if (!KnownItemCodes.Contains(normalized))
        {
            return Task.FromResult(ApplicationResult<ItemCost>.NotFound("Stock item was not found."));
        }

        return Task.FromResult(ApplicationResult<ItemCost>.Success(
            new ItemCost(normalized, AverageUnitCost, UnitOfMeasure)));
    }
}
