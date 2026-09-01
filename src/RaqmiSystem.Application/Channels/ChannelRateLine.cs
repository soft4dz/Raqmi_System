namespace RaqmiSystem.Application.Channels;

public sealed record ChannelRateLine(
    string RoomTypeCode,
    string RatePlanCode,
    DateOnly Night,
    decimal Amount,
    string CurrencyCode);
