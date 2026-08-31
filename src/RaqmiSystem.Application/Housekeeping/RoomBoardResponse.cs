namespace RaqmiSystem.Application.Housekeeping;

/// <summary>
/// The room board of one unit for one day: every ACTIVE room, its condition, what the day
/// expects of it, and the counters the head housekeeper reads first. Counters are computed from
/// the very rows returned, so what the screen totals and what it lists can never disagree.
/// </summary>
public sealed record RoomBoardResponse(
    string HotelUnitCode,
    DateOnly Date,
    int TotalRooms,
    int CleanRooms,
    int DirtyRooms,
    int InspectedRooms,
    int OutOfOrderRooms,
    int Departures,
    int Arrivals,
    int Turnovers,
    int OccupiedRooms,
    int VacantRooms,
    int PendingTasks,
    int InProgressTasks,
    int AwaitingInspectionTasks,
    IReadOnlyCollection<RoomBoardRow> Rows);
