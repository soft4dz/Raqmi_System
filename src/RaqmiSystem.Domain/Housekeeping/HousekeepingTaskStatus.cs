namespace RaqmiSystem.Domain.Housekeeping;

/// <summary>
/// Life of one cleaning task. The chain is Pending -> InProgress -> Cleaned -> Inspected, with
/// two branches: a supervisor may refuse a finished room (Cleaned -> Rejected, which sends it
/// back to InProgress), and any task that has not reached a terminal state may be cancelled.
/// Terminal states are <see cref="Inspected"/> and <see cref="Cancelled"/>.
/// </summary>
public enum HousekeepingTaskStatus
{
    /// <summary>Planned, not started. Assigning an attendant does not leave this state.</summary>
    Pending,

    /// <summary>The attendant is in the room.</summary>
    InProgress,

    /// <summary>The attendant declares the room done. It now waits for a supervisor's inspection.</summary>
    Cleaned,

    /// <summary>A supervisor checked the room and accepted it. Terminal.</summary>
    Inspected,

    /// <summary>A supervisor checked the room and refused it (mandatory reason). The task goes back to work.</summary>
    Rejected,

    /// <summary>Withdrawn from the sheet before completion (mandatory reason). Terminal.</summary>
    Cancelled
}
