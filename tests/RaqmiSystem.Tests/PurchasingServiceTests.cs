using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Inventory;
using RaqmiSystem.Application.Purchasing;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Purchasing;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Purchasing;

namespace RaqmiSystem.Tests;

/// <summary>
/// Service-level coverage of the purchasing workflows against a dedicated SQLite ":memory:"
/// database (one per test), with the stock module pinned by
/// <see cref="PurchasingStockOperationStub"/> and <see cref="PurchasingStockCostStub"/>: the
/// purchasing module only CONSUMES those contracts, so the tests assert WHAT IT ASKS the stock
/// side for rather than depending on stock data.
///
/// What is pinned here: the number allocated at approval (never before), the lines frozen right
/// after, the cumulative arithmetic of successive partial receipts with the exact status that
/// follows, the refusal of an over-receipt (and the absence of any stock entry when it is
/// refused), the unit COST handed to the stock module, and the impossibility of cancelling once
/// a delivery has landed.
/// </summary>
public sealed class PurchasingServiceTests
{
    private const string SupplierCode = "FRN-MEDINA";

    private const string WarehouseCode = "DEP-CENTRAL";

    private const string SugarCode = "SUC-01";

    private const string OilCode = "HUI-02";

    private static readonly DateOnly OrderDate = new(2026, 4, 12);

    private static readonly OperationContext Context = new(null, "acheteur", "127.0.0.1");

    [Fact]
    public async Task A_draft_carries_no_number_and_approval_allocates_it()
    {
        await using var harness = await HarnessAsync();

        var draft = await CreateOrderAsync(harness);

        Assert.Null(draft.Number);
        Assert.Equal(PurchaseOrderStatus.Draft, draft.Status);
        Assert.True(draft.CanEdit);
        Assert.False(draft.CanReceive);

        // 20 x 4 500.00 + 12 x 1 250.50
        Assert.Equal(105_006.00m, draft.TotalExclVat);
        Assert.Equal(32m, draft.TotalQuantityOrdered);
        Assert.Equal(0m, draft.TotalQuantityReceived);

        var approved = await harness.Service.ApproveOrderAsync(draft.Id, Context, CancellationToken.None);

        Assert.True(approved.Succeeded, approved.Error);
        Assert.Equal(PurchaseOrderStatus.Approved, approved.Value!.Status);

        // The sequence follows the APPROVAL year - the year the expense was engaged - not the
        // (backdatable) order date, which sits in 2026 whatever today is.
        Assert.Equal($"BC-{DateTimeOffset.UtcNow.Year}-000001", approved.Value.Number);
        Assert.Equal("acheteur", approved.Value.ApprovedBy);
        Assert.NotNull(approved.Value.ApprovedAt);
        Assert.True(approved.Value.CanReceive);
        Assert.False(approved.Value.CanEdit);
    }

    [Fact]
    public async Task Lines_are_frozen_once_the_order_is_approved()
    {
        await using var harness = await HarnessAsync();

        var draft = await CreateOrderAsync(harness);

        // While it is a draft, rewriting the lines is legitimate.
        var rewritten = await harness.Service.UpdateOrderLinesAsync(
            draft.Id,
            new UpdatePurchaseOrderLinesRequest([new PurchaseOrderLineRequest(SugarCode, "Sucre cristallise 50 kg", 5m, 4_500.00m)]),
            Context,
            CancellationToken.None);

        Assert.True(rewritten.Succeeded, rewritten.Error);
        Assert.Equal(22_500.00m, rewritten.Value!.TotalExclVat);
        Assert.Single(rewritten.Value.Lines);

        var approved = await harness.Service.ApproveOrderAsync(draft.Id, Context, CancellationToken.None);
        Assert.True(approved.Succeeded, approved.Error);

        var refused = await harness.Service.UpdateOrderLinesAsync(
            draft.Id,
            new UpdatePurchaseOrderLinesRequest([new PurchaseOrderLineRequest(SugarCode, "Sucre", 999m, 1.00m)]),
            Context,
            CancellationToken.None);

        // The immutability of an approved order is a state conflict, not a malformed request.
        Assert.False(refused.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, refused.ErrorType);

        // The refusal changed nothing in the database.
        var stored = await harness.DbContext.Set<PurchaseOrder>()
            .AsNoTracking()
            .Include(order => order.Lines)
            .SingleAsync(order => order.Id == draft.Id);

        var line = Assert.Single(stored.Lines);
        Assert.Equal(5m, line.Quantity);
        Assert.Equal(22_500.00m, stored.TotalExclVat);
    }

