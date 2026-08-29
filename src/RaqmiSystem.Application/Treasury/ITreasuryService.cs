using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Application.Treasury;

public interface ITreasuryService
{
    Task<IReadOnlyCollection<BankAccountResponse>> ListBankAccountsAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<BankAccountResponse>> GetBankAccountAsync(
        string code,
        CancellationToken cancellationToken);

    Task<ApplicationResult<BankAccountResponse>> CreateBankAccountAsync(
        CreateBankAccountRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<BankAccountResponse>> UpdateBankAccountAsync(
        string code,
        UpdateBankAccountRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<BankAccountResponse>> SetBankAccountActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CashReceiptResponse>> ListReceiptsAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        PaymentMethod? method,
        ReceiptStatus? status,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CashReceiptResponse>> GetReceiptAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CashReceiptResponse>> CreateReceiptAsync(
        CreateCashReceiptRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CashReceiptResponse>> UpdateReceiptAsync(
        Guid id,
        UpdateCashReceiptRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CashReceiptResponse>> ConfirmReceiptAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CashReceiptResponse>> CancelReceiptAsync(
        Guid id,
        CancelCashReceiptRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CashReceiptSummaryResponse>> GetReceiptSummaryAsync(
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        ReceiptStatus? status,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PaymentOrderResponse>> ListPaymentOrdersAsync(
        DateOnly? from,
        DateOnly? to,
        string? bankAccountCode,
        PaymentOrderStatus? status,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PaymentOrderResponse>> GetPaymentOrderAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PaymentOrderResponse>> CreatePaymentOrderAsync(
        CreatePaymentOrderRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PaymentOrderResponse>> ApprovePaymentOrderAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PaymentOrderResponse>> PayPaymentOrderAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PaymentOrderResponse>> CancelPaymentOrderAsync(
        Guid id,
        CancelPaymentOrderRequest request,
        OperationContext context,
        CancellationToken cancellationToken);
}
