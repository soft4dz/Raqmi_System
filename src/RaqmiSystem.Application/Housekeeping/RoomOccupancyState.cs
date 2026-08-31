namespace RaqmiSystem.Application.Housekeeping;

/// <summary>
/// What the reservations of the lodging module say about a room on ONE day. Derived at read
/// time and never stored: the reservations are the single truth about who sleeps where, and a
/// second stored copy would be a second truth free to disagree with the first.
/// </summary>
public enum RoomOccupancyState
{
    /// <summary>No stay touches the room that day.</summary>
    Vacant,

    /// <summary>A checked-in stay covers the night and does not end that day.</summary>
    Occupied,

    /// <summary>A stay ends that day: the room frees up and needs a full departure clean.</summary>
    Departure,

    /// <summary>A stay starts that day on a room nobody leaves: the room must be ready.</summary>
    Arrival,

    /// <summary>A departure AND an arrival on the same day - the tightest deadline of the sheet.</summary>
    Turnover
}
