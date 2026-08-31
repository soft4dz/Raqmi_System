namespace RaqmiSystem.Domain.Housekeeping;

/// <summary>
/// Nature of the service a room needs on one day. The type drives the workload a task
/// represents, which is why the day sheet counts them separately: a departure clean is not a
/// stayover touch-up.
/// </summary>
public enum HousekeepingTaskType
{
    /// <summary>Departure clean ("recouche a blanc"): the guest leaves that day, the room is stripped and made up in full.</summary>
    Departure,

    /// <summary>Stayover service: the guest stays another night, the room is refreshed around their belongings.</summary>
    Stayover,

    /// <summary>Refresh of a room that is vacant but dirty - nobody slept there, it still needs to be made sellable.</summary>
    Vacant,

    /// <summary>Deep clean planned by a supervisor, outside the daily rhythm of arrivals and departures.</summary>
    DeepClean
}
