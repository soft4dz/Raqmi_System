namespace RaqmiSystem.Application.Lodging;

public sealed record CreateRoomRequest(
    string HotelUnitCode,
    string Number,
    string RoomTypeCode);
