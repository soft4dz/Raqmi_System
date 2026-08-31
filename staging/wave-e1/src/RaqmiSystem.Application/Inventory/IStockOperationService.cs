using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Inventory;

public interface IStockOperationService
{
    // Enregistre une entree en stock issue d'une reception d'achat : un mouvement
    // d'entree par ligne, au cout unitaire de la reception.
    Task<ApplicationResult<StockEntryResult>> RegisterPurchaseReceiptAsync(
        RegisterPurchaseReceiptRequest request, OperationContext context, CancellationToken cancellationToken);
}

public sealed record RegisterPurchaseReceiptRequest(string WarehouseCode, string Reference, IReadOnlyList<StockEntryLine> Lines);

public sealed record StockEntryLine(string ItemCode, decimal Quantity, decimal UnitCost, string? LotNumber, DateOnly? ExpiryDate);

public sealed record StockEntryResult(int MovementCount);
