using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Treasury;

public sealed class PaymentOrder : AuditableEntity
{
    private PaymentOrder()
    {
    }

    public PaymentOrder(
        DateOnly orderDate,
        string beneficiary,
        decimal amount,
        DateOnly dueDate,
        string bankAccountCode,
        string? reference = null)
    {
        if (dueDate < orderDate)
        {
            throw new ArgumentException("Due date cannot be earlier than the order date.", nameof(dueDate));
        }

        OrderDate = orderDate;
        Beneficiary = RequireValue(beneficiary, nameof(beneficiary), 200);
        Amount = RequireStrictlyPositive(amount, nameof(amount));
        DueDate = dueDate;
        BankAccountCode = BankAccount.NormalizeCode(bankAccountCode);
        Reference = NormalizeOptional(reference, nameof(reference), 80);
        Status = PaymentOrderStatus.Draft;
    }

    public DateOnly OrderDate { get; private set; }

    public string Beneficiary { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public DateOnly DueDate { get; private set; }

    public string BankAccountCode { get; private set; } = string.Empty;

    public string? Reference { get; private set; }

    public PaymentOrderStatus Status { get; private set; } = PaymentOrderStatus.Draft;

    public DateTimeOffset? ApprovedAt { get; private set; }

    public string? ApprovedBy { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public string? PaidBy { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancelledBy { get; private set; }

    public string? CancelReason { get; private set; }

    public void Approve(string userName, DateTimeOffset utcNow)
    {
        if (Status != PaymentOrderStatus.Draft)
        {
            throw new InvalidOperationException("Only draft payment orders can be approved.");
        }

        Status = PaymentOrderStatus.Approved;
        ApprovedAt = utcNow;
        ApprovedBy = RequireActor(userName);
    }

    public void MarkPaid(string userName, DateTimeOffset utcNow)
    {
        if (Status != PaymentOrderStatus.Approved)
        {
            throw new InvalidOperationException("Only approved payment orders can be marked as paid.");
        }

        Status = PaymentOrderStatus.Paid;
        PaidAt = utcNow;
        PaidBy = RequireActor(userName);
    }

    public void Cancel(string reason, string userName, DateTimeOffset utcNow)
    {
        if (Status == PaymentOrderStatus.Paid)
        {
            throw new InvalidOperationException("A paid payment order cannot be cancelled.");
        }

        if (Status == PaymentOrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Payment order is already cancelled.");
        }

        // Validate before mutating so a rejected reason leaves the order untouched.
        var cancelReason = RequireValue(reason, nameof(reason), 500);

        Status = PaymentOrderStatus.Cancelled;
        CancelledAt = utcNow;
        CancelledBy = RequireActor(userName);
        CancelReason = cancelReason;
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
