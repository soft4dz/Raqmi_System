namespace RaqmiSystem.Application.Housekeeping;

/// <summary>
/// The workload of one attendant on the sheet of the day. <see cref="TotalMinutes"/> only counts
/// the passes that were actually finished, so it measures work done and not time elapsed.
/// </summary>
public sealed record HousekeepingAttendantLoad(
    string AssignedTo,
    int TaskCount,
    int Pending,
    int InProgress,
    int AwaitingInspection,
    int Inspected,
    int Rejected,
    int TotalMinutes);
