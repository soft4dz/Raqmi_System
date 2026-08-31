namespace RaqmiSystem.Application.Purchasing;

public sealed record CreatePurchaseOrderRequest(
    string SupplierCode,
    string WarehouseCode,
    DateOnly OrderDate,
    IReadOnlyCollection<PurchaseOrderLineRequest> Lines);
