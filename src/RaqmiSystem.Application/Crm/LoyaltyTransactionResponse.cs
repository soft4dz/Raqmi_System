using RaqmiSystem.Domain.Crm;

namespace RaqmiSystem.Application.Crm;

public sealed record LoyaltyTransactionResponse(
    Guid Id,
    string CustomerCode,
    LoyaltyTransactionKind Kind,
    int Points,
    DateOnly OccurredOn,
    string Reason,
    string? Reference,
    DateTimeOffset CreatedAt,
    string CreatedBy);
