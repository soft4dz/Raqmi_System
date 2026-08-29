using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Application.Treasury;

public sealed record CashReceiptResponse(
    Guid Id,
    DateOnly ReceiptDate,
    string HotelUnitCode,
    string? HotelUnitName,
    PaymentMethod Method,
    decimal Amount,
    string? Reference,
    string? BankAccountCode,
    string? Notes,
    ReceiptStatus Status,
    bool CanEdit,
    DateTimeOffset? ConfirmedAt,
    string? ConfirmedBy,
    DateTimeOffset? CancelledAt,
    string? CancelledBy,
    string? CancelReason,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
