using RaqmiSystem.Domain.Housekeeping;

namespace RaqmiSystem.Application.Housekeeping;

/// <summary>
/// Declares the housekeeping condition of one room by hand. OutOfOrder demands a reason;
/// OutOfOrderUntil is the indicative return-to-service date and is ignored by every other status.
/// </summary>
public sealed record SetRoomConditionRequest(
    RoomConditionStatus Status,
    string? OutOfOrderReason = null,
    DateOnly? OutOfOrderUntil = null);
