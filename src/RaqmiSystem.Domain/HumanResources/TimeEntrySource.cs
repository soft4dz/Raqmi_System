namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// Where a time entry came from. Kept on the row because it changes how a discrepancy is
/// investigated: a manual entry is questioned with its author, a device entry with the clock.
/// </summary>
public enum TimeEntrySource
{
    Manual = 0,

    /// <summary>Generated from time-clock records (badge readers).</summary>
    Device = 1
}
