namespace RaqmiSystem.Domain.Housekeeping;

/// <summary>
/// Housekeeping condition of a physical room - the CLEANLINESS axis, owned by this module
/// alone. It is deliberately independent of the OCCUPANCY axis (who sleeps there tonight),
/// which is derived from the reservations of the lodging module and never stored here: a room
/// can be occupied and dirty, vacant and inspected, and every combination in between.
/// </summary>
public enum RoomConditionStatus
{
    /// <summary>Serviced and sellable. The status a room nobody has ever declared dirty is presumed to be in.</summary>
    Clean,

    /// <summary>Needs service: the guest left, or a supervisor refused the last inspection.</summary>
    Dirty,

    /// <summary>Cleaned AND checked by a supervisor. The strongest state before selling the room again.</summary>
    Inspected,

    /// <summary>
    /// Withdrawn from service (breakdown, works, deep cleaning). Requires a reason, and
    /// optionally the date it is expected back. Housekeeping never plans a task on such a room.
    /// </summary>
    OutOfOrder
}
