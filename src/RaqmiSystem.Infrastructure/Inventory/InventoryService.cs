using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Inventory;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Inventory;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Inventory;

/// <summary>
/// Stocks and consumptions module (module 11). Implements the module's own contract
/// (<see cref="IInventoryService"/>) and the two contracts it PUBLISHES to the rest of the wave:
/// <see cref="IStockOperationService"/> (purchase receipts turned into entry movements, consumed
/// by the purchasing module) and <see cref="IStockCostProvider"/> (weighted average cost of an
/// item, consumed by the purchasing and kitchen modules). Publishing them from one class is
/// deliberate: the three contracts share the very same registry and the very same derivation
/// rules, and a second implementation could drift from this one.
///
/// THE TWO DOCTRINES OF THE MODULE
///
/// 1. Stock is never stored. It is the SUM of the movement registry over a (warehouse, item)
///    pair, always recomputed at read time - see <see cref="StockMovement"/>. Nothing in this
///    class writes, caches or reconciles a quantity column, because there is none to drift.
///    The direction of each movement comes from <see cref="StockMovement.IsInbound"/>, the
///    domain's single statement of the rule: this service never restates "an entry adds, a
///    consumption removes" in a SQL projection of its own.
///
/// 2. Stock can never go negative. Every outflow (a consumption, a decreasing adjustment, the
///    outgoing half of a transfer) re-derives the balance INSIDE a Serializable transaction and
///    commits the movement in that same transaction. Checked outside of one, two concurrent
///    outflows both read "there are 10 left", both take 8, and the registry sums to -6. Under
///    PostgreSQL the loser's commit is refused with a serialization failure; under the SQLite
///    test provider its write is turned away with "database is locked". Both surface as a
///    retryable 409 - same guard as LodgingService's anti-double-booking.
///
/// Sums are computed in memory over a narrow projection of the registry rather than by a SQL
/// aggregate. That is the repository's constant practice for decimal aggregates (the SQLite test
/// provider stores decimal as TEXT, where SUM does not mean what it says), it keeps the
/// arithmetic exact, and it is what the domain itself describes: "a sum at read time, which stays
/// cheap at the scale of a hotel group and is guarded by an index on (warehouse_code, item_code)".
/// </summary>
public sealed class InventoryService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter) : IInventoryService, IStockOperationService, IStockCostProvider
{
    private const string WarehousesEntity = "inventory.warehouses";

    private const string ItemsEntity = "inventory.stock_items";

    private const string MovementsEntity = "inventory.stock_movements";

    private const string CountsEntity = "inventory.inventory_counts";

    /// <summary>
    /// Answer given when a Serializable transaction of this module was rolled back whole because
    /// another request was writing the same stock at the same instant. Nothing was written, so
    /// the caller may simply try again on a freshly read balance.
    /// </summary>
    private const string ConcurrentStockMutationRefused =
        "Another stock operation on the same warehouse was committed while this one was being " +
        "checked, so it was rolled back and nothing was written. Reload the stock and try again.";

    private const string ConcurrentCountMutationRefused =
        "This inventory count was just validated by a concurrent operation, so this change was " +
        "rolled back and nothing was modified. Reload the count and try again.";

    // ============================== Warehouses ==============================

