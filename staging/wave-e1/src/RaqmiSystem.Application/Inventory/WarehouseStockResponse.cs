namespace RaqmiSystem.Application.Inventory;

/// <summary>
/// Current stock of a warehouse with its valuation. TotalValue is computed server-side (the
/// screen never adds up financial figures on its own) as the sum of the rows' StockValue.
/// </summary>
public sealed record WarehouseStockResponse(
    string WarehouseCode,
    string WarehouseLabel,
    IReadOnlyCollection<WarehouseStockRow> Rows,
    decimal TotalValue);
