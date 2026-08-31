using RaqmiSystem.Domain.Housekeeping;

namespace RaqmiSystem.Application.Housekeeping;

public sealed record RoomConditionResponse(
    Guid RoomId,
    string HotelUnitCode,
    string RoomNumber,
    RoomConditionStatus Status,
    DateTimeOffset? LastCleanedAt,
    string? LastCleanedBy,
    DateTimeOffset? LastInspectedAt,
    string? LastInspectedBy,
    string? OutOfOrderReason,
    DateOnly? OutOfOrderUntil,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
