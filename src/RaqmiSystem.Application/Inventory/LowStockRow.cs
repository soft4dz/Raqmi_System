namespace RaqmiSystem.Application.Inventory;

/// <summary>
/// An item whose current stock in a warehouse sits strictly below its minimum threshold.
/// Items with a zero threshold never alert (0 means "no alert" - see StockItem).
/// </summary>
public sealed record LowStockRow(
    string WarehouseCode,
    string WarehouseLabel,
    string ItemCode,
    string Designation,
    string UnitOfMeasure,
    decimal Quantity,
    decimal MinimumQuantity);
