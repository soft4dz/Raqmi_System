using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Application.Inventory;

public sealed record CreateStockItemRequest(
    string Code,
    string Designation,
    string UnitOfMeasure,
    StockItemCategory Category,
    decimal MinimumQuantity);
