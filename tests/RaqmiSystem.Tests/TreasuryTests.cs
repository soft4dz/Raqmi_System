using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Tests;

public sealed class TreasuryTests
{
    [Fact]
    public void Bank_account_normalizes_code_and_starts_active()
    {
        var account = new BankAccount(" biat-main ", " Compte principal ", "BIAT", "TN5904018104003691234567");

        Assert.Equal("BIAT-MAIN", account.Code);
        Assert.Equal("Compte principal", account.Label);
        Assert.True(account.IsActive);

        account.Deactivate();
        Assert.False(account.IsActive);

        account.Activate();
        Assert.True(account.IsActive);
    }

    [Fact]
    public void Bank_account_rejects_account_number_longer_than_34_characters()
    {
        Assert.Throws<ArgumentException>(() =>
            new BankAccount("BIAT-MAIN", "Compte principal", "BIAT", new string('9', 35)));
    }

    [Fact]
    public void Cash_receipt_accepts_cash_without_reference_or_bank_account()
    {
        var receipt = new CashReceipt(
            new DateOnly(2026, 8, 1),
            " el-manar ",
            PaymentMethod.Cash,
            150_000m);

        Assert.Equal("EL-MANAR", receipt.HotelUnitCode);
        Assert.Null(receipt.Reference);
        Assert.Null(receipt.BankAccountCode);
        Assert.Equal(ReceiptStatus.Draft, receipt.Status);
        Assert.True(receipt.CanEdit);
    }

    [Theory]
    [InlineData(PaymentMethod.Cheque)]
    [InlineData(PaymentMethod.BankTransfer)]
    public void Cash_receipt_requires_a_reference_for_cheque_and_bank_transfer(PaymentMethod method)
    {
        Assert.Throws<ArgumentException>(() =>
            new CashReceipt(
                new DateOnly(2026, 8, 1),
                "EL-MANAR",
                method,
                100m,
                reference: null,
                bankAccountCode: "BIAT-MAIN"));
    }

    [Theory]
    [InlineData(PaymentMethod.Card)]
    [InlineData(PaymentMethod.Cheque)]
    [InlineData(PaymentMethod.BankTransfer)]
    public void Cash_receipt_requires_a_bank_account_for_non_cash_methods(PaymentMethod method)
    {
        Assert.Throws<ArgumentException>(() =>
            new CashReceipt(
                new DateOnly(2026, 8, 1),
                "EL-MANAR",
                method,
                100m,
                reference: "CHQ-0001",
                bankAccountCode: null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Cash_receipt_rejects_non_positive_amounts(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CashReceipt(new DateOnly(2026, 8, 1), "EL-MANAR", PaymentMethod.Cash, amount));
    }

    [Fact]
    public void Confirmed_receipt_cannot_be_edited_or_confirmed_again()
    {
        var receipt = new CashReceipt(new DateOnly(2026, 8, 1), "EL-MANAR", PaymentMethod.Cash, 100m);

        receipt.Confirm("cashier", DateTimeOffset.UtcNow);

        Assert.Equal(ReceiptStatus.Confirmed, receipt.Status);
        Assert.False(receipt.CanEdit);
        Assert.Equal("cashier", receipt.ConfirmedBy);

        Assert.Throws<InvalidOperationException>(() =>
            receipt.Update(new DateOnly(2026, 8, 2), "EL-MANAR", PaymentMethod.Cash, 200m, null, null, null));

        Assert.Throws<InvalidOperationException>(() =>
            receipt.Confirm("cashier", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Receipt_cancellation_requires_a_reason_and_is_final()
    {
        var receipt = new CashReceipt(new DateOnly(2026, 8, 1), "EL-MANAR", PaymentMethod.Cash, 100m);

        Assert.Throws<ArgumentException>(() =>
            receipt.Cancel("  ", "cashier", DateTimeOffset.UtcNow));

        receipt.Cancel("Duplicate entry.", "cashier", DateTimeOffset.UtcNow);

        Assert.Equal(ReceiptStatus.Cancelled, receipt.Status);
        Assert.Equal("Duplicate entry.", receipt.CancelReason);

        Assert.Throws<InvalidOperationException>(() =>
            receipt.Cancel("Again.", "cashier", DateTimeOffset.UtcNow));

        Assert.Throws<InvalidOperationException>(() =>
            receipt.Confirm("cashier", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Payment_order_follows_draft_approved_paid_transitions()
    {
        var order = new PaymentOrder(
            new DateOnly(2026, 8, 1),
            "Fournisseur SARL",
            5_000m,
            new DateOnly(2026, 8, 15),
            " biat-main ",
            "INV-2026-042");

        Assert.Equal("BIAT-MAIN", order.BankAccountCode);
        Assert.Equal(PaymentOrderStatus.Draft, order.Status);

        Assert.Throws<InvalidOperationException>(() =>
            order.MarkPaid("cashier", DateTimeOffset.UtcNow));

        order.Approve("director", DateTimeOffset.UtcNow);
        Assert.Equal(PaymentOrderStatus.Approved, order.Status);
        Assert.Equal("director", order.ApprovedBy);

        Assert.Throws<InvalidOperationException>(() =>
            order.Approve("director", DateTimeOffset.UtcNow));

        order.MarkPaid("cashier", DateTimeOffset.UtcNow);
        Assert.Equal(PaymentOrderStatus.Paid, order.Status);
        Assert.Equal("cashier", order.PaidBy);
    }

    [Fact]
    public void Paid_payment_order_cannot_be_cancelled()
    {
        var order = new PaymentOrder(
            new DateOnly(2026, 8, 1),
            "Fournisseur SARL",
            5_000m,
            new DateOnly(2026, 8, 15),
            "BIAT-MAIN");

        order.Approve("director", DateTimeOffset.UtcNow);
        order.MarkPaid("cashier", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            order.Cancel("Too late.", "director", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Payment_order_cancellation_requires_a_reason()
    {
        var order = new PaymentOrder(
            new DateOnly(2026, 8, 1),
            "Fournisseur SARL",
            5_000m,
            new DateOnly(2026, 8, 15),
            "BIAT-MAIN");

        Assert.Throws<ArgumentException>(() =>
            order.Cancel(" ", "director", DateTimeOffset.UtcNow));

        order.Cancel("Wrong beneficiary.", "director", DateTimeOffset.UtcNow);

        Assert.Equal(PaymentOrderStatus.Cancelled, order.Status);
        Assert.Equal("Wrong beneficiary.", order.CancelReason);
    }

    [Fact]
    public void Payment_order_rejects_a_due_date_earlier_than_the_order_date()
    {
        Assert.Throws<ArgumentException>(() =>
            new PaymentOrder(
                new DateOnly(2026, 8, 15),
                "Fournisseur SARL",
                5_000m,
                new DateOnly(2026, 8, 14),
                "BIAT-MAIN"));

        // Same-day settlement stays valid: the invariant is DueDate >= OrderDate.
        var sameDayOrder = new PaymentOrder(
            new DateOnly(2026, 8, 15),
            "Fournisseur SARL",
            5_000m,
            new DateOnly(2026, 8, 15),
            "BIAT-MAIN");

        Assert.Equal(sameDayOrder.OrderDate, sameDayOrder.DueDate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public void Payment_order_rejects_non_positive_amounts(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PaymentOrder(
                new DateOnly(2026, 8, 1),
                "Fournisseur SARL",
                amount,
                new DateOnly(2026, 8, 15),
                "BIAT-MAIN"));
    }
}
