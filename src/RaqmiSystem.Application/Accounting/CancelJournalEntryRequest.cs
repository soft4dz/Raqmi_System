namespace RaqmiSystem.Application.Accounting;

/// <summary>
/// Abandons a DRAFT entry. There is no equivalent for a posted entry: that one is corrected by
/// a reversing entry, never cancelled and never deleted.
/// </summary>
public sealed record CancelJournalEntryRequest(string Reason);
