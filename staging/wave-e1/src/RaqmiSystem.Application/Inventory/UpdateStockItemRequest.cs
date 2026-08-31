using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Application.Inventory;

public sealed record UpdateStockItemRequest(
    string Designation,
    string UnitOfMeasure,
    StockItemCategory Category,
    decimal MinimumQuantity);
