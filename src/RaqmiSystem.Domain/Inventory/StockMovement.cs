using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Inventory;

/// <summary>
/// THE stock registry. The design decision of the whole module lives here: current stock is
/// NOT a column anywhere - it is the SUM of the movements recorded against a (warehouse, item)
/// pair. No denormalized counter means no possible desynchronization: a stock figure can never
/// contradict its own history, an audit can always re-derive it, and a correction is one more
/// movement rather than an overwrite. The price is a sum at read time, which stays cheap at
/// the scale of a hotel group and is guarded by an index on (warehouse_code, item_code).
///
/// Quantities are stored strictly positive; the direction comes from <see cref="Kind"/>
/// (and, for the two-way <see cref="StockMovementKind.InventoryAdjustment"/>, from
/// <see cref="AdjustmentIsIncrease"/>). A movement is immutable once written: the registry
/// only ever grows.
///
/// A TRANSFER is two linked movements (out of the source, into the destination) sharing one
/// <see cref="TransferGroupId"/>, created atomically by <see cref="CreateTransferPair"/> so a
/// quantity can never leave one warehouse without arriving in the other.
/// </summary>
public sealed class StockMovement : AuditableEntity
{
    private StockMovement()
    {
    }

    private StockMovement(
        string warehouseCode,
        string itemCode,
        DateOnly movementDate,
        StockMovementKind kind,
        decimal quantity,
        decimal? unitCost,
        string reference,
        string? lotNumber,
        DateOnly? expiryDate,
        string? notes,
        bool? adjustmentIsIncrease,
        Guid? transferGroupId)
    {
        WarehouseCode = Warehouse.NormalizeCode(warehouseCode);
        ItemCode = StockItem.NormalizeCode(itemCode);
        MovementDate = movementDate;
        Kind = kind;
        Quantity = RequireQuantity(quantity);
        UnitCost = RequireUnitCost(kind, unitCost);
        Reference = RequireValue(reference, nameof(reference), 80);
        LotNumber = NormalizeOptional(lotNumber, nameof(lotNumber), 60);
        ExpiryDate = expiryDate;
        Notes = NormalizeOptional(notes, nameof(notes), 500);
        AdjustmentIsIncrease = RequireAdjustmentDirection(kind, adjustmentIsIncrease);
        TransferGroupId = transferGroupId;
    }

    public string WarehouseCode { get; private set; } = string.Empty;

    public string ItemCode { get; private set; } = string.Empty;

    public DateOnly MovementDate { get; private set; }

    public StockMovementKind Kind { get; private set; }

    /// <summary>Always strictly positive, at most 3 decimal places; the sign comes from the kind.</summary>
    public decimal Quantity { get; private set; }

    /// <summary>
    /// Unit cost of the movement. MANDATORY for a purchase entry - it feeds the weighted
    /// average cost - and merely informative elsewhere (a transfer or an adjustment may carry
    /// the average cost known at the time, for traceability).
    /// </summary>
    public decimal? UnitCost { get; private set; }

    public string Reference { get; private set; } = string.Empty;

    public string? LotNumber { get; private set; }

