namespace RaqmiSystem.Application.Housekeeping;

/// <summary>
/// Planning of the teams for one unit and one day: the load carried by each attendant, and what
/// is still on nobody plate (<see cref="UnassignedTasks"/>), which is what a supervisor
/// rebalances the morning around. Cancelled tasks are excluded everywhere: they are no longer
/// work to distribute.
/// </summary>
public sealed record HousekeepingDaySheetResponse(
    string HotelUnitCode,
    DateOnly Date,
    int TotalTasks,
    int UnassignedTasks,
    int PendingTasks,
    int InProgressTasks,
    int AwaitingInspectionTasks,
    int InspectedTasks,
    int RejectedTasks,
    IReadOnlyCollection<HousekeepingAttendantLoad> Attendants,
    IReadOnlyCollection<HousekeepingTaskResponse> Tasks);
