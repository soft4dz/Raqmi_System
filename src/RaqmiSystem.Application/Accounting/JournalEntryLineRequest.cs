namespace RaqmiSystem.Application.Accounting;

/// <summary>
/// One side of one accounting fact. Exactly one of <paramref name="Debit"/> and
/// <paramref name="Credit"/> must be non-zero - the other is sent as 0.
/// </summary>
public sealed record JournalEntryLineRequest(
    string AccountCode,
    string Label,
    decimal Debit,
    decimal Credit,
    Guid? PartyId = null);
