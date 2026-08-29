namespace RaqmiSystem.Application.Treasury;

public sealed record CreateBankAccountRequest(
    string Code,
    string Label,
    string BankName,
    string AccountNumber);