    public async Task<IReadOnlyCollection<WarehouseResponse>> ListWarehousesAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Warehouse>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(warehouse => warehouse.IsActive);
        }

        var warehouses = await query
            .OrderBy(warehouse => warehouse.Code)
            .ToArrayAsync(cancellationToken);

        return warehouses.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<WarehouseResponse>> CreateWarehouseAsync(
        CreateWarehouseRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        Warehouse warehouse;

        try
        {
            warehouse = new Warehouse(request.Code, request.Label, request.HotelUnitCode);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<WarehouseResponse>.Validation(ex.Message);
        }

        var unitExists = await dbContext.Set<HotelUnit>()
            .AnyAsync(unit => unit.Code == warehouse.HotelUnitCode, cancellationToken);

        if (!unitExists)
        {
            return ApplicationResult<WarehouseResponse>.Validation(
                $"Hotel unit '{warehouse.HotelUnitCode}' does not exist.");
        }

        var exists = await dbContext.Set<Warehouse>()
            .AnyAsync(current => current.Code == warehouse.Code, cancellationToken);

        if (exists)
        {
            return ApplicationResult<WarehouseResponse>.Conflict("A warehouse with this code already exists.");
        }

        warehouse.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<Warehouse>().Add(warehouse);

        try
        {
            await WriteAuditAsync(
                "inventory.warehouse.created",
                WarehousesEntity,
                warehouse.Id,
                context,
                new { warehouse.Code, warehouse.Label, warehouse.HotelUnitCode },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // The exists-check above and this insert are not atomic: a concurrent create with the
            // same code loses the race against ux_warehouses_code.
            return ApplicationResult<WarehouseResponse>.Conflict("A warehouse with this code already exists.");
        }

        return ApplicationResult<WarehouseResponse>.Success(Map(warehouse));
    }

    public async Task<ApplicationResult<WarehouseResponse>> UpdateWarehouseAsync(
        string code,
        UpdateWarehouseRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var warehouse = await LoadWarehouseAsync(code, track: true, cancellationToken);

        if (warehouse is null)
        {
            return ApplicationResult<WarehouseResponse>.NotFound("Warehouse was not found.");
        }

        string normalizedUnitCode;

        try
        {
            normalizedUnitCode = HotelUnit.NormalizeCode(request.HotelUnitCode);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<WarehouseResponse>.Validation(ex.Message);
        }

        var unitExists = await dbContext.Set<HotelUnit>()
            .AnyAsync(unit => unit.Code == normalizedUnitCode, cancellationToken);

        if (!unitExists)
        {
            return ApplicationResult<WarehouseResponse>.Validation(
                $"Hotel unit '{normalizedUnitCode}' does not exist.");
        }

        try
        {
            warehouse.UpdateDetails(request.Label, request.HotelUnitCode);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<WarehouseResponse>.Validation(ex.Message);
        }

        warehouse.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "inventory.warehouse.updated",
            WarehousesEntity,
            warehouse.Id,
            context,
            new { warehouse.Code, warehouse.Label, warehouse.HotelUnitCode },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<WarehouseResponse>.Success(Map(warehouse));
    }

    public async Task<ApplicationResult<WarehouseResponse>> SetWarehouseActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var warehouse = await LoadWarehouseAsync(code, track: true, cancellationToken);

        if (warehouse is null)
        {
            return ApplicationResult<WarehouseResponse>.NotFound("Warehouse was not found.");
        }

        if (isActive)
        {
            warehouse.Activate();
        }
        else
        {
            // A warehouse is deactivated, never deleted: its movements are the proof behind every
            // stock figure ever shown for it. Deactivating only removes it from the capture lists.
            warehouse.Deactivate();
        }

        warehouse.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "inventory.warehouse.activated" : "inventory.warehouse.deactivated",
            WarehousesEntity,
            warehouse.Id,
            context,
            new { warehouse.Code, warehouse.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<WarehouseResponse>.Success(Map(warehouse));
    }

    // ================================ Items =================================

    public async Task<IReadOnlyCollection<StockItemResponse>> ListItemsAsync(
        string? search,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<StockItem>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(item => item.IsActive);
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim().ToUpperInvariant();

        if (normalizedSearch is not null)
        {
            query = query.Where(item =>
                item.Code.Contains(normalizedSearch) ||
                item.Designation.ToUpper().Contains(normalizedSearch));
        }

        var items = await query
            .OrderBy(item => item.Code)
            .ToArrayAsync(cancellationToken);

        return items.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<StockItemResponse>> CreateItemAsync(
        CreateStockItemRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        StockItem item;

        try
        {
            item = new StockItem(
                request.Code,
                request.Designation,
                request.UnitOfMeasure,
                request.Category,
                request.MinimumQuantity);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<StockItemResponse>.Validation(ex.Message);
        }

        var exists = await dbContext.Set<StockItem>()
            .AnyAsync(current => current.Code == item.Code, cancellationToken);

        if (exists)
        {
            return ApplicationResult<StockItemResponse>.Conflict("A stock item with this code already exists.");
        }

        item.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<StockItem>().Add(item);

        try
        {
            await WriteAuditAsync(
                "inventory.item.created",
                ItemsEntity,
                item.Id,
                context,
                new { item.Code, item.Designation, item.UnitOfMeasure, Category = item.Category.ToString(), item.MinimumQuantity },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<StockItemResponse>.Conflict("A stock item with this code already exists.");
        }

        return ApplicationResult<StockItemResponse>.Success(Map(item));
    }

    public async Task<ApplicationResult<StockItemResponse>> UpdateItemAsync(
        string code,
        UpdateStockItemRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var item = await LoadItemAsync(code, track: true, cancellationToken);

        if (item is null)
        {
            return ApplicationResult<StockItemResponse>.NotFound("Stock item was not found.");
        }

        try
        {
            item.UpdateDetails(
                request.Designation,
                request.UnitOfMeasure,
                request.Category,
                request.MinimumQuantity);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<StockItemResponse>.Validation(ex.Message);
        }

        item.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "inventory.item.updated",
            ItemsEntity,
            item.Id,
            context,
            new { item.Code, item.Designation, item.UnitOfMeasure, Category = item.Category.ToString(), item.MinimumQuantity },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<StockItemResponse>.Success(Map(item));
    }

    public async Task<ApplicationResult<StockItemResponse>> SetItemActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var item = await LoadItemAsync(code, track: true, cancellationToken);

        if (item is null)
        {
            return ApplicationResult<StockItemResponse>.NotFound("Stock item was not found.");
        }

        if (isActive)
        {
            item.Activate();
        }
        else
        {
            item.Deactivate();
        }

        item.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "inventory.item.activated" : "inventory.item.deactivated",
            ItemsEntity,
            item.Id,
            context,
            new { item.Code, item.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<StockItemResponse>.Success(Map(item));
    }

    // ============================== Movements ===============================

    public async Task<IReadOnlyCollection<StockMovementResponse>> ListMovementsAsync(
        DateOnly? from,
        DateOnly? to,
        string? warehouseCode,
        string? itemCode,
        StockMovementKind? kind,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<StockMovement>().AsNoTracking();

        if (from.HasValue)
        {
            query = query.Where(movement => movement.MovementDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(movement => movement.MovementDate <= to.Value);
        }

        var normalizedWarehouse = NormalizeNullableCode(warehouseCode);

        if (normalizedWarehouse is not null)
        {
            query = query.Where(movement => movement.WarehouseCode == normalizedWarehouse);
        }

        var normalizedItem = NormalizeNullableCode(itemCode);

        if (normalizedItem is not null)
        {
            query = query.Where(movement => movement.ItemCode == normalizedItem);
        }

        if (kind.HasValue)
        {
            query = query.Where(movement => movement.Kind == kind.Value);
        }

        var movements = await query.ToArrayAsync(cancellationToken);

        // Sorted in memory: the SQLite provider used by the test harness translates neither
        // ORDER BY nor comparison on DateTimeOffset, and CreatedAt is the tie-breaker that makes
        // several movements captured on the same business date come back in capture order.
        var ordered = movements
            .OrderByDescending(movement => movement.MovementDate)
            .ThenByDescending(movement => movement.CreatedAt)
            .ToArray();

        var items = await LoadItemLabelsAsync(
            ordered.Select(movement => movement.ItemCode).Distinct().ToArray(),
            cancellationToken);

        return ordered.Select(movement => Map(movement, items)).ToArray();
    }

    public async Task<ApplicationResult<StockMovementResponse>> CreateMovementAsync(
        CreateStockMovementRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Kind))
        {
            return ApplicationResult<StockMovementResponse>.Validation("Movement kind is not valid.");
        }

        // A transfer is TWO linked movements. Letting one half be captured here would allow a
        // quantity to leave a warehouse without arriving anywhere - the registry would still sum
        // correctly per warehouse, and the goods would still be lost.
        if (request.Kind is StockMovementKind.TransferIn or StockMovementKind.TransferOut)
        {
            return ApplicationResult<StockMovementResponse>.Validation(
                "A transfer is recorded as its two linked halves at once: use the transfer " +
                "operation (POST /inventory/transfers) instead of capturing a single half.");
        }

        StockMovement movement;

        try
        {
            movement = BuildMovement(request);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<StockMovementResponse>.Validation(ex.Message);
        }

        var referenceFailure = await ValidateMovementReferencesAsync(
            movement.WarehouseCode,
            new[] { movement.ItemCode },
            cancellationToken);

        if (referenceFailure is not null)
        {
            return ApplicationResult<StockMovementResponse>.Validation(referenceFailure);
        }

        var isOutbound = !StockMovement.IsInbound(movement.Kind, movement.AdjustmentIsIncrease);

        if (!isOutbound)
        {
            // An entry can never make the registry sum negative, so it needs no guard and no
            // transaction of its own.
            return await PersistSingleMovementAsync(movement, context, cancellationToken);
        }

        // NEVER-NEGATIVE GUARD: the balance is re-derived and the movement is written inside one
        // Serializable transaction. See the doctrine at the top of this class.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var available = await CurrentStockAsync(movement.WarehouseCode, movement.ItemCode, cancellationToken);

            if (movement.Quantity > available)
            {
                return ApplicationResult<StockMovementResponse>.Conflict(
                    DescribeInsufficientStock(movement.WarehouseCode, movement.ItemCode, available, movement.Quantity));
            }

            var persisted = await PersistSingleMovementAsync(movement, context, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return persisted;
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<StockMovementResponse>.Conflict(ConcurrentStockMutationRefused);
        }
    }

    public async Task<ApplicationResult<StockTransferResponse>> TransferAsync(
        CreateStockTransferRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        StockMovement outMovement;
        StockMovement inMovement;

        try
        {
            (outMovement, inMovement) = StockMovement.CreateTransferPair(
                request.FromWarehouseCode,
                request.ToWarehouseCode,
                request.ItemCode,
                request.MovementDate,
                request.Quantity,
                request.Reference,
                unitCost: null,
                request.LotNumber,
                request.ExpiryDate,
                request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<StockTransferResponse>.Validation(ex.Message);
        }

        var sourceFailure = await ValidateMovementReferencesAsync(
            outMovement.WarehouseCode,
            new[] { outMovement.ItemCode },
            cancellationToken);

        if (sourceFailure is not null)
        {
            return ApplicationResult<StockTransferResponse>.Validation(sourceFailure);
        }

        var destinationFailure = await ValidateMovementReferencesAsync(
            inMovement.WarehouseCode,
            Array.Empty<string>(),
            cancellationToken);

        if (destinationFailure is not null)
        {
            return ApplicationResult<StockTransferResponse>.Validation(destinationFailure);
        }

        // Both halves are written in ONE transaction, under the same never-negative guard as any
        // other outflow: a transfer that committed only its outgoing half would destroy goods.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var available = await CurrentStockAsync(outMovement.WarehouseCode, outMovement.ItemCode, cancellationToken);

            if (outMovement.Quantity > available)
            {
                return ApplicationResult<StockTransferResponse>.Conflict(
                    DescribeInsufficientStock(
                        outMovement.WarehouseCode,
                        outMovement.ItemCode,
                        available,
                        outMovement.Quantity));
            }

            var now = DateTimeOffset.UtcNow;

            outMovement.MarkCreated(context.UserName, now);
            inMovement.MarkCreated(context.UserName, now);

            dbContext.Set<StockMovement>().Add(outMovement);
            dbContext.Set<StockMovement>().Add(inMovement);

            await WriteAuditAsync(
                "inventory.transfer.recorded",
                MovementsEntity,
                outMovement.TransferGroupId!.Value,
                context,
                new
                {
                    FromWarehouseCode = outMovement.WarehouseCode,
                    ToWarehouseCode = inMovement.WarehouseCode,
                    outMovement.ItemCode,
                    outMovement.MovementDate,
                    outMovement.Quantity,
                    outMovement.Reference,
                    TransferGroupId = outMovement.TransferGroupId
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var items = await LoadItemLabelsAsync(new[] { outMovement.ItemCode }, cancellationToken);

            return ApplicationResult<StockTransferResponse>.Success(new StockTransferResponse(
                outMovement.TransferGroupId!.Value,
                Map(outMovement, items),
                Map(inMovement, items)));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<StockTransferResponse>.Conflict(ConcurrentStockMutationRefused);
        }
    }

    // ========================== Stock and alerts ============================

    public async Task<ApplicationResult<WarehouseStockResponse>> GetWarehouseStockAsync(
        string warehouseCode,
        CancellationToken cancellationToken)
    {
        var warehouse = await LoadWarehouseAsync(warehouseCode, track: false, cancellationToken);

        if (warehouse is null)
        {
            return ApplicationResult<WarehouseStockResponse>.NotFound("Warehouse was not found.");
        }

        var facts = await LoadMovementFactsAsync(
            query => query.Where(movement => movement.WarehouseCode == warehouse.Code),
            cancellationToken);

        var quantities = facts
            .GroupBy(fact => fact.ItemCode)
            .ToDictionary(group => group.Key, SignedTotal);

        var itemCodes = quantities.Keys.ToArray();

        var items = await dbContext.Set<StockItem>()
            .AsNoTracking()
            .Where(item => itemCodes.Contains(item.Code))
            .ToArrayAsync(cancellationToken);

        var averageCosts = await LoadAverageCostsAsync(itemCodes, cancellationToken);

        var rows = new List<WarehouseStockRow>(items.Length);

        foreach (var item in items.OrderBy(current => current.Code, StringComparer.Ordinal))
        {
            var quantity = quantities[item.Code];
            var averageUnitCost = averageCosts.TryGetValue(item.Code, out var cost) ? cost : 0m;

            rows.Add(new WarehouseStockRow(
                item.Code,
                item.Designation,
                item.UnitOfMeasure,
                item.Category,
                quantity,
                averageUnitCost,
                // Value is the PUBLISHED average times the quantity, so the screen's three
                // columns stay arithmetically consistent with each other: a reader can multiply
                // the two figures shown and land on the third.
                decimal.Round(quantity * averageUnitCost, 2, MidpointRounding.AwayFromZero),
                item.MinimumQuantity,
                IsBelowMinimum(quantity, item.MinimumQuantity)));
        }

        return ApplicationResult<WarehouseStockResponse>.Success(new WarehouseStockResponse(
            warehouse.Code,
            warehouse.Label,
            rows,
            rows.Sum(row => row.StockValue)));
    }

    public async Task<IReadOnlyCollection<LowStockRow>> GetLowStockAsync(CancellationToken cancellationToken)
    {
        // Only ACTIVE items carrying a threshold can alert (a zero threshold means "no alert",
        // see StockItem), and only in ACTIVE warehouses: an alert nobody can act on is noise.
        var alertingItems = await dbContext.Set<StockItem>()
            .AsNoTracking()
            .Where(item => item.IsActive && item.MinimumQuantity > 0m)
            .ToArrayAsync(cancellationToken);

        if (alertingItems.Length == 0)
        {
            return Array.Empty<LowStockRow>();
        }

        var warehouses = await dbContext.Set<Warehouse>()
            .AsNoTracking()
            .Where(warehouse => warehouse.IsActive)
            .ToArrayAsync(cancellationToken);

        if (warehouses.Length == 0)
        {
            return Array.Empty<LowStockRow>();
        }

        var itemCodes = alertingItems.Select(item => item.Code).ToArray();
        var warehouseCodes = warehouses.Select(warehouse => warehouse.Code).ToArray();

        var facts = await LoadMovementFactsAsync(
            query => query.Where(movement =>
                itemCodes.Contains(movement.ItemCode) &&
                warehouseCodes.Contains(movement.WarehouseCode)),
            cancellationToken);

        var itemsByCode = alertingItems.ToDictionary(item => item.Code);
        var warehousesByCode = warehouses.ToDictionary(warehouse => warehouse.Code);

        var rows = facts
            .GroupBy(fact => new { fact.WarehouseCode, fact.ItemCode })
            .Select(group => new
            {
                group.Key.WarehouseCode,
                group.Key.ItemCode,
                Quantity = SignedTotal(group)
            })
            .Where(row => IsBelowMinimum(row.Quantity, itemsByCode[row.ItemCode].MinimumQuantity))
            .OrderBy(row => row.WarehouseCode, StringComparer.Ordinal)
            .ThenBy(row => row.ItemCode, StringComparer.Ordinal)
            .Select(row => new LowStockRow(
                row.WarehouseCode,
                warehousesByCode[row.WarehouseCode].Label,
                row.ItemCode,
                itemsByCode[row.ItemCode].Designation,
                itemsByCode[row.ItemCode].UnitOfMeasure,
                row.Quantity,
                itemsByCode[row.ItemCode].MinimumQuantity))
            .ToArray();

        return rows;
    }

    // =========================== Inventory counts ===========================

    public async Task<IReadOnlyCollection<InventoryCountResponse>> ListCountsAsync(
        string? warehouseCode,
        InventoryCountStatus? status,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<InventoryCount>()
            .AsNoTracking()
            .Include(count => count.Lines)
            .AsQueryable();

        var normalizedWarehouse = NormalizeNullableCode(warehouseCode);

        if (normalizedWarehouse is not null)
        {
            query = query.Where(count => count.WarehouseCode == normalizedWarehouse);
        }

        if (status.HasValue)
        {
            query = query.Where(count => count.Status == status.Value);
        }

        var counts = await query.ToArrayAsync(cancellationToken);

        // Ordered in memory: CreatedAt is a DateTimeOffset, which the SQLite test provider does
        // not sort.
        var ordered = counts
            .OrderByDescending(count => count.CountDate)
            .ThenByDescending(count => count.CreatedAt)
            .ToArray();

        var items = await LoadItemLabelsAsync(
            ordered.SelectMany(count => count.Lines).Select(line => line.ItemCode).Distinct().ToArray(),
            cancellationToken);

        return ordered.Select(count => Map(count, items)).ToArray();
    }

    public async Task<ApplicationResult<InventoryCountResponse>> GetCountAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var count = await dbContext.Set<InventoryCount>()
            .AsNoTracking()
            .Include(current => current.Lines)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (count is null)
        {
            return ApplicationResult<InventoryCountResponse>.NotFound("Inventory count was not found.");
        }

        var items = await LoadItemLabelsAsync(
            count.Lines.Select(line => line.ItemCode).Distinct().ToArray(),
            cancellationToken);

        return ApplicationResult<InventoryCountResponse>.Success(Map(count, items));
    }

    public async Task<ApplicationResult<InventoryCountResponse>> CreateCountAsync(
        CreateInventoryCountRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        InventoryCount count;

        try
        {
            count = new InventoryCount(request.WarehouseCode, request.CountDate);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<InventoryCountResponse>.Validation(ex.Message);
        }

        var warehouse = await LoadWarehouseAsync(count.WarehouseCode, track: false, cancellationToken);

        if (warehouse is null)
        {
            return ApplicationResult<InventoryCountResponse>.Validation(
                $"Warehouse '{count.WarehouseCode}' does not exist.");
        }

        if (!warehouse.IsActive)
        {
            return ApplicationResult<InventoryCountResponse>.Validation(
                $"Warehouse '{warehouse.Code}' is deactivated and can no longer be counted.");
        }

        // One count in progress per warehouse. Two open counts of the same shelves would each
        // claim to be the physical truth, and validating both would apply two contradictory sets
        // of adjustments to the same registry.
        var openCount = await dbContext.Set<InventoryCount>()
            .AnyAsync(
                current => current.WarehouseCode == count.WarehouseCode
                    && current.Status == InventoryCountStatus.Draft,
                cancellationToken);

        if (openCount)
        {
            return ApplicationResult<InventoryCountResponse>.Conflict(
                $"An inventory count is already in progress for warehouse '{count.WarehouseCode}'. " +
                "Validate it, or delete its lines, before opening another one.");
        }

        count.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<InventoryCount>().Add(count);

        await WriteAuditAsync(
            "inventory.count.created",
            CountsEntity,
            count.Id,
            context,
            new { count.WarehouseCode, count.CountDate },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<InventoryCountResponse>.Success(
            Map(count, new Dictionary<string, StockItem>(StringComparer.Ordinal)));
    }

    public async Task<ApplicationResult<InventoryCountResponse>> ReplaceCountLinesAsync(
        Guid id,
        ReplaceInventoryCountLinesRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (request.Lines is null)
        {
            return ApplicationResult<InventoryCountResponse>.Validation("The counted lines are required.");
        }

        // Serializable transaction + conditional claim, same shape as AccountingService: checking
        // "is it still a draft?" in memory and saving afterwards leaves the window in which a
        // concurrent validation freezes the count between the check and the write.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var count = await dbContext.Set<InventoryCount>()
                .Include(current => current.Lines)
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (count is null)
            {
                return ApplicationResult<InventoryCountResponse>.NotFound("Inventory count was not found.");
            }

            if (count.Status != InventoryCountStatus.Draft)
            {
                return ApplicationResult<InventoryCountResponse>.Conflict(
                    "A validated inventory count is immutable: it is the proof behind the " +
                    "adjustments it generated. Open a new count instead.");
            }

            var now = DateTimeOffset.UtcNow;

            if (!await TryClaimDraftCountAsync(count.Id, now, cancellationToken))
            {
                return ApplicationResult<InventoryCountResponse>.Conflict(ConcurrentCountMutationRefused);
            }

            List<InventoryCountLine> lines;

            try
            {
                lines = request.Lines
                    .Select(line => new InventoryCountLine(line.ItemCode, line.CountedQuantity))
                    .ToList();
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                return ApplicationResult<InventoryCountResponse>.Validation(ex.Message);
            }

            // The referential is checked BEFORE the aggregate is mutated: a refusal must leave
            // the tracked count exactly as it was, not rely on the rollback to undo an in-memory
            // mutation the change tracker would still be carrying afterwards.
            var unknownItem = await FindUnknownOrInactiveItemAsync(
                lines.Select(line => line.ItemCode).Distinct().ToArray(),
                cancellationToken);

            if (unknownItem is not null)
            {
                return ApplicationResult<InventoryCountResponse>.Validation(unknownItem);
            }

            try
            {
                count.ReplaceLines(lines);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
            {
                return ApplicationResult<InventoryCountResponse>.Validation(ex.Message);
            }

            count.MarkUpdated(context.UserName, now);

            await WriteAuditAsync(
                "inventory.count.lines_updated",
                CountsEntity,
                count.Id,
                context,
                new { count.WarehouseCode, count.CountDate, LineCount = count.Lines.Count },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var items = await LoadItemLabelsAsync(
                count.Lines.Select(line => line.ItemCode).Distinct().ToArray(),
                cancellationToken);

            return ApplicationResult<InventoryCountResponse>.Success(Map(count, items));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<InventoryCountResponse>.Conflict(ConcurrentCountMutationRefused);
        }
    }

    public async Task<ApplicationResult<InventoryCountValidationResponse>> ValidateCountAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        // Validation READS the theoretical stock and WRITES the adjustments that close the gap:
        // both must sit in one Serializable transaction, or a consumption slipping between the
        // read and the write would be silently erased by an adjustment computed before it. The
        // conditional claim on top makes a double-click generate one set of adjustments, never two.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var count = await dbContext.Set<InventoryCount>()
                .Include(current => current.Lines)
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (count is null)
            {
                return ApplicationResult<InventoryCountValidationResponse>.NotFound("Inventory count was not found.");
            }

            if (count.Status != InventoryCountStatus.Draft)
            {
                return ApplicationResult<InventoryCountValidationResponse>.Conflict(
                    "This inventory count has already been validated.");
            }

            if (count.Lines.Count == 0)
            {
                return ApplicationResult<InventoryCountValidationResponse>.Validation(
                    "An inventory count requires at least one counted line to be validated.");
            }

            var now = DateTimeOffset.UtcNow;

            if (!await TryClaimDraftCountAsync(count.Id, now, cancellationToken))
            {
                return ApplicationResult<InventoryCountValidationResponse>.Conflict(ConcurrentCountMutationRefused);
            }

            var countedCodes = count.Lines.Select(line => line.ItemCode).ToArray();

            var facts = await LoadMovementFactsAsync(
                query => query.Where(movement =>
                    movement.WarehouseCode == count.WarehouseCode &&
                    countedCodes.Contains(movement.ItemCode)),
                cancellationToken);

            var theoretical = facts
                .GroupBy(fact => fact.ItemCode)
                .ToDictionary(group => group.Key, SignedTotal);

            var reference = BuildAdjustmentReference(count);
            var adjustments = new List<StockMovement>();

            foreach (var line in count.Lines.OrderBy(current => current.LineNumber))
            {
                var known = theoretical.TryGetValue(line.ItemCode, out var value) ? value : 0m;
                var difference = line.CountedQuantity - known;

                // A line that matches the theoretical stock generates nothing: the registry
                // already says what the shelf says, and a zero-quantity movement is not even
                // representable (StockMovement requires a strictly positive quantity).
                if (difference == 0m)
                {
                    continue;
                }

                var adjustment = StockMovement.InventoryAdjustment(
                    count.WarehouseCode,
                    line.ItemCode,
                    count.CountDate,
                    Math.Abs(difference),
                    isIncrease: difference > 0m,
                    reference,
                    notes: $"Physical count {line.CountedQuantity} against theoretical {known}.");

                adjustment.MarkCreated(context.UserName, now);
                adjustments.Add(adjustment);
            }

            // The count is frozen BEFORE the adjustments are attached: if the freeze were
            // refused, no adjustment must have been staged for insertion.
            try
            {
                count.Validate(context.UserName, now);
            }
            catch (InvalidOperationException ex)
            {
                return ApplicationResult<InventoryCountValidationResponse>.Validation(ex.Message);
            }

            if (adjustments.Count > 0)
            {
                dbContext.Set<StockMovement>().AddRange(adjustments);
            }

            count.MarkUpdated(context.UserName, now);

            await WriteAuditAsync(
                "inventory.count.validated",
                CountsEntity,
                count.Id,
                context,
                new
                {
                    count.WarehouseCode,
                    count.CountDate,
                    LineCount = count.Lines.Count,
                    AdjustmentCount = adjustments.Count,
                    Reference = reference
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var items = await LoadItemLabelsAsync(countedCodes.Distinct().ToArray(), cancellationToken);

            return ApplicationResult<InventoryCountValidationResponse>.Success(
                new InventoryCountValidationResponse(Map(count, items), adjustments.Count));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<InventoryCountValidationResponse>.Conflict(ConcurrentCountMutationRefused);
        }
    }

    // ===================== Contracts published to the wave ==================

    /// <summary>
    /// Entry movements generated by a purchase receipt, one per received line, at the unit cost
    /// of the reception - which is what feeds the weighted average cost of the item.
    ///
    /// Deliberately opens NO transaction of its own: entries only ever ADD to stock, so there is
    /// no never-negative guard to hold, and the caller (PurchasingService.ReceiveAsync) already
    /// owns a Serializable transaction on this very DbContext - starting a second one there would
    /// throw rather than protect anything. The changes are flushed into the caller's transaction
    /// and are rolled back with it if the reception is refused afterwards.
    /// </summary>
    public async Task<ApplicationResult<StockEntryResult>> RegisterPurchaseReceiptAsync(
        RegisterPurchaseReceiptRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (request.Lines is null || request.Lines.Count == 0)
        {
            return ApplicationResult<StockEntryResult>.Validation("A stock receipt requires at least one line.");
        }

        var movements = new List<StockMovement>(request.Lines.Count);

        try
        {
            foreach (var line in request.Lines)
            {
                // The movement date is the date the goods were RECEIVED, which is today: the
                // contract carries no date because a reception is registered as it happens, and
                // back-dating stock would let a consumption sit before the entry that supplied it.
                movements.Add(StockMovement.PurchaseEntry(
                    request.WarehouseCode,
                    line.ItemCode,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    line.Quantity,
                    line.UnitCost,
                    request.Reference,
                    line.LotNumber,
                    line.ExpiryDate));
            }
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<StockEntryResult>.Validation(ex.Message);
        }

        var warehouseCode = movements[0].WarehouseCode;

        var warehouse = await LoadWarehouseAsync(warehouseCode, track: false, cancellationToken);

        if (warehouse is null)
        {
            return ApplicationResult<StockEntryResult>.NotFound($"Warehouse '{warehouseCode}' does not exist.");
        }

        if (!warehouse.IsActive)
        {
            return ApplicationResult<StockEntryResult>.Validation(
                $"Warehouse '{warehouse.Code}' is deactivated and can no longer receive goods.");
        }

        // Existence only, not activity: the order was placed while the item was live, and refusing
        // the physical goods already on the dock because someone deactivated the item in between
        // would leave the delivery untraceable.
        var itemCodes = movements.Select(movement => movement.ItemCode).Distinct().ToArray();

        var knownCodes = await dbContext.Set<StockItem>()
            .AsNoTracking()
            .Where(item => itemCodes.Contains(item.Code))
            .Select(item => item.Code)
            .ToArrayAsync(cancellationToken);

        var unknown = itemCodes.Except(knownCodes, StringComparer.Ordinal).FirstOrDefault();

        if (unknown is not null)
        {
            return ApplicationResult<StockEntryResult>.NotFound($"Stock item '{unknown}' does not exist.");
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var movement in movements)
        {
            movement.MarkCreated(context.UserName, now);
        }

        dbContext.Set<StockMovement>().AddRange(movements);

        await WriteAuditAsync(
            "inventory.receipt.registered",
            MovementsEntity,
            movements[0].Id,
            context,
            new
            {
                WarehouseCode = warehouseCode,
                request.Reference,
                MovementCount = movements.Count,
                TotalQuantity = movements.Sum(movement => movement.Quantity)
            },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<StockEntryResult>.Success(new StockEntryResult(movements.Count));
    }

    /// <summary>
    /// Weighted average cost (PMP) of an item, across ALL its purchase entries, every warehouse
    /// combined: the cost of a kilo of flour does not depend on which storeroom it sits in.
    /// An item that exists but never entered stock answers Success with a zero cost - the caller
    /// (a recipe costing, a purchase order check) needs to tell "unknown item" from "not yet
    /// received", and only the first is a NotFound.
    /// </summary>
    public async Task<ApplicationResult<ItemCost>> GetAverageCostAsync(
        string itemCode,
        CancellationToken cancellationToken)
    {
        var item = await LoadItemAsync(itemCode, track: false, cancellationToken);

        if (item is null)
        {
            return ApplicationResult<ItemCost>.NotFound($"Stock item '{NormalizeCodeOrEmpty(itemCode)}' was not found.");
        }

        var averageCosts = await LoadAverageCostsAsync(new[] { item.Code }, cancellationToken);

        var average = averageCosts.TryGetValue(item.Code, out var cost) ? cost : 0m;

        return ApplicationResult<ItemCost>.Success(new ItemCost(item.Code, average, item.UnitOfMeasure));
    }

    // ================================ Helpers ===============================

    /// <summary>
    /// The narrow projection of the registry every stock figure is derived from. It carries the
    /// two fields the direction rule needs (<see cref="StockMovementKind"/> and, for an
    /// adjustment, its direction) and nothing else, so summing it stays cheap.
    /// </summary>
    private sealed record MovementFact(
        string WarehouseCode,
        string ItemCode,
        StockMovementKind Kind,
        decimal Quantity,
        bool? AdjustmentIsIncrease);

    private async Task<MovementFact[]> LoadMovementFactsAsync(
        Func<IQueryable<StockMovement>, IQueryable<StockMovement>> filter,
        CancellationToken cancellationToken)
    {
        return await filter(dbContext.Set<StockMovement>().AsNoTracking())
            .Select(movement => new MovementFact(
                movement.WarehouseCode,
                movement.ItemCode,
                movement.Kind,
                movement.Quantity,
                movement.AdjustmentIsIncrease))
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Current stock of one (warehouse, item) pair: the sum of its registry, with the sign of
    /// each movement taken from <see cref="StockMovement.IsInbound"/> - the domain's rule, never
    /// a copy of it. Called inside the never-negative guard's transaction, so the read is part of
    /// the same serializable unit as the write it protects.
    /// </summary>
    private async Task<decimal> CurrentStockAsync(
        string warehouseCode,
        string itemCode,
        CancellationToken cancellationToken)
    {
        var facts = await LoadMovementFactsAsync(
            query => query.Where(movement =>
                movement.WarehouseCode == warehouseCode &&
                movement.ItemCode == itemCode),
            cancellationToken);

        return SignedTotal(facts);
    }

    private static decimal SignedTotal(IEnumerable<MovementFact> facts)
    {
        return facts.Sum(fact => StockMovement.IsInbound(fact.Kind, fact.AdjustmentIsIncrease)
            ? fact.Quantity
            : -fact.Quantity);
    }

    /// <summary>
    /// Weighted average cost per item code: sum(quantity x unit cost) / sum(quantity) over the
    /// purchase entries, rounded to the 2 decimals of a money column. Items with no purchase
    /// entry are absent from the dictionary (their cost is not zero, it is unknown - the callers
    /// decide what to do with that).
    /// </summary>
    private async Task<Dictionary<string, decimal>> LoadAverageCostsAsync(
        IReadOnlyCollection<string> itemCodes,
        CancellationToken cancellationToken)
    {
        if (itemCodes.Count == 0)
        {
            return new Dictionary<string, decimal>(StringComparer.Ordinal);
        }

        var codes = itemCodes.ToArray();

        // Materialized before the arithmetic: decimals are stored as TEXT by the SQLite test
        // provider, where a SQL SUM over them does not mean what it says.
        var entries = await dbContext.Set<StockMovement>()
            .AsNoTracking()
            .Where(movement =>
                movement.Kind == StockMovementKind.PurchaseEntry &&
                movement.UnitCost != null &&
                codes.Contains(movement.ItemCode))
            .Select(movement => new
            {
                movement.ItemCode,
                movement.Quantity,
                UnitCost = movement.UnitCost!.Value
            })
            .ToArrayAsync(cancellationToken);

        var averages = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var group in entries.GroupBy(entry => entry.ItemCode))
        {
            var quantity = group.Sum(entry => entry.Quantity);

            if (quantity <= 0m)
            {
                continue;
            }

            var value = group.Sum(entry => entry.Quantity * entry.UnitCost);

            averages[group.Key] = decimal.Round(value / quantity, 2, MidpointRounding.AwayFromZero);
        }

        return averages;
    }

    /// <summary>
    /// The alert rule, stated once: an item alerts when its stock sits STRICTLY below its
    /// threshold, and a zero threshold never alerts (0 means "no alert", see StockItem).
    /// </summary>
    private static bool IsBelowMinimum(decimal quantity, decimal minimumQuantity)
    {
        return minimumQuantity > 0m && quantity < minimumQuantity;
    }

    private static StockMovement BuildMovement(CreateStockMovementRequest request)
    {
        return request.Kind switch
        {
            StockMovementKind.PurchaseEntry => StockMovement.PurchaseEntry(
                request.WarehouseCode,
                request.ItemCode,
                request.MovementDate,
                request.Quantity,
                request.UnitCost ?? throw new ArgumentException(
                    "A unit cost is required for a purchase entry: it feeds the weighted average cost.",
                    nameof(request)),
                request.Reference,
                request.LotNumber,
                request.ExpiryDate,
                request.Notes),

            StockMovementKind.Consumption => StockMovement.Consumption(
                request.WarehouseCode,
                request.ItemCode,
                request.MovementDate,
                request.Quantity,
                request.Reference,
                request.UnitCost,
                request.Notes),

            StockMovementKind.InventoryAdjustment => StockMovement.InventoryAdjustment(
                request.WarehouseCode,
                request.ItemCode,
                request.MovementDate,
                request.Quantity,
                request.AdjustmentIsIncrease ?? throw new ArgumentException(
                    "An inventory adjustment requires an explicit direction.",
                    nameof(request)),
                request.Reference,
                request.Notes),

            _ => throw new ArgumentException("Movement kind is not valid.", nameof(request))
        };
    }

    private async Task<ApplicationResult<StockMovementResponse>> PersistSingleMovementAsync(
        StockMovement movement,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        movement.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<StockMovement>().Add(movement);

        await WriteAuditAsync(
            "inventory.movement.recorded",
            MovementsEntity,
            movement.Id,
            context,
            new
            {
                movement.WarehouseCode,
                movement.ItemCode,
                movement.MovementDate,
                Kind = movement.Kind.ToString(),
                movement.Quantity,
                movement.SignedQuantity,
                movement.UnitCost,
                movement.Reference
            },
            cancellationToken);

        await SaveAsync(cancellationToken);

        var items = await LoadItemLabelsAsync(new[] { movement.ItemCode }, cancellationToken);

        return ApplicationResult<StockMovementResponse>.Success(Map(movement, items));
    }

    private static string DescribeInsufficientStock(
        string warehouseCode,
        string itemCode,
        decimal available,
        decimal requested)
    {
        return $"Stock of item '{itemCode}' in warehouse '{warehouseCode}' is {available}: " +
            $"taking out {requested} would make it negative. Stock can never go below zero.";
    }

    /// <summary>
    /// Warehouse and items a movement points at must exist and be active at capture time.
    /// Returns the message naming the first offender, or null when everything resolves.
    /// </summary>
    private async Task<string?> ValidateMovementReferencesAsync(
        string warehouseCode,
        IReadOnlyCollection<string> itemCodes,
        CancellationToken cancellationToken)
    {
        var warehouse = await LoadWarehouseAsync(warehouseCode, track: false, cancellationToken);

        if (warehouse is null)
        {
            return $"Warehouse '{warehouseCode}' does not exist.";
        }

        if (!warehouse.IsActive)
        {
            return $"Warehouse '{warehouse.Code}' is deactivated and can no longer receive movements.";
        }

        return itemCodes.Count == 0
            ? null
            : await FindUnknownOrInactiveItemAsync(itemCodes, cancellationToken);
    }

    private async Task<string?> FindUnknownOrInactiveItemAsync(
        IReadOnlyCollection<string> itemCodes,
        CancellationToken cancellationToken)
    {
        if (itemCodes.Count == 0)
        {
            return null;
        }

        var codes = itemCodes.ToArray();

        var items = await dbContext.Set<StockItem>()
            .AsNoTracking()
            .Where(item => codes.Contains(item.Code))
            .Select(item => new { item.Code, item.IsActive })
            .ToArrayAsync(cancellationToken);

        var known = items.ToDictionary(item => item.Code, item => item.IsActive, StringComparer.Ordinal);

        foreach (var code in codes)
        {
            if (!known.TryGetValue(code, out var isActive))
            {
                return $"Stock item '{code}' does not exist.";
            }

            if (!isActive)
            {
                return $"Stock item '{code}' is deactivated and can no longer move.";
            }
        }

        return null;
    }

    /// <summary>
    /// Atomic form of "this count is still a draft": the invariant travels as the WHERE clause of
    /// one conditional UPDATE, evaluated by the database at the instant the row is claimed rather
    /// than answered by the earlier SELECT a concurrent validation can invalidate. The single
    /// column it writes is the one the caller's mutation stamps anyway, with the same timestamp.
    /// </summary>
    private async Task<bool> TryClaimDraftCountAsync(
        Guid countId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var claimedRows = await dbContext.Set<InventoryCount>()
            .Where(current => current.Id == countId && current.Status == InventoryCountStatus.Draft)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(current => current.UpdatedAt, now),
                cancellationToken);

        return claimedRows == 1;
    }

    /// <summary>
    /// Reference stamped on every adjustment a validation generates: stable, short enough for the
    /// 80-character column, and traceable back to the count that produced it - the audit trail
    /// that makes "why did stock move on that day?" answerable from the registry alone.
    /// </summary>
    private static string BuildAdjustmentReference(InventoryCount count)
    {
        return $"INV-{count.CountDate:yyyyMMdd}-{count.Id.ToString("N")[..8].ToUpperInvariant()}";
    }

    private async Task<Warehouse?> LoadWarehouseAsync(string code, bool track, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var query = dbContext.Set<Warehouse>().AsQueryable();

        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);
    }

    private async Task<StockItem?> LoadItemAsync(string code, bool track, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var query = dbContext.Set<StockItem>().AsQueryable();

        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);
    }

    private async Task<Dictionary<string, StockItem>> LoadItemLabelsAsync(
        IReadOnlyCollection<string> itemCodes,
        CancellationToken cancellationToken)
    {
        if (itemCodes.Count == 0)
        {
            return new Dictionary<string, StockItem>(StringComparer.Ordinal);
        }

        var codes = itemCodes.ToArray();

        var items = await dbContext.Set<StockItem>()
            .AsNoTracking()
            .Where(item => codes.Contains(item.Code))
            .ToArrayAsync(cancellationToken);

        return items.ToDictionary(item => item.Code, StringComparer.Ordinal);
    }

    /// <summary>
    /// Lookup normalization for a code coming from a route or a query string. Deliberately does
    /// not go through the entities' strict NormalizeCode: a malformed code must produce a clean
    /// 404 (nothing matches) rather than an exception, while a code being CREATED does go through
    /// the strict normalization, in the entity's constructor.
    /// </summary>
    private static string NormalizeCodeOrEmpty(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
    }

    private static string? NormalizeNullableCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
    }

    private static WarehouseResponse Map(Warehouse warehouse)
    {
        return new WarehouseResponse(
            warehouse.Id,
            warehouse.Code,
            warehouse.Label,
            warehouse.HotelUnitCode,
            warehouse.IsActive,
            warehouse.CreatedAt,
            warehouse.CreatedBy,
            warehouse.UpdatedAt,
            warehouse.UpdatedBy);
    }

    private static StockItemResponse Map(StockItem item)
    {
        return new StockItemResponse(
            item.Id,
            item.Code,
            item.Designation,
            item.UnitOfMeasure,
            item.Category,
            item.MinimumQuantity,
            item.IsActive,
            item.CreatedAt,
            item.CreatedBy,
            item.UpdatedAt,
            item.UpdatedBy);
    }

    private static StockMovementResponse Map(StockMovement movement, IReadOnlyDictionary<string, StockItem> items)
    {
        items.TryGetValue(movement.ItemCode, out var item);

        return new StockMovementResponse(
            movement.Id,
            movement.WarehouseCode,
            movement.ItemCode,
            item?.Designation,
            item?.UnitOfMeasure,
            movement.MovementDate,
            movement.Kind,
            movement.Quantity,
            movement.SignedQuantity,
            movement.UnitCost,
            movement.Reference,
            movement.LotNumber,
            movement.ExpiryDate,
            movement.Notes,
            movement.AdjustmentIsIncrease,
            movement.TransferGroupId,
            movement.CreatedAt,
            movement.CreatedBy);
    }

    private static InventoryCountResponse Map(InventoryCount count, IReadOnlyDictionary<string, StockItem> items)
    {
        var lines = count.Lines
            .OrderBy(line => line.LineNumber)
            .Select(line =>
            {
                items.TryGetValue(line.ItemCode, out var item);

                return new InventoryCountLineResponse(
                    line.Id,
                    line.LineNumber,
                    line.ItemCode,
                    item?.Designation,
                    item?.UnitOfMeasure,
                    line.CountedQuantity);
            })
            .ToArray();

        return new InventoryCountResponse(
            count.Id,
            count.WarehouseCode,
            count.CountDate,
            count.Status,
            lines,
            count.CanEdit,
            count.ValidatedAt,
            count.ValidatedBy,
            count.CreatedAt,
            count.CreatedBy,
            count.UpdatedAt,
            count.UpdatedBy);
    }

    /// <summary>
    /// Explicit flush after the audit write. AuditLogWriter.WriteAsync already calls
    /// SaveChangesAsync internally, so this call is usually a no-op - it exists so persistence
    /// never silently depends on the audit writer's internals.
    /// </summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task WriteAuditAsync(
        string action,
        string entityName,
        Guid entityId,
        OperationContext context,
        object details,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                entityName,
                entityId.ToString(),
                context.IpAddress,
                JsonSerializer.Serialize(details)),
            cancellationToken);
    }
}
