using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Application.Inventory;

public sealed record StockMovementResponse(
    Guid Id,
    string WarehouseCode,
    string ItemCode,
    string? ItemDesignation,
    string? UnitOfMeasure,
    DateOnly MovementDate,
    StockMovementKind Kind,
    decimal Quantity,
    decimal SignedQuantity,
    decimal? UnitCost,
    string Reference,
    string? LotNumber,
    DateOnly? ExpiryDate,
    string? Notes,
    bool? AdjustmentIsIncrease,
    Guid? TransferGroupId,
    DateTimeOffset CreatedAt,
    string CreatedBy);
