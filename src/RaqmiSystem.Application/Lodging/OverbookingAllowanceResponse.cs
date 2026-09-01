namespace RaqmiSystem.Application.Lodging;

public sealed record OverbookingAllowanceResponse(
    Guid Id,
    string HotelUnitCode,
    string RoomTypeCode,
    DateOnly FromDate,
    DateOnly ToDate,
    int ExtraRooms,
    bool IsActive,
    string? Notes,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
