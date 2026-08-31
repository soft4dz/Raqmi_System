namespace RaqmiSystem.Application.Inventory;

public sealed record InventoryCountLineResponse(
    Guid Id,
    int LineNumber,
    string ItemCode,
    string? Designation,
    string? UnitOfMeasure,
    decimal CountedQuantity);
