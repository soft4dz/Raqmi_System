namespace RaqmiSystem.Application.Treasury;

public sealed record BankAccountResponse(
    Guid Id,
    string Code,
    string Label,
    string BankName,
    string AccountNumber,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
