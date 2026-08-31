namespace RaqmiSystem.Application.Inventory;

public sealed record CreateWarehouseRequest(
    string Code,
    string Label,
    string HotelUnitCode);
