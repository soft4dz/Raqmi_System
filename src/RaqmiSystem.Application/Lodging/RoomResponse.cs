namespace RaqmiSystem.Application.Lodging;

public sealed record RoomResponse(
    Guid Id,
    string HotelUnitCode,
    string Number,
    string RoomTypeCode,
    string? Floor,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
