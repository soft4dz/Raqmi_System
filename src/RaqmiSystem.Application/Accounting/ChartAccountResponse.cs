using RaqmiSystem.Domain.Accounting;

namespace RaqmiSystem.Application.Accounting;

public sealed record ChartAccountResponse(
    Guid Id,
    string Code,
    string Label,
    int AccountClass,
    string? AccountClassLabel,
    AccountKind Kind,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
