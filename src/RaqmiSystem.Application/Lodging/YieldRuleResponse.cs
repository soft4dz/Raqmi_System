using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

public sealed record YieldRuleResponse(
    Guid Id,
    string HotelUnitCode,
    string Code,
    string Label,
    string? RoomTypeCode,
    string? RatePlanCode,
    DateOnly FromDate,
    DateOnly ToDate,
    YieldTrigger Trigger,
    decimal ThresholdValue,
    string? DaysOfWeek,
    decimal AdjustmentPercent,
    int Priority,
    bool IsActive,
    string? Notes,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
