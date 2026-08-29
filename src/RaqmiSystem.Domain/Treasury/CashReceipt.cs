using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Treasury;

public sealed class CashReceipt : AuditableEntity
{
    private CashReceipt()
    {
    }

    public CashReceipt(
        DateOnly receiptDate,
        string hotelUnitCode,
        PaymentMethod method,
        decimal amount,
        string? reference = null,
        string? bankAccountCode = null,
        string? notes = null)
    {
        ReceiptDate = receiptDate;
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Method = RequireDefinedMethod(method);
        Amount = RequireStrictlyPositive(amount, nameof(amount));
        Reference = NormalizeReference(method, reference);
        BankAccountCode = NormalizeBankAccountCode(method, bankAccountCode);
        Notes = NormalizeOptional(notes, nameof(notes), 1000);
        Status = ReceiptStatus.Draft;
    }

    public DateOnly ReceiptDate { get; private set; }

    public string HotelUnitCode { get; private set; } = string.Empty;

    public PaymentMethod Method { get; private set; } = PaymentMethod.Cash;

    public decimal Amount { get; private set; }

    public string? Reference { get; private set; }

    public string? BankAccountCode { get; private set; }

    public string? Notes { get; private set; }

    public ReceiptStatus Status { get; private set; } = ReceiptStatus.Draft;

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public string? ConfirmedBy { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancelledBy { get; private set; }

    public string? CancelReason { get; private set; }

    public bool CanEdit => Status == ReceiptStatus.Draft;

    public void Update(
        DateOnly receiptDate,
        string hotelUnitCode,
        PaymentMethod method,
        decimal amount,
        string? reference,
        string? bankAccountCode,
        string? notes)
    {
        if (!CanEdit)
        {
            throw new InvalidOperationException("Only draft receipts can be edited.");
        }

        ReceiptDate = receiptDate;
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Method = RequireDefinedMethod(method);
        Amount = RequireStrictlyPositive(amount, nameof(amount));
        Reference = NormalizeReference(method, reference);
        BankAccountCode = NormalizeBankAccountCode(method, bankAccountCode);
        Notes = NormalizeOptional(notes, nameof(notes), 1000);
    }

    public void Confirm(string userName, DateTimeOffset utcNow)
    {
        if (Status != ReceiptStatus.Draft)
        {
            throw new InvalidOperationException("Only draft receipts can be confirmed.");
        }

        Status = ReceiptStatus.Confirmed;
        ConfirmedAt = utcNow;
        ConfirmedBy = RequireActor(userName);
    }

    public void Cancel(string reason, string userName, DateTimeOffset utcNow)
    {
        if (Status == ReceiptStatus.Cancelled)
        {
            throw new InvalidOperationException("Receipt is already cancelled.");
        }

        // Validate before mutating so a rejected reason leaves the receipt untouched.
        var cancelReason = RequireValue(reason, nameof(reason), 500);

        Status = ReceiptStatus.Cancelled;
        CancelledAt = utcNow;
        CancelledBy = RequireActor(userName);
        CancelReason = cancelReason;
    }

    public static bool RequiresReference(PaymentMethod method)
    {
        return method is PaymentMethod.Cheque or PaymentMethod.BankTransfer;
    }

    public static bool RequiresBankAccount(PaymentMethod method)
    {
        return method is PaymentMethod.Card or PaymentMethod.Cheque or PaymentMethod.BankTransfer;
    }

    private static string? NormalizeReference(PaymentMethod method, string? reference)
    {
        if (RequiresReference(method) && string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException(
                "A reference is required for cheque and bank transfer receipts.",
                nameof(reference));
        }

        return NormalizeOptional(reference, nameof(reference), 80);
    }

    private static string? NormalizeBankAccountCode(PaymentMethod method, string? bankAccountCode)
    {
        if (RequiresBankAccount(method) && string.IsNullOrWhiteSpace(bankAccountCode))
        {
            throw new ArgumentException(
                "A bank account is required for card, cheque and bank transfer receipts.",
                nameof(bankAccountCode));
        }

        return string.IsNullOrWhiteSpace(bankAccountCode)
            ? null
            : BankAccount.NormalizeCode(bankAccountCode);
    }

    private static PaymentMethod RequireDefinedMethod(PaymentMethod method)
    {
        if (!Enum.IsDefined(method))
        {
            throw new ArgumentException("Payment method is not valid.", nameof(method));
        }

        return method;
    }

    private static string RequireActor(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return "system";
        }

        return userName.Trim();
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

    private static string? NormalizeOptional(string? value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }

    private static decimal RequireStrictlyPositive(decimal value, string argumentName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "Value must be strictly positive.");
        }

        return value;
    }
}
