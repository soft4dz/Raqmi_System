namespace RaqmiSystem.Application.Inventory;

/// <summary>The two halves of a persisted transfer, linked by their shared transfer group id.</summary>
public sealed record StockTransferResponse(
    Guid TransferGroupId,
    StockMovementResponse OutMovement,
    StockMovementResponse InMovement);
