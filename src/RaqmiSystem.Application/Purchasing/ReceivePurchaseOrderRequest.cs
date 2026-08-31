namespace RaqmiSystem.Application.Purchasing;

/// <summary>
/// One delivery against an approved purchase order: for each order line concerned, the
/// quantity received NOW (cumulated server-side into the line's total received). Partial
/// deliveries are the normal case - the same order accepts as many receipts as it takes to
/// complete it. Lot number and expiry date are optional traceability details forwarded to the
/// stock module with the generated stock entry.
/// </summary>
public sealed record ReceivePurchaseOrderRequest(
    IReadOnlyCollection<ReceivePurchaseOrderLineRequest> Lines);

public sealed record ReceivePurchaseOrderLineRequest(
    Guid LineId,
    decimal Quantity,
    string? LotNumber = null,
    DateOnly? ExpiryDate = null);
