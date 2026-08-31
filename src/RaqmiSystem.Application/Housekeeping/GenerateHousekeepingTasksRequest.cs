namespace RaqmiSystem.Application.Housekeeping;

/// <summary>
/// Builds the day sheet of one unit from the reservations of that day. Idempotent: a room that
/// already carries a task of the same type for that date is left alone, so a supervisor can
/// re-run it after a late booking without duplicating the morning work.
/// </summary>
public sealed record GenerateHousekeepingTasksRequest(
    string HotelUnitCode,
    DateOnly ServiceDate);