    [Fact]
    public async Task Successive_partial_receipts_cumulate_and_drive_the_status()
    {
        await using var harness = await HarnessAsync();

        var order = await CreateApprovedOrderAsync(harness);
        var sugar = LineOf(order, SugarCode);
        var oil = LineOf(order, OilCode);

        var first = await harness.Service.ReceiveOrderAsync(
            order.Id,
            new ReceivePurchaseOrderRequest([new ReceivePurchaseOrderLineRequest(sugar.Id, 8m)]),
            Context,
            CancellationToken.None);

        Assert.True(first.Succeeded, first.Error);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, first.Value!.Status);
        Assert.Equal(8m, first.Value.TotalQuantityReceived);
        Assert.Equal(8m, LineOf(first.Value, SugarCode).QuantityReceived);
        Assert.Equal(12m, LineOf(first.Value, SugarCode).RemainingQuantity);
        Assert.Equal(0m, LineOf(first.Value, OilCode).QuantityReceived);

        var second = await harness.Service.ReceiveOrderAsync(
            order.Id,
            new ReceivePurchaseOrderRequest(
            [
                new ReceivePurchaseOrderLineRequest(sugar.Id, 12m),
                new ReceivePurchaseOrderLineRequest(oil.Id, 5m)
            ]),
            Context,
            CancellationToken.None);

