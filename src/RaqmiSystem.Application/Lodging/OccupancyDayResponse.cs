namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Occupation of one hotel unit for one night: how many rooms are active in the unit, how many
/// are taken by a reservation (Booked or CheckedIn) covering that night, and the resulting rate
/// as a percentage (0 when the unit has no active room).
/// </summary>
public sealed record OccupancyDayResponse(
    DateOnly Date,
    int TotalActiveRooms,
    int OccupiedRooms,
    decimal OccupancyRatePercent);
