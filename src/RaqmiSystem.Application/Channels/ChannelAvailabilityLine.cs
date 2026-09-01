namespace RaqmiSystem.Application.Channels;

public sealed record ChannelAvailabilityLine(string RoomTypeCode, DateOnly Night, int RoomsAvailable);
