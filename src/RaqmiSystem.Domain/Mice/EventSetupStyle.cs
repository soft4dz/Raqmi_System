namespace RaqmiSystem.Domain.Mice;

/// <summary>
/// Room layout requested for the event. It is stored on the BOOKING and not on the space, because
/// the same room is laid out differently from one event to the next.
///
/// Known simplification, stated rather than hidden: a real venue publishes a DIFFERENT capacity
/// per layout (a room seating 200 in theatre style seats 80 in classroom style). This version
/// holds a single maximum capacity per space and checks the expected attendance against it, so it
/// catches the gross error - booking 300 people into a 100-seat room - but not the subtle one.
/// Per-layout capacities are the natural next step.
/// </summary>
public enum EventSetupStyle
{
    /// <summary>Rows of chairs facing the stage.</summary>
    Theatre = 0,

    /// <summary>Rows of tables with chairs, for writing.</summary>
    Classroom = 1,

    /// <summary>Tables in a U, everyone facing the centre.</summary>
    UShape = 2,

    /// <summary>One closed table, boardroom style.</summary>
    Boardroom = 3,

    /// <summary>Round tables for a seated meal.</summary>
    Banquet = 4,

    /// <summary>Standing, no seating plan.</summary>
    Cocktail = 5,

    /// <summary>Anything else; describe it in the booking notes.</summary>
    Custom = 6
}
