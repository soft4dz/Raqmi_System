using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Application.Inventory;

public sealed record StockItemResponse(
    Guid Id,
    string Code,
    string Designation,
    string UnitOfMeasure,
    StockItemCategory Category,
    decimal MinimumQuantity,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
