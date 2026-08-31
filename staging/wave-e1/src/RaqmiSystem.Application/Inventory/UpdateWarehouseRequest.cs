namespace RaqmiSystem.Application.Inventory;

public sealed record UpdateWarehouseRequest(
    string Label,
    string HotelUnitCode);
