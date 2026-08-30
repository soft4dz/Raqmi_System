namespace RaqmiSystem.Application.Accounting;

public sealed record AccountingJournalResponse(
    Guid Id,
    string Code,
    string Label,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
