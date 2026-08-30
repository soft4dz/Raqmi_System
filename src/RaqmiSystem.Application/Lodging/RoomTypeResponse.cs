namespace RaqmiSystem.Application.Lodging;

public sealed record RoomTypeResponse(
    Guid Id,
    string HotelUnitCode,
    string Code,
    string Label,
    int Capacity,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
