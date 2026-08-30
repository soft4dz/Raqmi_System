namespace RaqmiSystem.Application.Accounting;

/// <summary>
/// Creates a DRAFT entry. The draft is allowed to be unbalanced - balance is required only to
/// post it (<c>POST /accounting/entries/{id}/post</c>).
/// </summary>
public sealed record CreateJournalEntryRequest(
    DateOnly EntryDate,
    string JournalCode,
    string Label,
    string? Reference,
    IReadOnlyCollection<JournalEntryLineRequest> Lines);
