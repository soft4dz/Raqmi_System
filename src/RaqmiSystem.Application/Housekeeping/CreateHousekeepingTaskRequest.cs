using RaqmiSystem.Domain.Housekeeping;

namespace RaqmiSystem.Application.Housekeeping;

public sealed record CreateHousekeepingTaskRequest(
    Guid RoomId,
    DateOnly ServiceDate,
    HousekeepingTaskType TaskType,
    string? AssignedTo = null,
    string? Notes = null);
