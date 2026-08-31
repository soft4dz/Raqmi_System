namespace RaqmiSystem.Application.Inventory;

public sealed record WarehouseResponse(
    Guid Id,
    string Code,
    string Label,
    string HotelUnitCode,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