    /// <summary>Expiry date of the lot, recorded for perishables and checked by reports, not by the registry.</summary>
    public DateOnly? ExpiryDate { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>
    /// Direction of an inventory adjustment (the one kind that can go both ways):
    /// true adds to stock, false removes from it. Null for every other kind.
    /// </summary>
    public bool? AdjustmentIsIncrease { get; private set; }

    /// <summary>Shared identifier of the two halves of a transfer; null for any other movement.</summary>
    public Guid? TransferGroupId { get; private set; }

    /// <summary>
    /// The quantity as it counts toward current stock: positive for what enters the
    /// warehouse, negative for what leaves it. Current stock of (warehouse, item) is the sum
    /// of this value over the registry - the single derivation rule of the module.
    /// </summary>
    public decimal SignedQuantity => IsInbound(Kind, AdjustmentIsIncrease) ? Quantity : -Quantity;

    public static StockMovement PurchaseEntry(
        string warehouseCode,
        string itemCode,
        DateOnly movementDate,
        decimal quantity,
        decimal unitCost,
        string reference,
        string? lotNumber = null,
        DateOnly? expiryDate = null,
        string? notes = null)
    {
        return new StockMovement(
            warehouseCode,
            itemCode,
            movementDate,
            StockMovementKind.PurchaseEntry,
            quantity,
            unitCost,
            reference,
            lotNumber,
            expiryDate,
            notes,
            adjustmentIsIncrease: null,
            transferGroupId: null);
    }

    public static StockMovement Consumption(
        string warehouseCode,
        string itemCode,
        DateOnly movementDate,
        decimal quantity,
        string reference,
        decimal? unitCost = null,
        string? notes = null)
    {
        return new StockMovement(
            warehouseCode,
            itemCode,
            movementDate,
            StockMovementKind.Consumption,
            quantity,
            unitCost,
            reference,
            lotNumber: null,
            expiryDate: null,
            notes,
            adjustmentIsIncrease: null,
            transferGroupId: null);
    }

    public static StockMovement InventoryAdjustment(
        string warehouseCode,
        string itemCode,
        DateOnly movementDate,
        decimal quantity,
        bool isIncrease,
        string reference,
        string? notes = null)
    {
        return new StockMovement(
            warehouseCode,
            itemCode,
            movementDate,
            StockMovementKind.InventoryAdjustment,
            quantity,
            unitCost: null,
            reference,
            lotNumber: null,
            expiryDate: null,
            notes,
            adjustmentIsIncrease: isIncrease,
            transferGroupId: null);
    }

    /// <summary>
    /// Builds the two halves of a transfer as one operation: the quantity that leaves the
    /// source and the quantity that enters the destination are the very same value, stamped
    /// with the same shared group id. Callers persist both in one transaction - a transfer
    /// with a single half is not representable through this factory.
    /// </summary>
    public static (StockMovement OutMovement, StockMovement InMovement) CreateTransferPair(
        string fromWarehouseCode,
        string toWarehouseCode,
        string itemCode,
        DateOnly movementDate,
        decimal quantity,
        string reference,
        decimal? unitCost = null,
        string? lotNumber = null,
        DateOnly? expiryDate = null,
        string? notes = null)
    {
        var normalizedFrom = Warehouse.NormalizeCode(fromWarehouseCode);
        var normalizedTo = Warehouse.NormalizeCode(toWarehouseCode);

        if (normalizedFrom == normalizedTo)
        {
            throw new ArgumentException(
                "A transfer requires two distinct warehouses.",
                nameof(toWarehouseCode));
        }

        var transferGroupId = Guid.NewGuid();

        var outMovement = new StockMovement(
            normalizedFrom,
            itemCode,
            movementDate,
            StockMovementKind.TransferOut,
            quantity,
            unitCost,
            reference,
            lotNumber,
            expiryDate,
            notes,
            adjustmentIsIncrease: null,
            transferGroupId);

        var inMovement = new StockMovement(
            normalizedTo,
            itemCode,
            movementDate,
            StockMovementKind.TransferIn,
            quantity,
            unitCost,
            reference,
            lotNumber,
            expiryDate,
            notes,
            adjustmentIsIncrease: null,
            transferGroupId);

        return (outMovement, inMovement);
    }

    /// <summary>
    /// Single source of truth for the direction rule: what a kind does to current stock.
    /// Exposed so read models (stock state, low-stock alerts) sum the registry with the very
    /// same rule the entity applies - neither side may restate it.
    /// </summary>
    public static bool IsInbound(StockMovementKind kind, bool? adjustmentIsIncrease)
    {
        return kind switch
        {
            StockMovementKind.PurchaseEntry => true,
            StockMovementKind.TransferIn => true,
            StockMovementKind.Consumption => false,
            StockMovementKind.TransferOut => false,
            StockMovementKind.InventoryAdjustment => adjustmentIsIncrease
                ?? throw new InvalidOperationException("An inventory adjustment carries an explicit direction."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Movement kind is not valid.")
        };
    }

    /// <summary>
    /// Quantities are stored with 3 decimals (numeric(18,3)); a finer value would be silently
    /// truncated at persistence time and the registry sum would stop matching what the user
    /// validated on screen - refuse it upfront. Same rule as InvoiceLine quantities.
    /// </summary>
    private static decimal RequireQuantity(decimal value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Quantity must be strictly positive.");
        }

        if (decimal.Round(value, 3) != value)
        {
            throw new ArgumentException("Quantity cannot have more than 3 decimal places.", nameof(value));
        }

        return value;
    }

    private static decimal? RequireUnitCost(StockMovementKind kind, decimal? unitCost)
    {
        if (kind == StockMovementKind.PurchaseEntry && unitCost is null)
        {
            throw new ArgumentException(
                "A unit cost is required for a purchase entry: it feeds the weighted average cost.",
                nameof(unitCost));
        }

        if (unitCost is null)
        {
            return null;
        }

        if (unitCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitCost), unitCost, "Unit cost cannot be negative.");
        }

        // Costs are money: 2 decimal places (numeric(18,2)), same rule as invoice unit prices.
        if (decimal.Round(unitCost.Value, 2) != unitCost.Value)
        {
            throw new ArgumentException("Unit cost cannot have more than 2 decimal places.", nameof(unitCost));
        }

        return unitCost;
    }

    private static bool? RequireAdjustmentDirection(StockMovementKind kind, bool? adjustmentIsIncrease)
    {
        if (kind == StockMovementKind.InventoryAdjustment)
        {
            if (adjustmentIsIncrease is null)
            {
                throw new ArgumentException(
                    "An inventory adjustment requires an explicit direction.",
                    nameof(adjustmentIsIncrease));
            }

            return adjustmentIsIncrease;
        }

        if (adjustmentIsIncrease is not null)
        {
            throw new ArgumentException(
                "Only an inventory adjustment carries an adjustment direction.",
                nameof(adjustmentIsIncrease));
        }

        return null;
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
}
