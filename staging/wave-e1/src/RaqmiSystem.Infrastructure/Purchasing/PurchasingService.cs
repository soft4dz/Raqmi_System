using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Inventory;
using RaqmiSystem.Application.Purchasing;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Purchasing;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Infrastructure.Purchasing;

/// <summary>
/// Purchasing module: suppliers, purchase orders and receptions. The stock side is consumed
/// EXCLUSIVELY through the contracts published by the stock module
/// (<see cref="IStockOperationService"/> for the entry movements a reception generates,
/// <see cref="IStockCostProvider"/> to assert an ordered item exists in the stock
/// referential): this service never touches the stock module's own entities.
///
/// Concurrency doctrine, borrowed from the existing services:
/// - approval allocates the "BC-{year}-{seq:D6}" number with the invoice mechanic
///   (BillingService.IssueInvoiceAsync): SELECT max+1 protected by the unique index
///   ux_purchase_orders_approved_year_sequence, one retry on collision, and a DB status
///   re-check that turns a double-approve race into a clean 409;
/// - every other sensitive transition (line rewrite, reception, cancellation) runs inside a
///   Serializable transaction with the atomic claim-in-one-statement pattern of
///   AccountingService.TryClaimDraftEntryAsync, so the status checked in memory is re-asserted
///   by the database at the instant the row is claimed.
/// </summary>
public sealed class PurchasingService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    IStockOperationService stockOperationService,
    IStockCostProvider stockCostProvider) : IPurchasingService
{
    private const string SuppliersEntity = "purchasing.suppliers";

    private const string OrdersEntity = "purchasing.purchase_orders";

    /// <summary>
    /// Answer given when the atomic claim finds that the order loaded a moment ago has been
    /// moved to another status by a concurrent request. Nothing was modified.
    /// </summary>
    private const string ConcurrentOrderMutationRefused =
        "This purchase order was just modified by a concurrent operation, so this change was not applied. " +
        "Reload the order and retry if still relevant.";

    public async Task<IReadOnlyCollection<SupplierResponse>> ListSuppliersAsync(
        string? search,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Supplier>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(supplier => supplier.IsActive);
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim().ToUpperInvariant();

        if (normalizedSearch is not null)
        {
            query = query.Where(supplier =>
                supplier.Code.Contains(normalizedSearch) ||
                supplier.Name.ToUpper().Contains(normalizedSearch));
        }

        var suppliers = await query
            .OrderBy(supplier => supplier.Code)
            .ToArrayAsync(cancellationToken);

        return suppliers.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<SupplierResponse>> GetSupplierAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var supplier = await dbContext.Set<Supplier>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (supplier is null)
        {
            return ApplicationResult<SupplierResponse>.NotFound("Supplier was not found.");
        }

        return ApplicationResult<SupplierResponse>.Success(Map(supplier));
    }

    public async Task<ApplicationResult<SupplierResponse>> CreateSupplierAsync(
        CreateSupplierRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(request.Code);

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return ApplicationResult<SupplierResponse>.Validation("Supplier code is required.");
        }

        var exists = await dbContext.Set<Supplier>()
            .AnyAsync(current => current.Code == normalizedCode, cancellationToken);

        if (exists)
        {
            return ApplicationResult<SupplierResponse>.Conflict("A supplier with this code already exists.");
        }

        Supplier supplier;

        try
        {
            supplier = new Supplier(
                normalizedCode,
                request.Name,
                request.SupplierType,
                request.Nif,
                request.Rc,
                request.Ai,
                request.Nis,
                request.Address,
                request.City,
                request.Phone,
                request.Email);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<SupplierResponse>.Validation(ex.Message);
        }

        supplier.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<Supplier>().Add(supplier);

        try
        {
            await WriteAuditAsync(
                "purchasing.supplier.created",
                SuppliersEntity,
                supplier.Id,
                context,
                new { supplier.Code, supplier.Name, SupplierType = supplier.SupplierType.ToString() },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // The exists-check above and this insert are not atomic: a concurrent create with
            // the same code loses the race against the unique constraint on suppliers.code.
            return ApplicationResult<SupplierResponse>.Conflict("A supplier with this code already exists.");
        }

        return ApplicationResult<SupplierResponse>.Success(Map(supplier));
    }

    public async Task<ApplicationResult<SupplierResponse>> UpdateSupplierAsync(
        string code,
        UpdateSupplierRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var supplier = await dbContext.Set<Supplier>()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (supplier is null)
        {
            return ApplicationResult<SupplierResponse>.NotFound("Supplier was not found.");
        }

        try
        {
            supplier.UpdateDetails(
                request.Name,
                request.SupplierType,
                request.Nif,
                request.Rc,
                request.Ai,
                request.Nis,
                request.Address,
                request.City,
                request.Phone,
                request.Email);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<SupplierResponse>.Validation(ex.Message);
        }

        supplier.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "purchasing.supplier.updated",
            SuppliersEntity,
            supplier.Id,
            context,
            new { supplier.Code, supplier.Name, SupplierType = supplier.SupplierType.ToString() },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<SupplierResponse>.Success(Map(supplier));
    }

    public async Task<ApplicationResult<SupplierResponse>> SetSupplierActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var supplier = await dbContext.Set<Supplier>()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (supplier is null)
        {
            return ApplicationResult<SupplierResponse>.NotFound("Supplier was not found.");
        }

        if (isActive)
        {
            supplier.Activate();
        }
        else
        {
            supplier.Deactivate();
        }

        supplier.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "purchasing.supplier.activated" : "purchasing.supplier.deactivated",
            SuppliersEntity,
            supplier.Id,
            context,
            new { supplier.Code, supplier.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<SupplierResponse>.Success(Map(supplier));
    }

    public async Task<IReadOnlyCollection<PurchaseOrderResponse>> ListOrdersAsync(
        DateOnly? from,
        DateOnly? to,
        string? supplierCode,
        string? warehouseCode,
        PurchaseOrderStatus? status,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<PurchaseOrder>()
            .AsNoTracking()
            .Include(order => order.Lines)
            .AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(order => order.OrderDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(order => order.OrderDate <= to.Value);
        }

        var normalizedSupplierCode = NormalizeNullableCode(supplierCode);

        if (normalizedSupplierCode is not null)
        {
            query = query.Where(order => order.SupplierCode == normalizedSupplierCode);
        }

        var normalizedWarehouseCode = NormalizeNullableCode(warehouseCode);

        if (normalizedWarehouseCode is not null)
        {
            query = query.Where(order => order.WarehouseCode == normalizedWarehouseCode);
        }

        if (status.HasValue)
        {
            query = query.Where(order => order.Status == status.Value);
        }

        var orders = await query
            .OrderByDescending(order => order.OrderDate)
            .ThenBy(order => order.SupplierCode)
            .ToArrayAsync(cancellationToken);

        var supplierNames = await LoadSupplierNamesAsync(
            orders.Select(order => order.SupplierCode).Distinct().ToArray(),
            cancellationToken);

        return orders
            .Select(order => Map(order, supplierNames.GetValueOrDefault(order.SupplierCode)))
            .ToArray();
    }

    public async Task<ApplicationResult<PurchaseOrderResponse>> GetOrderAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.Set<PurchaseOrder>()
            .AsNoTracking()
            .Include(current => current.Lines)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (order is null)
        {
            return ApplicationResult<PurchaseOrderResponse>.NotFound("Purchase order was not found.");
        }

        return ApplicationResult<PurchaseOrderResponse>.Success(
            Map(order, await LoadSupplierNameAsync(order.SupplierCode, cancellationToken)));
    }

    public async Task<ApplicationResult<PurchaseOrderResponse>> CreateOrderAsync(
        CreatePurchaseOrderRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (request.Lines is null || request.Lines.Count == 0)
        {
            return ApplicationResult<PurchaseOrderResponse>.Validation("A purchase order must contain at least one line.");
        }

        var normalizedSupplierCode = NormalizeCodeOrEmpty(request.SupplierCode);

        if (string.IsNullOrWhiteSpace(normalizedSupplierCode))
        {
            return ApplicationResult<PurchaseOrderResponse>.Validation("Supplier code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.WarehouseCode))
        {
            return ApplicationResult<PurchaseOrderResponse>.Validation("Delivery warehouse code is required.");
        }

        var supplier = await dbContext.Set<Supplier>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedSupplierCode, cancellationToken);

        if (supplier is null)
        {
            return ApplicationResult<PurchaseOrderResponse>.NotFound("Supplier was not found.");
        }

        if (!supplier.IsActive)
        {
            return ApplicationResult<PurchaseOrderResponse>.Validation(
                "Purchase orders cannot be created for an inactive supplier.");
        }

        var unknownItemFailure = await DescribeUnknownItemsAsync(request.Lines, cancellationToken);

        if (unknownItemFailure is not null)
        {
            return ApplicationResult<PurchaseOrderResponse>.Validation(unknownItemFailure);
        }

        PurchaseOrder order;

        try
        {
            order = new PurchaseOrder(normalizedSupplierCode, request.WarehouseCode, request.OrderDate);
            order.ReplaceLines(BuildLines(request.Lines));
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<PurchaseOrderResponse>.Validation(ex.Message);
        }

        order.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<PurchaseOrder>().Add(order);

        await WriteAuditAsync(
            "purchasing.order.created",
            OrdersEntity,
            order.Id,
            context,
            new { order.SupplierCode, order.WarehouseCode, order.OrderDate, order.TotalExclVat },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<PurchaseOrderResponse>.Success(Map(order, supplier.Name));
    }

    public async Task<ApplicationResult<PurchaseOrderResponse>> UpdateOrderLinesAsync(
        Guid id,
        UpdatePurchaseOrderLinesRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (request.Lines is null || request.Lines.Count == 0)
        {
            return ApplicationResult<PurchaseOrderResponse>.Validation("A purchase order must contain at least one line.");
        }

        // Read, check and write inside one Serializable transaction with the draft status
        // re-asserted by the atomic claim: without both, a concurrent /approve slips between
        // the in-memory check and the save, and an APPROVED - therefore frozen - order gets
        // its lines rewritten (same TOCTOU as AccountingService.UpdateEntryLinesAsync).
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var order = await dbContext.Set<PurchaseOrder>()
                .Include(current => current.Lines)
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (order is null)
            {
                return ApplicationResult<PurchaseOrderResponse>.NotFound("Purchase order was not found.");
            }

            // Checked here first so the immutability of an approved order surfaces as a 409
            // Conflict (the state of the resource forbids the operation), not a 400.
            if (order.Status != PurchaseOrderStatus.Draft)
            {
                return ApplicationResult<PurchaseOrderResponse>.Conflict(
                    "The lines of a purchase order are frozen once it is approved. Only drafts can be modified.");
            }

            var now = DateTimeOffset.UtcNow;

            if (!await TryClaimOrderAsync(order.Id, PurchaseOrderStatus.Draft, now, cancellationToken))
            {
                return ApplicationResult<PurchaseOrderResponse>.Conflict(ConcurrentOrderMutationRefused);
            }

            var unknownItemFailure = await DescribeUnknownItemsAsync(request.Lines, cancellationToken);

            if (unknownItemFailure is not null)
            {
                return ApplicationResult<PurchaseOrderResponse>.Validation(unknownItemFailure);
            }

            try
            {
                order.ReplaceLines(BuildLines(request.Lines));
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
            {
                return ApplicationResult<PurchaseOrderResponse>.Validation(ex.Message);
            }

            order.MarkUpdated(context.UserName, now);

            await WriteAuditAsync(
                "purchasing.order.lines_updated",
                OrdersEntity,
                order.Id,
                context,
                new { order.SupplierCode, LineCount = order.Lines.Count, order.TotalExclVat },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<PurchaseOrderResponse>.Success(
                Map(order, await LoadSupplierNameAsync(order.SupplierCode, cancellationToken)));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<PurchaseOrderResponse>.Conflict(ConcurrentOrderMutationRefused);
        }
    }

    public async Task<ApplicationResult<PurchaseOrderResponse>> ApproveOrderAsync(
        Guid id,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.Set<PurchaseOrder>()
            .Include(current => current.Lines)
            .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

        if (order is null)
        {
            return ApplicationResult<PurchaseOrderResponse>.NotFound("Purchase order was not found.");
        }

        var supplier = await dbContext.Set<Supplier>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == order.SupplierCode, cancellationToken);

        if (supplier is null)
        {
            return ApplicationResult<PurchaseOrderResponse>.Validation("The order's supplier no longer exists.");
        }

        // Approval engages the expense, so the references are re-checked here and not only at
        // capture time: a supplier may have been deactivated, an item removed from the stock
        // referential, while the order sat in the drafts.
        if (!supplier.IsActive)
        {
            return ApplicationResult<PurchaseOrderResponse>.Validation(
                "A purchase order cannot be approved for a supplier that has been deactivated.");
        }

        var unknownItemFailure = await DescribeUnknownItemsAsync(
            order.Lines.Select(line => line.ItemCode),
            cancellationToken);

        if (unknownItemFailure is not null)
        {
            return ApplicationResult<PurchaseOrderResponse>.Validation(unknownItemFailure);
        }

        var now = DateTimeOffset.UtcNow;

        // The numbering follows the APPROVAL year, not the (backdatable) order date: BC-{year}-
        // sequences are per approval year, because that is the year the expense was engaged.
        var year = now.Year;

        try
        {
            order.Approve(
                year,
                await NextApprovalSequenceAsync(year, cancellationToken),
                context.UserName,
                now);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<PurchaseOrderResponse>.Validation(ex.Message);
        }

        order.MarkUpdated(context.UserName, now);

        // The number is allocated as SELECT max(sequence)+1 protected by the unique index
        // ux_purchase_orders_approved_year_sequence - the exact mechanic of
        // BillingService.IssueInvoiceAsync. On a collision we re-check the row's status in the
        // database: when the collision was caused by the SAME order having been approved by a
        // concurrent request, the caller gets a clean 409 instead of a second number; otherwise
        // we retry exactly once with a freshly computed sequence.
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            var statusInDatabase = await dbContext.Set<PurchaseOrder>()
                .AsNoTracking()
                .Where(current => current.Id == id)
                .Select(current => current.Status)
                .SingleAsync(cancellationToken);

            if (statusInDatabase != PurchaseOrderStatus.Draft)
            {
                return ApplicationResult<PurchaseOrderResponse>.Conflict(
                    "The purchase order has already been approved by a concurrent operation.");
            }

            try
            {
                order.ReassignApprovalNumber(year, await NextApprovalSequenceAsync(year, cancellationToken));
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException retryEx) when (retryEx.IsUniqueViolation())
            {
                return ApplicationResult<PurchaseOrderResponse>.Conflict(
                    "Purchase order number allocation conflict. Please retry the operation.");
            }
        }

        await WriteAuditAsync(
            "purchasing.order.approved",
            OrdersEntity,
            order.Id,
            context,
            new { order.Number, order.SupplierCode, order.WarehouseCode, order.TotalExclVat },
            cancellationToken);

        return ApplicationResult<PurchaseOrderResponse>.Success(Map(order, supplier.Name));
    }

    public async Task<ApplicationResult<PurchaseOrderResponse>> ReceiveOrderAsync(
        Guid id,
        ReceivePurchaseOrderRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        if (request.Lines is null || request.Lines.Count == 0)
        {
            return ApplicationResult<PurchaseOrderResponse>.Validation("A receipt must contain at least one line.");
        }

        if (request.Lines.Select(line => line.LineId).Distinct().Count() != request.Lines.Count)
        {
            return ApplicationResult<PurchaseOrderResponse>.Validation(
                "A receipt cannot reference the same order line twice.");
        }

        // Serializable transaction + atomic claim: the stock entry and the updated cumulative
        // quantities must land together, and two concurrent receipts of the same order must not
        // both read the same "remaining to receive" and jointly over-receive the line.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var order = await dbContext.Set<PurchaseOrder>()
                .Include(current => current.Lines)
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (order is null)
            {
                return ApplicationResult<PurchaseOrderResponse>.NotFound("Purchase order was not found.");
            }

            if (!order.CanReceive)
            {
                return ApplicationResult<PurchaseOrderResponse>.Conflict(order.Status switch
                {
                    PurchaseOrderStatus.Draft => "A draft purchase order cannot be received: approve it first.",
                    PurchaseOrderStatus.Received => "This purchase order has already been fully received.",
                    _ => "A cancelled purchase order cannot be received."
                });
            }

            var now = DateTimeOffset.UtcNow;

            // The receivable status is re-asserted as the WHERE clause of one conditional
            // UPDATE: a concurrent cancellation or completing receipt makes the claim miss.
            if (!await TryClaimOrderAsync(order.Id, order.Status, now, cancellationToken))
            {
                return ApplicationResult<PurchaseOrderResponse>.Conflict(ConcurrentOrderMutationRefused);
            }

            // Domain-side validation and mutation FIRST: an over-receipt or an unknown line
            // must be refused before any stock movement is requested.
            try
            {
                order.RegisterReceipt(request.Lines.ToDictionary(line => line.LineId, line => line.Quantity));
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
            {
                return ApplicationResult<PurchaseOrderResponse>.Validation(ex.Message);
            }

            // The actual stock entry, through the stock module's published contract: one entry
            // movement per received line, into the order's delivery warehouse, referenced by the
            // order number, at the unit cost the line was ORDERED at. A refusal from the stock
            // side aborts the whole reception - the transaction is rolled back, the cumulative
            // quantities updated above never persist.
            var linesById = order.Lines.ToDictionary(line => line.Id);
            var entryLines = request.Lines
                .Select(line => new StockEntryLine(
                    linesById[line.LineId].ItemCode,
                    line.Quantity,
                    linesById[line.LineId].UnitPrice,
                    line.LotNumber,
                    line.ExpiryDate))
                .ToArray();

            var stockResult = await stockOperationService.RegisterPurchaseReceiptAsync(
                new RegisterPurchaseReceiptRequest(order.WarehouseCode, order.Number!, entryLines),
                context,
                cancellationToken);

            if (!stockResult.Succeeded)
            {
                var error = stockResult.Error ?? "The stock module refused the receipt.";

                return stockResult.ErrorType switch
                {
                    ApplicationErrorType.NotFound => ApplicationResult<PurchaseOrderResponse>.Validation(error),
                    ApplicationErrorType.Conflict => ApplicationResult<PurchaseOrderResponse>.Conflict(error),
                    _ => ApplicationResult<PurchaseOrderResponse>.Validation(error)
                };
            }

            order.MarkUpdated(context.UserName, now);

            await WriteAuditAsync(
                "purchasing.order.received",
                OrdersEntity,
                order.Id,
                context,
                new
                {
                    order.Number,
                    order.WarehouseCode,
                    Status = order.Status.ToString(),
                    ReceivedLineCount = request.Lines.Count,
                    stockResult.Value?.MovementCount
                },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<PurchaseOrderResponse>.Success(
                Map(order, await LoadSupplierNameAsync(order.SupplierCode, cancellationToken)));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<PurchaseOrderResponse>.Conflict(ConcurrentOrderMutationRefused);
        }
    }

    public async Task<ApplicationResult<PurchaseOrderResponse>> CancelOrderAsync(
        Guid id,
        CancelPurchaseOrderRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        // Serializable transaction + atomic claim: a cancellation racing a reception must lose
        // against the received - therefore uncancellable - order rather than quietly void the
        // supporting document of real stock movements.
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var order = await dbContext.Set<PurchaseOrder>()
                .Include(current => current.Lines)
                .SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (order is null)
            {
                return ApplicationResult<PurchaseOrderResponse>.NotFound("Purchase order was not found.");
            }

            if (order.HasAnyReceipt ||
                order.Status is PurchaseOrderStatus.PartiallyReceived or PurchaseOrderStatus.Received)
            {
                return ApplicationResult<PurchaseOrderResponse>.Conflict(
                    "A purchase order cannot be cancelled once a delivery has been received against it.");
            }

            var now = DateTimeOffset.UtcNow;

            // Claimed only on the cancellable statuses: an order already seen as Cancelled
            // falls through to order.Cancel, whose own refusal names the real reason.
            if (order.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Approved
                && !await TryClaimOrderAsync(order.Id, order.Status, now, cancellationToken))
            {
                return ApplicationResult<PurchaseOrderResponse>.Conflict(ConcurrentOrderMutationRefused);
            }

            try
            {
                order.Cancel(request.Reason, context.UserName, now);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return ApplicationResult<PurchaseOrderResponse>.Validation(ex.Message);
            }

            order.MarkUpdated(context.UserName, now);

            await WriteAuditAsync(
                "purchasing.order.cancelled",
                OrdersEntity,
                order.Id,
                context,
                new { order.Number, order.SupplierCode, order.CancellationReason },
                cancellationToken);

            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ApplicationResult<PurchaseOrderResponse>.Success(
                Map(order, await LoadSupplierNameAsync(order.SupplierCode, cancellationToken)));
        }
        catch (Exception exception) when (exception.IsSerializationFailure())
        {
            return ApplicationResult<PurchaseOrderResponse>.Conflict(ConcurrentOrderMutationRefused);
        }
    }

    /// <summary>
    /// Atomic form of "this order is still in the status I just checked". The invariant
    /// travels as the WHERE clause of one conditional UPDATE on the order's own row, so the
    /// database evaluates it at the instant the row is claimed - the claim-in-one-statement
    /// pattern of <c>AccountingService.TryClaimDraftEntryAsync</c>. Returns true only when the
    /// statement really matched the row.
    /// </summary>
    private async Task<bool> TryClaimOrderAsync(
        Guid orderId,
        PurchaseOrderStatus expectedStatus,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var claimedRows = await dbContext.Set<PurchaseOrder>()
            .Where(current => current.Id == orderId && current.Status == expectedStatus)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(current => current.UpdatedAt, now),
                cancellationToken);

        return claimedRows == 1;
    }

    private async Task<int> NextApprovalSequenceAsync(int year, CancellationToken cancellationToken)
    {
        var maxSequence = await dbContext.Set<PurchaseOrder>()
            .Where(order => order.ApprovedYear == year)
            .MaxAsync(order => (int?)order.ApprovedSequence, cancellationToken);

        return (maxSequence ?? 0) + 1;
    }

    /// <summary>
    /// Asserts that every ordered item exists in the stock referential, through the stock
    /// module's published <see cref="IStockCostProvider"/> contract - the purchasing module
    /// never queries the stock module's own tables. Returns the failure message naming the
    /// first unknown item, or null when every code resolves.
    /// </summary>
    private async Task<string?> DescribeUnknownItemsAsync(
        IEnumerable<PurchaseOrderLineRequest> lines,
        CancellationToken cancellationToken)
    {
        return await DescribeUnknownItemsAsync(lines.Select(line => line.ItemCode), cancellationToken);
    }

    private async Task<string?> DescribeUnknownItemsAsync(
        IEnumerable<string> itemCodes,
        CancellationToken cancellationToken)
    {
        var normalizedCodes = itemCodes
            .Select(code => string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant())
            .Distinct()
            .ToArray();

        if (normalizedCodes.Any(string.IsNullOrEmpty))
        {
            return "Every purchase order line requires an item code.";
        }

        foreach (var itemCode in normalizedCodes)
        {
            var itemResult = await stockCostProvider.GetAverageCostAsync(itemCode, cancellationToken);

            if (!itemResult.Succeeded)
            {
                return $"Item '{itemCode}' is unknown to the stock referential: {itemResult.Error}";
            }
        }

        return null;
    }

    private static List<PurchaseOrderLine> BuildLines(IReadOnlyCollection<PurchaseOrderLineRequest> requests)
    {
        return requests
            .Select(line => new PurchaseOrderLine(line.ItemCode, line.Designation, line.Quantity, line.UnitPrice))
            .ToList();
    }

    private async Task<string?> LoadSupplierNameAsync(string supplierCode, CancellationToken cancellationToken)
    {
        return await dbContext.Set<Supplier>()
            .AsNoTracking()
            .Where(supplier => supplier.Code == supplierCode)
            .Select(supplier => supplier.Name)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<Dictionary<string, string>> LoadSupplierNamesAsync(
        string[] supplierCodes,
        CancellationToken cancellationToken)
    {
        if (supplierCodes.Length == 0)
        {
            return new Dictionary<string, string>();
        }

        return await dbContext.Set<Supplier>()
            .AsNoTracking()
            .Where(supplier => supplierCodes.Contains(supplier.Code))
            .ToDictionaryAsync(supplier => supplier.Code, supplier => supplier.Name, cancellationToken);
    }

    private static SupplierResponse Map(Supplier supplier)
    {
        return new SupplierResponse(
            supplier.Id,
            supplier.Code,
            supplier.Name,
            supplier.SupplierType,
            supplier.Nif,
            supplier.Rc,
            supplier.Ai,
            supplier.Nis,
            supplier.Address,
            supplier.City,
            supplier.Phone,
            supplier.Email,
            supplier.IsActive,
            supplier.CreatedAt,
            supplier.CreatedBy,
            supplier.UpdatedAt,
            supplier.UpdatedBy);
    }

    private static PurchaseOrderResponse Map(PurchaseOrder order, string? supplierName)
    {
        var lines = order.Lines
            .OrderBy(line => line.LineNumber)
            .Select(line => new PurchaseOrderLineResponse(
                line.Id,
                line.LineNumber,
                line.ItemCode,
                line.Designation,
                line.Quantity,
                line.UnitPrice,
                line.LineTotalExclVat,
                line.QuantityReceived,
                line.RemainingQuantity))
            .ToArray();

        return new PurchaseOrderResponse(
            order.Id,
            order.Number,
            order.SupplierCode,
            supplierName,
            order.WarehouseCode,
            order.OrderDate,
            order.Status,
            order.TotalExclVat,
            order.Lines.Sum(line => line.Quantity),
            order.Lines.Sum(line => line.QuantityReceived),
            lines,
            order.CanEdit,
            order.CanReceive,
            order.ApprovedAt,
            order.ApprovedBy,
            order.CancelledAt,
            order.CancelledBy,
            order.CancellationReason,
            order.CreatedAt,
            order.CreatedBy,
            order.UpdatedAt,
            order.UpdatedBy);
    }

    private static string NormalizeCodeOrEmpty(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
    }

    private static string? NormalizeNullableCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
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
