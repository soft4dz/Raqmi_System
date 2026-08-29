using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Application.Treasury;

public sealed record PaymentOrderResponse(
    Guid Id,
    DateOnly OrderDate,
    string Beneficiary,
    decimal Amount,
    DateOnly DueDate,
    string BankAccountCode,
    string? Reference,
    PaymentOrderStatus Status,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    DateTimeOffset? PaidAt,
    string? PaidBy,
    DateTimeOffset? CancelledAt,
    string? CancelledBy,
    string? CancelReason,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
