namespace RaqmiSystem.Application.Channels;

public sealed record ChannelRatePush(
    string HotelUnitCode,
    DateOnly From,
    DateOnly To,
    IReadOnlyCollection<ChannelRateLine> Lines);
