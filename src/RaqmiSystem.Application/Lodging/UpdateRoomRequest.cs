namespace RaqmiSystem.Application.Lodging;

public sealed record UpdateRoomRequest(
    string RoomTypeCode,
    string? Floor = null,
    string? Notes = null);
