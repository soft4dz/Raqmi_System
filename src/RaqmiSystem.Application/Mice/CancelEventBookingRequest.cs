namespace RaqmiSystem.Application.Mice;

/// <summary>
/// Cancels an event and releases its space. The reason is mandatory: a cancelled event is the one
/// thing a client will ask about months later, and "cancelled, no reason recorded" is not an
/// answer a hotel can give.
/// </summary>
public sealed record CancelEventBookingRequest(string Reason);
