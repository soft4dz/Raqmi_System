namespace RaqmiSystem.Application.Accounting;

/// <summary>
/// The code is absent because it is immutable: entries reference their journal by code.
/// </summary>
public sealed record UpdateAccountingJournalRequest(string Label);
