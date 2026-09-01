namespace RaqmiSystem.Application.Lodging;

public sealed record SaveOverbookingAllowanceRequest(
    string HotelUnitCode,
    string RoomTypeCode,
    DateOnly FromDate,
    DateOnly ToDate,
    int ExtraRooms,
    string? Notes = null);
