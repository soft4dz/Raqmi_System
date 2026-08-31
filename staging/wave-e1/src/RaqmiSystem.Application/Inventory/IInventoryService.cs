using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Application.Inventory;

/// <summary>
/// Stocks and consumptions module: warehouses and items administration, the movement
/// registry (entries, consumptions, transfers, adjustments), current stock with its
/// weighted-average valuation, low-stock alerts and physical inventory counts.
///
/// Doctrine of the module: stock is never a stored counter - it is always derived by
/// summing the movement registry (see StockMovement). Every operation that takes stock
/// OUT re-checks the resulting balance inside an atomic guard so the registry can never
/// sum to a negative quantity.
/// </summary>
public interface IInventoryService
{
    // ------------------------------ Warehouses ------------------------------

    Task<IReadOnlyCollection<WarehouseResponse>> ListWarehousesAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<WarehouseResponse>> CreateWarehouseAsync(
        CreateWarehouseRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<WarehouseResponse>> UpdateWarehouseAsync(
        string code,
        UpdateWarehouseRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<WarehouseResponse>> SetWarehouseActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    // -------------------------------- Items ---------------------------------

    Task<IReadOnlyCollection<StockItemResponse>> ListItemsAsync(
        string? search,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<StockItemResponse>> CreateItemAsync(
        CreateStockItemRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<StockItemResponse>> UpdateItemAsync(
        string code,
        UpdateStockItemRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<StockItemResponse>> SetItemActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    // ------------------------------ Movements -------------------------------

    Task<IReadOnlyCollection<StockMovementResponse>> ListMovementsAsync(
        DateOnly? from,
        DateOnly? to,
        string? warehouseCode,
        string? itemCode,
        StockMovementKind? kind,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records one movement (purchase entry, consumption or manual adjustment). An outgoing
    /// movement is refused with the available stock in the message when it would make the
    /// registry sum negative. Transfer kinds are refused here: use
    /// <see cref="TransferAsync"/> so both halves are created atomically.
    /// </summary>
    Task<ApplicationResult<StockMovementResponse>> CreateMovementAsync(
        CreateStockMovementRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records an inter-warehouse transfer as its two linked movements in one transaction,
    /// guarded against making the source warehouse negative.
    /// </summary>
    Task<ApplicationResult<StockTransferResponse>> TransferAsync(
        CreateStockTransferRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    // ---------------------------- Stock and alerts --------------------------

    /// <summary>
    /// Current stock of one warehouse: per-item quantity (registry sum), weighted average
    /// cost, stock value, below-threshold flag, and the warehouse's total valuation.
    /// Items with no movement in the warehouse are omitted; items whose balance returned to
    /// zero are kept visible as long as they have a history there.
    /// </summary>
    Task<ApplicationResult<WarehouseStockResponse>> GetWarehouseStockAsync(
        string warehouseCode,
        CancellationToken cancellationToken);

    /// <summary>Items strictly below their minimum threshold, per warehouse (active pairs only).</summary>
    Task<IReadOnlyCollection<LowStockRow>> GetLowStockAsync(
        CancellationToken cancellationToken);

    // --------------------------- Inventory counts ---------------------------

    Task<IReadOnlyCollection<InventoryCountResponse>> ListCountsAsync(
        string? warehouseCode,
        InventoryCountStatus? status,
        CancellationToken cancellationToken);

    Task<ApplicationResult<InventoryCountResponse>> GetCountAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ApplicationResult<InventoryCountResponse>> CreateCountAsync(
        CreateInventoryCountRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<InventoryCountResponse>> ReplaceCountLinesAsync(
        Guid id,
        ReplaceInventoryCountLinesRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Validates a draft count: generates one adjustment movement per counted line whose
    /// quantity differs from the theoretical stock (counted minus theoretical, in either
    /// direction), then freezes the count for good. Atomic: a concurrent validation is
    /// answered with a conflict, never with duplicated adjustments.
    /// </summary>
    Task<ApplicationResult<InventoryCountValidationResponse>> ValidateCountAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken);
}
