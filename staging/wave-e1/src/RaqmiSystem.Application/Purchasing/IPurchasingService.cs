using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Purchasing;

namespace RaqmiSystem.Application.Purchasing;

/// <summary>
/// Purchasing module (module 12): supplier referential, purchase orders and the receptions
/// that feed the stock. HONEST SCOPE NOTE - purchase requisitions, requests for quotation and
/// supplier invoices are NOT covered by this wave; this interface deliberately stops at
/// suppliers, orders and receipts.
/// </summary>
public interface IPurchasingService
{
    Task<IReadOnlyCollection<SupplierResponse>> ListSuppliersAsync(
        string? search,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<SupplierResponse>> GetSupplierAsync(
        string code,
        CancellationToken cancellationToken);

    Task<ApplicationResult<SupplierResponse>> CreateSupplierAsync(
        CreateSupplierRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<SupplierResponse>> UpdateSupplierAsync(
        string code,
        UpdateSupplierRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<SupplierResponse>> SetSupplierActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PurchaseOrderResponse>> ListOrdersAsync(
        DateOnly? from,
        DateOnly? to,
        string? supplierCode,
        string? warehouseCode,
        PurchaseOrderStatus? status,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PurchaseOrderResponse>> GetOrderAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PurchaseOrderResponse>> CreateOrderAsync(
        CreatePurchaseOrderRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PurchaseOrderResponse>> UpdateOrderLinesAsync(
        Guid id,
        UpdatePurchaseOrderLinesRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PurchaseOrderResponse>> ApproveOrderAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PurchaseOrderResponse>> ReceiveOrderAsync(
        Guid id,
        ReceivePurchaseOrderRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PurchaseOrderResponse>> CancelOrderAsync(
        Guid id,
        CancelPurchaseOrderRequest request,
        OperationContext context,
        CancellationToken cancellationToken);
}
