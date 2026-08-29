using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Application.Treasury;

public sealed record UpdateCashReceiptRequest(
    DateOnly ReceiptDate,
    string HotelUnitCode,
    PaymentMethod Method,
    decimal Amount,
    string? Reference = null,
    string? BankAccountCode = null,
    string? Notes = null);
