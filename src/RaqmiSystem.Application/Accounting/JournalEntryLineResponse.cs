namespace RaqmiSystem.Application.Accounting;

public sealed record JournalEntryLineResponse(
    Guid Id,
    int LineNumber,
    string AccountCode,
    string Label,
    decimal Debit,
    decimal Credit,
    Guid? PartyId = null);
