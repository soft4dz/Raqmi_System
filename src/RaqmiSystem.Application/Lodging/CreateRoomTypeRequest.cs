namespace RaqmiSystem.Application.Lodging;

public sealed record CreateRoomTypeRequest(
    string HotelUnitCode,
    string Code,
    string Label,
    int Capacity,
    string? Description = null);
