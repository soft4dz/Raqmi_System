namespace RaqmiSystem.Domain.Receivables;

/// <summary>
/// Escalation ladder of a dunning action. The order is meaningful (First &lt; Second &lt;
/// FormalNotice): the highest level ever reached on an invoice is what qualifies the
/// commercial relationship, and comparisons rely on the underlying ordinal values.
/// </summary>
public enum ReminderLevel
{
    /// <summary>Premiere relance: courtesy reminder.</summary>
    First,

    /// <summary>Deuxieme relance: firm reminder after the first one went unanswered.</summary>
    Second,

    /// <summary>Mise en demeure: formal notice, the last step before legal recovery.</summary>
    FormalNotice
}
