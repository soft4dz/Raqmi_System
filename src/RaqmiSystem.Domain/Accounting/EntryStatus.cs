namespace RaqmiSystem.Domain.Accounting;

/// <summary>
/// Lifecycle of a journal entry. Note what is NOT here: there is no "deleted" state, and
/// <see cref="Cancelled"/> is reachable only from <see cref="Draft"/>. Once an entry is
/// <see cref="Posted"/> it is immutable and stays in the books for good; the only correction is
/// a reversing entry (extourne), which leaves the original posted and flags it as reversed.
/// </summary>
public enum EntryStatus
{
    /// <summary>Brouillon - editable, ignored by the trial balance, may be unbalanced.</summary>
    Draft = 1,

    /// <summary>Comptabilisee - balanced, immutable, counted by the trial balance.</summary>
    Posted = 2,

    /// <summary>
    /// Abandonnee - a draft that was given up before ever being posted. It never entered the
    /// books, so discarding it is not an accounting correction and needs no reversing entry.
    /// </summary>
    Cancelled = 3
}
