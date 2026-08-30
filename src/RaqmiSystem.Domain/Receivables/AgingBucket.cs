namespace RaqmiSystem.Domain.Receivables;

/// <summary>
/// Age brackets of an outstanding receivable, counted in days.
/// </summary>
public enum AgingBucket
{
    /// <summary>Not yet overdue as of the reporting date.</summary>
    NotDue,

    /// <summary>1 to 30 days old.</summary>
    Days1To30,

    /// <summary>31 to 60 days old.</summary>
    Days31To60,

    /// <summary>61 to 90 days old.</summary>
    Days61To90,

    /// <summary>More than 90 days old.</summary>
    Over90
}
