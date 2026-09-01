namespace RaqmiSystem.Application.Lodging;

public sealed record PayDepositRequest(DateOnly PaidDate, string PaymentMethod, string? Reference = null);
