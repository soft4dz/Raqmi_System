using RaqmiSystem.Domain.Housekeeping;

namespace RaqmiSystem.Application.Housekeeping;

/// <summary>
/// One room on the board, crossing the two axes housekeeping works with: what the reservations
/// say (<see cref="OccupancyState"/>, derived) and what the teams say
/// (<see cref="ConditionStatus"/>, owned here), plus the task of the day when there is one.
///
/// <see cref="ConditionRecorded"/> tells a Clean that somebody declared apart from a Clean
/// nobody ever spoke about: a room with no condition row reads as Clean by presumption, and the
/// screen must be able to show that difference rather than claim a service that never happened.
/// </summary>
public sealed record RoomBoardRow(
    Guid RoomId,
    string RoomNumber,
    string RoomTypeCode,
    string? Floor,
    RoomConditionStatus ConditionStatus,
    bool ConditionRecorded,
    DateTimeOffset? LastCleanedAt,
    string? LastCleanedBy,
    DateTimeOffset? LastInspectedAt,
    string? LastInspectedBy,
    string? OutOfOrderReason,
    DateOnly? OutOfOrderUntil,
    RoomOccupancyState OccupancyState,
    Guid? ReservationId,
    string? CustomerCode,
    int? GuestCount,
    Guid? TaskId,
    HousekeepingTaskType? TaskType,
    HousekeepingTaskStatus? TaskStatus,
    string? TaskAssignedTo);
