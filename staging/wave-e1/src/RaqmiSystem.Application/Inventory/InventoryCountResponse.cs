using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Application.Inventory;

public sealed record InventoryCountResponse(
    Guid Id,
    string WarehouseCode,
    DateOnly CountDate,
    InventoryCountStatus Status,
    IReadOnlyCollection<InventoryCountLineResponse> Lines,
    bool CanEdit,
    DateTimeOffset? ValidatedAt,
    string? ValidatedBy,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
