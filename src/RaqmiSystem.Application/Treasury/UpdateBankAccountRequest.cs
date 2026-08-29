namespace RaqmiSystem.Application.Treasury;

public sealed record UpdateBankAccountRequest(
    string Label,
    string BankName,
    string AccountNumber);
