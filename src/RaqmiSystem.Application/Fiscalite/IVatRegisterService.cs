using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Fiscalite;

namespace RaqmiSystem.Application.Fiscalite;

/// <summary>
/// VAT sales and purchases registers. <see cref="RegisterSaleAsync"/> and
/// <see cref="RegisterPurchaseAsync"/> are called internally, in the same transaction, by
/// BillingService.IssueInvoiceAsync and PurchasingService.ReceiveOrderAsync - a chronological
/// legal register, written once at the source event, never recomputed from a report query.
/// </summary>
public interface IVatRegisterService
{
    Task<IReadOnlyCollection<VatSalesRegisterEntryResponse>> GetSalesRegisterAsync(
        int year, int month, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<VatPurchaseRegisterEntryResponse>> GetPurchasesRegisterAsync(
        int year, int month, CancellationToken cancellationToken);

    Task RegisterSaleAsync(RegisterVatSaleRequest request, OperationContext context, CancellationToken cancellationToken);

    Task RegisterPurchaseAsync(RegisterVatPurchaseRequest request, OperationContext context, CancellationToken cancellationToken);

    Task<ApplicationResult<VatPurchaseRegisterEntryResponse>> CreateManualPurchaseEntryAsync(
        CreateVatPurchaseEntryRequest request, OperationContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Imports every approved-and-received purchase order of the period not already present in
    /// the register (matched by PurchaseOrderId), mirroring the legacy "Importer bons validés".
    /// Returns the number of lines imported.
    /// </summary>
    Task<ApplicationResult<int>> ImportApprovedPurchaseOrdersAsync(
        int year, int month, OperationContext context, CancellationToken cancellationToken);
}

public sealed record RegisterVatSaleRequest(
    Guid InvoiceId, string PieceNumber, DateOnly PieceDate, VatMovementType Type,
    string CustomerName, string? CustomerNif, decimal BaseHt, decimal VatAmount);

public sealed record RegisterVatPurchaseRequest(
    Guid PurchaseOrderId, string PieceNumber, DateOnly PieceDate,
    string SupplierName, string? SupplierNif, decimal BaseHt, decimal VatAmount);

public sealed record CreateVatPurchaseEntryRequest(
    string PieceNumber, DateOnly PieceDate, string SupplierName, string? SupplierNif, decimal BaseHt, decimal VatAmount);

public sealed record VatSalesRegisterEntryResponse(
    Guid Id, Guid InvoiceId, string PieceNumber, DateOnly PieceDate, VatMovementType Type,
    string CustomerName, string? CustomerNif, decimal BaseHt, decimal VatAmount, decimal Ttc);

public sealed record VatPurchaseRegisterEntryResponse(
    Guid Id, Guid? PurchaseOrderId, string PieceNumber, DateOnly PieceDate,
    string SupplierName, string? SupplierNif, decimal BaseHt, decimal VatAmount, decimal Ttc, VatRegisterSource Source);
