namespace RaqmiSystem.Application.Inventory;

/// <summary>
/// Inter-warehouse transfer: one request, two movements (out of the source, into the
/// destination) persisted atomically with a shared transfer group id.
/// </summary>
public sealed record CreateStockTransferRequest(
    string FromWarehouseCode,
    string ToWarehouseCode,
    string ItemCode,
    DateOnly MovementDate,
    decimal Quantity,
    string Reference,
    string? LotNumber,
    DateOnly? ExpiryDate,
    string? Notes);
