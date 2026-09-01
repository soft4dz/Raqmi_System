namespace RaqmiSystem.Application.Channels;

public sealed record ChannelRestrictionLine(
    string RoomTypeCode,
    string? RatePlanCode,
    DateOnly Date,
    bool IsClosed,
    bool IsClosedToArrival,
    bool IsClosedToDeparture,
    int MinimumStay,
    int MaximumStay);
