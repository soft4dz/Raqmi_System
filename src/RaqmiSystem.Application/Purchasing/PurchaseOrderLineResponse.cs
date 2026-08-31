namespace RaqmiSystem.Application.Purchasing;

public sealed record PurchaseOrderLineResponse(
    Guid Id,
    int LineNumber,
    string ItemCode,
    string Designation,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotalExclVat,
    decimal QuantityReceived,
    decimal RemainingQuantity);
