namespace RaqmiSystem.Application.Accounting;

/// <summary>
/// Replaces the lines of a DRAFT entry. Refused with a 409 on a posted entry: a posted entry is
/// immutable and is corrected by a reversing entry.
/// </summary>
public sealed record UpdateJournalEntryLinesRequest(
    IReadOnlyCollection<JournalEntryLineRequest> Lines);
