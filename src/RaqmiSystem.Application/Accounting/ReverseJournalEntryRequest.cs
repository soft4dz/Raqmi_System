namespace RaqmiSystem.Application.Accounting;

/// <summary>
/// Both fields are optional. <paramref name="ReversalDate"/> defaults to the reversed entry's own
/// date, so the two entries cancel out inside any period that contains the original;
/// <paramref name="Reference"/> defaults to the original's reference.
/// </summary>
public sealed record ReverseJournalEntryRequest(
    DateOnly? ReversalDate,
    string? Reference);
