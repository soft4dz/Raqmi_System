using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Treasury;

public sealed class BankAccount : AuditableEntity
{
    private BankAccount()
    {
    }

    public BankAccount(string code, string label, string bankName, string accountNumber)
    {
        Code = NormalizeCode(code);
        Label = RequireValue(label, nameof(label), 160);
        BankName = RequireValue(bankName, nameof(bankName), 160);
        AccountNumber = RequireValue(accountNumber, nameof(accountNumber), 34);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public string BankName { get; private set; } = string.Empty;

    public string AccountNumber { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public void UpdateDetails(string label, string bankName, string accountNumber)
    {
        Label = RequireValue(label, nameof(label), 160);
        BankName = RequireValue(bankName, nameof(bankName), 160);
        AccountNumber = RequireValue(accountNumber, nameof(accountNumber), 34);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public static string NormalizeCode(string value)
    {
        return RequireValue(value, nameof(value), 40).ToUpperInvariant();
    }

    private static string RequireValue(string value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }
}
