namespace RaqmiSystem.Application.Housekeeping;

/// <summary>
/// What the generation of a day sheet actually did. <see cref="SkippedExisting"/> is reported
/// rather than swallowed: a supervisor re-running the generation after a late booking must see
/// that the morning work was preserved, not wonder whether it was silently rebuilt.
/// </summary>
public sealed record GenerateHousekeepingTasksResponse(
    string HotelUnitCode,
    DateOnly ServiceDate,
    int Created,
    int SkippedExisting,
    int SkippedOutOfOrder,
    IReadOnlyCollection<HousekeepingTaskResponse> Tasks);
