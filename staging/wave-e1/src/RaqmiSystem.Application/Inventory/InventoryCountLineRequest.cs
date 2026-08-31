namespace RaqmiSystem.Application.Inventory;

public sealed record InventoryCountLineRequest(
    string ItemCode,
    decimal CountedQuantity);
