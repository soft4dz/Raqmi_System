namespace RaqmiSystem.Application.Purchasing;

public sealed record UpdatePurchaseOrderLinesRequest(
    IReadOnlyCollection<PurchaseOrderLineRequest> Lines);
