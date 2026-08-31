using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Application.Inventory;

/// <summary>
/// Current state of one item in one warehouse. Quantity is the sum of the movement registry;
/// AverageUnitCost is the weighted average cost (PMP) derived from the purchase entries
/// (sum of quantity x unit cost divided by sum of quantities, all purchase entries of the
/// item combined); StockValue is Quantity x AverageUnitCost rounded to 2 decimals.
/// </summary>
public sealed record WarehouseStockRow(
    string ItemCode,
    string Designation,
    string UnitOfMeasure,
    StockItemCategory Category,
    decimal Quantity,
    decimal AverageUnitCost,
    decimal StockValue,
    decimal MinimumQuantity,
    bool IsBelowMinimum);
