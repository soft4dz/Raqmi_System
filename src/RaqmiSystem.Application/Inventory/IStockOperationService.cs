using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Application.Inventory;

public interface IStockOperationService
{
    // Enregistre une entree en stock issue d'une reception d'achat : un mouvement
    // d'entree par ligne, au cout unitaire de la reception.
    Task<ApplicationResult<StockEntryResult>> RegisterPurchaseReceiptAsync(
        RegisterPurchaseReceiptRequest request, OperationContext context, CancellationToken cancellationToken);

    // Enregistre la sortie de stock d'une vente : un mouvement de sortie par ligne, dans le
    // magasin indique, refuse si le stock disponible ne couvre pas les quantites. La reference
    // (numero de facture) rend l'operation idempotente : une vente deja enregistree sous cette
    // reference n'est pas ressortie une seconde fois.
    Task<ApplicationResult<StockExitResult>> RegisterSaleAsync(
        RegisterSaleRequest request, OperationContext context, CancellationToken cancellationToken);
}

public sealed record RegisterPurchaseReceiptRequest(string WarehouseCode, string Reference, IReadOnlyList<StockEntryLine> Lines);

public sealed record StockEntryLine(string ItemCode, decimal Quantity, decimal UnitCost, string? LotNumber, DateOnly? ExpiryDate);

public sealed record StockEntryResult(int MovementCount);

public sealed record RegisterSaleRequest(string WarehouseCode, string Reference, IReadOnlyList<StockExitLine> Lines);

public sealed record StockExitLine(string ItemCode, decimal Quantity);

/// <param name="MovementCount">Mouvements ecrits par cet appel : zero quand la vente etait deja enregistree.</param>
/// <param name="AlreadyRegistered">Vrai quand la reference avait deja produit sa sortie : rien n'a ete ecrit.</param>
public sealed record StockExitResult(int MovementCount, bool AlreadyRegistered);
