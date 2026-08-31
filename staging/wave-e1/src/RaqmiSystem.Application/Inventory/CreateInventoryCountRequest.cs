namespace RaqmiSystem.Application.Inventory;

public sealed record CreateInventoryCountRequest(
    string WarehouseCode,
    DateOnly CountDate);
