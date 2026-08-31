using RaqmiSystem.Domain.Housekeeping;

namespace RaqmiSystem.Application.Housekeeping;

public sealed record HousekeepingTaskResponse(
    Guid Id,
    string HotelUnitCode,
    Guid RoomId,
    string RoomNumber,
    DateOnly ServiceDate,
    HousekeepingTaskType TaskType,
    HousekeepingTaskStatus Status,
    string? AssignedTo,
    DateTimeOffset? AssignedAt,
    string? AssignedBy,
    DateTimeOffset? StartedAt,
    string? StartedBy,
    DateTimeOffset? CleanedAt,
    string? CleanedBy,
    int? DurationMinutes,
    DateTimeOffset? InspectedAt,
    string? InspectedBy,
    string? InspectionNotes,
    DateTimeOffset? CancelledAt,
    string? CancelledBy,
    string? CancelReason,
    string? Notes,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
