namespace RaqmiSystem.Application.Channels;

public sealed record ChannelRestrictionPush(
    string HotelUnitCode,
    DateOnly From,
    DateOnly To,
    IReadOnlyCollection<ChannelRestrictionLine> Lines);
