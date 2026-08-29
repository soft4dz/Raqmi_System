namespace RaqmiSystem.Application.Treasury;

public sealed record CreatePaymentOrderRequest(
    DateOnly OrderDate,
    string Beneficiary,
    decimal Amount,
    DateOnly DueDate,
    string BankAccountCode,
    string? Reference = null);
