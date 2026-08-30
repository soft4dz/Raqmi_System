using RaqmiSystem.Domain.Accounting;

namespace RaqmiSystem.Application.Accounting;

/// <summary>
/// A journal entry with its lines.
///
/// <paramref name="ReversesEntryId"/> is set on a reversing entry and names what it corrects;
/// <paramref name="ReversedByEntryId"/> is set on the corrected entry and names its reversal. A
/// reversed entry stays <see cref="EntryStatus.Posted"/> - it is corrected, not removed - so
/// readers must look at <paramref name="ReversedByEntryId"/>, not at the status, to tell whether
/// an entry has been extourned.
/// </summary>
public sealed record JournalEntryResponse(
    Guid Id,
    DateOnly EntryDate,
    string JournalCode,
    string Label,
    string? Reference,
    EntryStatus Status,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsBalanced,
    bool CanEdit,
    IReadOnlyCollection<JournalEntryLineResponse> Lines,
    Guid? ReversesEntryId,
    Guid? ReversedByEntryId,
    DateTimeOffset? PostedAt,
    string? PostedBy,
    DateTimeOffset? ReversedAt,
    string? ReversedBy,
    DateTimeOffset? CancelledAt,
    string? CancelledBy,
    string? CancellationReason,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