        Assert.True(second.Succeeded, second.Error);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, second.Value!.Status);
        Assert.Equal(20m, LineOf(second.Value, SugarCode).QuantityReceived);
        Assert.Equal(0m, LineOf(second.Value, SugarCode).RemainingQuantity);
        Assert.Equal(5m, LineOf(second.Value, OilCode).QuantityReceived);

        var third = await harness.Service.ReceiveOrderAsync(
            order.Id,
            new ReceivePurchaseOrderRequest([new ReceivePurchaseOrderLineRequest(oil.Id, 7m)]),
            Context,
            CancellationToken.None);

        // Every line fully received: the status turns to Received on its own.
        Assert.True(third.Succeeded, third.Error);
        Assert.Equal(PurchaseOrderStatus.Received, third.Value!.Status);
        Assert.Equal(32m, third.Value.TotalQuantityReceived);
        Assert.Equal(32m, third.Value.TotalQuantityOrdered);
        Assert.False(third.Value.CanReceive);

        // Three deliveries, three stock entries requested: one per receipt, never replayed.
        Assert.Equal(3, harness.StockOperations.Requests.Count);

        // A fully received order refuses a fourth delivery.
        var extra = await harness.Service.ReceiveOrderAsync(
            order.Id,
            new ReceivePurchaseOrderRequest([new ReceivePurchaseOrderLineRequest(oil.Id, 1m)]),
            Context,
            CancellationToken.None);

        Assert.False(extra.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, extra.ErrorType);
        Assert.Equal(3, harness.StockOperations.Requests.Count);
    }

    [Fact]
    public async Task A_receipt_feeds_the_stock_module_at_the_ordered_unit_cost()
    {
        await using var harness = await HarnessAsync();

        var order = await CreateApprovedOrderAsync(harness);
        var sugar = LineOf(order, SugarCode);
        var oil = LineOf(order, OilCode);

        var received = await harness.Service.ReceiveOrderAsync(
            order.Id,
            new ReceivePurchaseOrderRequest(
            [
                new ReceivePurchaseOrderLineRequest(sugar.Id, 8m, "LOT-2026-04", new DateOnly(2027, 4, 30)),
                new ReceivePurchaseOrderLineRequest(oil.Id, 3m)
            ]),
            Context,
            CancellationToken.None);

        Assert.True(received.Succeeded, received.Error);

        var request = Assert.Single(harness.StockOperations.Requests);

        // The entry lands in the order's delivery warehouse and cites the order number as its
        // reference - the traceable link between the stock movement and its supporting document.
        Assert.Equal(WarehouseCode, request.WarehouseCode);
        Assert.Equal(received.Value!.Number, request.Reference);
        Assert.Equal(2, request.Lines.Count);

        var sugarEntry = request.Lines.Single(line => line.ItemCode == SugarCode);
        Assert.Equal(8m, sugarEntry.Quantity);

        // The valuation is the price the line was ORDERED at, not an average cost read back
        // from the stock module: this receipt is precisely what will move that average.
        Assert.Equal(4_500.00m, sugarEntry.UnitCost);
        Assert.Equal("LOT-2026-04", sugarEntry.LotNumber);
        Assert.Equal(new DateOnly(2027, 4, 30), sugarEntry.ExpiryDate);

        var oilEntry = request.Lines.Single(line => line.ItemCode == OilCode);
        Assert.Equal(3m, oilEntry.Quantity);
        Assert.Equal(1_250.50m, oilEntry.UnitCost);
        Assert.Null(oilEntry.LotNumber);
        Assert.Null(oilEntry.ExpiryDate);
    }

    [Fact]
    public async Task An_over_receipt_is_refused_and_requests_no_stock_entry_at_all()
    {
        await using var harness = await HarnessAsync();

        var order = await CreateApprovedOrderAsync(harness);
        var sugar = LineOf(order, SugarCode);
        var oil = LineOf(order, OilCode);

        var first = await harness.Service.ReceiveOrderAsync(
            order.Id,
            new ReceivePurchaseOrderRequest([new ReceivePurchaseOrderLineRequest(sugar.Id, 15m)]),
            Context,
            CancellationToken.None);

        Assert.True(first.Succeeded, first.Error);
        Assert.Single(harness.StockOperations.Requests);

        // 6 more bags exceed the 5 that remain. The oil quantity submitted in the same delivery
        // is perfectly valid, but the WHOLE receipt must be refused - and above all, no stock
        // movement may be requested for a delivery that is not accepted.
        var refused = await harness.Service.ReceiveOrderAsync(
            order.Id,
            new ReceivePurchaseOrderRequest(
            [
                new ReceivePurchaseOrderLineRequest(sugar.Id, 6m),
                new ReceivePurchaseOrderLineRequest(oil.Id, 4m)
            ]),
            Context,
            CancellationToken.None);

        Assert.False(refused.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, refused.ErrorType);
        Assert.Contains("exceeds", refused.Error);
        Assert.Single(harness.StockOperations.Requests);

        var stored = await harness.DbContext.Set<PurchaseOrder>()
            .AsNoTracking()
            .Include(current => current.Lines)
            .SingleAsync(current => current.Id == order.Id);

        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, stored.Status);
        Assert.Equal(15m, stored.Lines.Single(line => line.ItemCode == SugarCode).QuantityReceived);
        Assert.Equal(0m, stored.Lines.Single(line => line.ItemCode == OilCode).QuantityReceived);
    }

    [Fact]
    public async Task A_refusal_from_the_stock_module_rolls_the_whole_receipt_back()
    {
        await using var harness = await HarnessAsync();

        var order = await CreateApprovedOrderAsync(harness);
        var sugar = LineOf(order, SugarCode);

        harness.StockOperations.NextResult = ApplicationResult<StockEntryResult>
            .Validation("The delivery warehouse is unknown to the stock referential.");

        var refused = await harness.Service.ReceiveOrderAsync(
            order.Id,
            new ReceivePurchaseOrderRequest([new ReceivePurchaseOrderLineRequest(sugar.Id, 4m)]),
            Context,
            CancellationToken.None);

        Assert.False(refused.Succeeded);
        Assert.Equal("The delivery warehouse is unknown to the stock referential.", refused.Error);

        // The cumulative quantities updated in memory before the stock call never persist.
        var stored = await harness.DbContext.Set<PurchaseOrder>()
            .AsNoTracking()
            .Include(current => current.Lines)
            .SingleAsync(current => current.Id == order.Id);

        Assert.Equal(PurchaseOrderStatus.Approved, stored.Status);
        Assert.Equal(0m, stored.Lines.Sum(line => line.QuantityReceived));
    }

    [Fact]
    public async Task Cancellation_is_refused_once_a_delivery_has_been_received()
    {
        await using var harness = await HarnessAsync();

        var order = await CreateApprovedOrderAsync(harness);
        var sugar = LineOf(order, SugarCode);

        var received = await harness.Service.ReceiveOrderAsync(
            order.Id,
            new ReceivePurchaseOrderRequest([new ReceivePurchaseOrderLineRequest(sugar.Id, 1m)]),
            Context,
            CancellationToken.None);

        Assert.True(received.Succeeded, received.Error);

        var refused = await harness.Service.CancelOrderAsync(
            order.Id,
            new CancelPurchaseOrderRequest("Fournisseur defaillant"),
            Context,
            CancellationToken.None);

        Assert.False(refused.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, refused.ErrorType);

        var stored = await harness.DbContext.Set<PurchaseOrder>()
            .AsNoTracking()
            .SingleAsync(current => current.Id == order.Id);

        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, stored.Status);
        Assert.Null(stored.CancelledAt);
        Assert.Null(stored.CancellationReason);
    }

    [Fact]
    public async Task An_approved_order_without_receipt_is_cancellable_with_a_reason()
    {
        await using var harness = await HarnessAsync();

        var order = await CreateApprovedOrderAsync(harness);

        var missingReason = await harness.Service.CancelOrderAsync(
            order.Id,
            new CancelPurchaseOrderRequest("   "),
            Context,
            CancellationToken.None);

        Assert.False(missingReason.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, missingReason.ErrorType);

        var cancelled = await harness.Service.CancelOrderAsync(
            order.Id,
            new CancelPurchaseOrderRequest("Rupture chez le fournisseur"),
            Context,
            CancellationToken.None);

        Assert.True(cancelled.Succeeded, cancelled.Error);
        Assert.Equal(PurchaseOrderStatus.Cancelled, cancelled.Value!.Status);
        Assert.Equal("Rupture chez le fournisseur", cancelled.Value.CancellationReason);

        // A cancelled order can no longer be received.
        var refusedReceipt = await harness.Service.ReceiveOrderAsync(
            order.Id,
            new ReceivePurchaseOrderRequest([new ReceivePurchaseOrderLineRequest(LineOf(order, SugarCode).Id, 1m)]),
            Context,
            CancellationToken.None);

        Assert.False(refusedReceipt.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, refusedReceipt.ErrorType);
        Assert.Empty(harness.StockOperations.Requests);
    }

    [Fact]
    public async Task An_item_unknown_to_the_stock_referential_is_refused_at_capture_time()
    {
        await using var harness = await HarnessAsync();

        var result = await harness.Service.CreateOrderAsync(
            new CreatePurchaseOrderRequest(
                SupplierCode,
                WarehouseCode,
                OrderDate,
                [new PurchaseOrderLineRequest("INCONNU-99", "Article fantome", 1m, 10.00m)]),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);
        Assert.Contains("INCONNU-99", result.Error);

        Assert.Equal(0, await harness.DbContext.Set<PurchaseOrder>().CountAsync());
    }

    [Fact]
    public async Task A_deactivated_supplier_blocks_both_capture_and_approval()
    {
        await using var harness = await HarnessAsync();

        // Captured while the supplier is still active...
        var draft = await CreateOrderAsync(harness);

        var deactivated = await harness.Service.SetSupplierActiveAsync(
            SupplierCode,
            isActive: false,
            Context,
            CancellationToken.None);

        Assert.True(deactivated.Succeeded, deactivated.Error);

        // ...but approval re-checks the references, because it is the act that engages the
        // expense: a supplier deactivated while the order sat in the drafts blocks it.
        var refusedApproval = await harness.Service.ApproveOrderAsync(draft.Id, Context, CancellationToken.None);

        Assert.False(refusedApproval.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, refusedApproval.ErrorType);

        var refusedCapture = await harness.Service.CreateOrderAsync(
            new CreatePurchaseOrderRequest(
                SupplierCode,
                WarehouseCode,
                OrderDate,
                [new PurchaseOrderLineRequest(SugarCode, "Sucre cristallise 50 kg", 1m, 4_500.00m)]),
            Context,
            CancellationToken.None);

        Assert.False(refusedCapture.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, refusedCapture.ErrorType);

        // The refused approval left a pristine draft: no number was burnt.
        var stored = await harness.DbContext.Set<PurchaseOrder>()
            .AsNoTracking()
            .SingleAsync(current => current.Id == draft.Id);

        Assert.Null(stored.Number);
        Assert.Equal(PurchaseOrderStatus.Draft, stored.Status);
    }

    private static PurchaseOrderLineResponse LineOf(PurchaseOrderResponse order, string itemCode)
    {
        return order.Lines.Single(line => line.ItemCode == itemCode);
    }

    private static async Task<PurchaseOrderResponse> CreateOrderAsync(Harness harness)
    {
        var result = await harness.Service.CreateOrderAsync(
            new CreatePurchaseOrderRequest(
                SupplierCode,
                WarehouseCode,
                OrderDate,
                [
                    new PurchaseOrderLineRequest(SugarCode, "Sucre cristallise 50 kg", 20m, 4_500.00m),
                    new PurchaseOrderLineRequest(OilCode, "Huile de table 5 L", 12m, 1_250.50m)
                ]),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);

        return result.Value!;
    }

    private static async Task<PurchaseOrderResponse> CreateApprovedOrderAsync(Harness harness)
    {
        var draft = await CreateOrderAsync(harness);

        var approved = await harness.Service.ApproveOrderAsync(draft.Id, Context, CancellationToken.None);

        Assert.True(approved.Succeeded, approved.Error);

        return approved.Value!;
    }

    private static async Task<Harness> HarnessAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var dbContext = new RaqmiDbContext(
            new DbContextOptionsBuilder<RaqmiDbContext>()
                .UseSqlite(connection)
                .Options);

        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Set<Supplier>().Add(new Supplier(
            SupplierCode,
            "SARL Medina Distribution",
            SupplierType.Company,
            nif: "098765432112345",
            city: "Alger"));

        await dbContext.SaveChangesAsync();

        var stockOperations = new PurchasingStockOperationStub();
        var stockCosts = new PurchasingStockCostStub(SugarCode, OilCode);

        return new Harness(
            connection,
            dbContext,
            stockOperations,
            new PurchasingService(dbContext, new AuditLogWriter(dbContext), stockOperations, stockCosts));
    }

    private sealed class Harness(
        SqliteConnection connection,
        RaqmiDbContext dbContext,
        PurchasingStockOperationStub stockOperations,
        PurchasingService service) : IAsyncDisposable
    {
        public RaqmiDbContext DbContext { get; } = dbContext;

        public PurchasingStockOperationStub StockOperations { get; } = stockOperations;

        public PurchasingService Service { get; } = service;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
