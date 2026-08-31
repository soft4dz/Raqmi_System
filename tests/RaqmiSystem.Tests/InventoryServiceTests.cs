using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Inventory;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Inventory;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Inventory;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Service-level coverage of the stocks module against a dedicated SQLite ":memory:" database
/// (one per test): the never-negative guard, the exactness of the weighted average cost over
/// several entries, the all-or-nothing shape of a transfer, the adjustments an inventory
/// validation generates and the immutability that follows, and the threshold alerts.
///
/// <see cref="InventoryStockConcurrencyTests"/> completes this file where a single-request test
/// cannot reach: the same never-negative invariant under two simultaneous outflows.
/// </summary>
public sealed class InventoryServiceTests
{
    private const string UnitCode = "HTL1";

    private const string MainWarehouse = "MAG1";

    private const string SecondWarehouse = "MAG2";

    private const string FlourItem = "FAR-T55";

    private const string OilItem = "HUI-5L";

    private static readonly DateOnly Today = new(2030, 3, 15);

    private static readonly OperationContext Context = new(null, "magasinier", "127.0.0.1");

    // ------------------------- Stock is never negative --------------------------

    [Fact]
    public async Task An_outflow_larger_than_the_stock_is_refused_and_writes_nothing()
    {
        await using var harness = await HarnessAsync();

        await EnterAsync(harness, MainWarehouse, FlourItem, 10m, 250.00m, "BL-1");

        var result = await harness.Service.CreateMovementAsync(
            ConsumptionRequest(MainWarehouse, FlourItem, 10.001m, "BS-1"),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, result.ErrorType);

        // The refusal names the available stock, so the user knows what they CAN take out.
        Assert.Contains("10", result.Error);
        Assert.Contains("negative", result.Error, StringComparison.OrdinalIgnoreCase);

        // Nothing was written: the registry still holds the single entry.
        Assert.Equal(1, await harness.DbContext.Set<StockMovement>().CountAsync());
        Assert.Equal(10m, await CurrentStockAsync(harness, MainWarehouse, FlourItem));
    }

    [Fact]
    public async Task An_outflow_that_empties_the_stock_exactly_is_accepted()
    {
        await using var harness = await HarnessAsync();

        await EnterAsync(harness, MainWarehouse, FlourItem, 10m, 250.00m, "BL-1");

        var result = await harness.Service.CreateMovementAsync(
            ConsumptionRequest(MainWarehouse, FlourItem, 10m, "BS-1"),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(-10m, result.Value!.SignedQuantity);
        Assert.Equal(0m, await CurrentStockAsync(harness, MainWarehouse, FlourItem));
    }

    [Fact]
    public async Task A_decreasing_adjustment_is_guarded_exactly_like_a_consumption()
    {
        await using var harness = await HarnessAsync();

        await EnterAsync(harness, MainWarehouse, FlourItem, 5m, 250.00m, "BL-1");

        var result = await harness.Service.CreateMovementAsync(
            new CreateStockMovementRequest(
                MainWarehouse, FlourItem, Today, StockMovementKind.InventoryAdjustment,
                6m, null, "AJ-1", null, null, null, AdjustmentIsIncrease: false),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, result.ErrorType);
        Assert.Equal(5m, await CurrentStockAsync(harness, MainWarehouse, FlourItem));
    }

    [Fact]
    public async Task Stock_is_counted_per_warehouse_not_across_the_group()
    {
        await using var harness = await HarnessAsync();

        await EnterAsync(harness, SecondWarehouse, FlourItem, 100m, 250.00m, "BL-1");

        // 100 kg exist in the group, none of them in MAG1: the outflow is still refused.
        var result = await harness.Service.CreateMovementAsync(
            ConsumptionRequest(MainWarehouse, FlourItem, 1m, "BS-1"),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, result.ErrorType);
    }

    // ----------------------- Weighted average cost (PMP) ------------------------

    [Fact]
    public async Task The_average_cost_is_exact_over_several_entries_at_different_prices()
    {
        await using var harness = await HarnessAsync();

        // 100 kg at 10.00 and 50 kg at 13.00 => (1000 + 650) / 150 = 11.00 exactly.
        await EnterAsync(harness, MainWarehouse, FlourItem, 100m, 10.00m, "BL-1");
        await EnterAsync(harness, MainWarehouse, FlourItem, 50m, 13.00m, "BL-2");

        var cost = await harness.Service.GetAverageCostAsync(FlourItem, CancellationToken.None);

        Assert.True(cost.Succeeded, cost.Error);
        Assert.Equal(11.00m, cost.Value!.AverageUnitCost);
        Assert.Equal("kg", cost.Value.UnitOfMeasure);
    }

    [Fact]
    public async Task The_average_cost_is_the_weighted_one_not_the_arithmetic_mean_and_rounds_to_two_decimals()
    {
        await using var harness = await HarnessAsync();

        // 3 at 10.00, 3 at 11.00, 3 at 13.00 => 102 / 9 = 11.3333... => 11.33.
        // The arithmetic mean of the three prices is 11.33 too, so the weights are made to
        // matter: a fourth, much larger entry pulls the weighted figure away from the mean.
        await EnterAsync(harness, MainWarehouse, FlourItem, 3m, 10.00m, "BL-1");
        await EnterAsync(harness, MainWarehouse, FlourItem, 3m, 11.00m, "BL-2");
        await EnterAsync(harness, MainWarehouse, FlourItem, 3m, 13.00m, "BL-3");

        var afterThree = await harness.Service.GetAverageCostAsync(FlourItem, CancellationToken.None);
        Assert.Equal(11.33m, afterThree.Value!.AverageUnitCost);

        // 91 kg at 20.00 on top: (102 + 1820) / 100 = 19.22, far from the 13.50 mean of prices.
        await EnterAsync(harness, MainWarehouse, FlourItem, 91m, 20.00m, "BL-4");

        var afterFour = await harness.Service.GetAverageCostAsync(FlourItem, CancellationToken.None);
        Assert.Equal(19.22m, afterFour.Value!.AverageUnitCost);
    }

    [Fact]
    public async Task Outflows_do_not_change_the_average_cost_only_entries_do()
    {
        await using var harness = await HarnessAsync();

        await EnterAsync(harness, MainWarehouse, FlourItem, 100m, 10.00m, "BL-1");
        await EnterAsync(harness, MainWarehouse, FlourItem, 100m, 20.00m, "BL-2");

        var before = await harness.Service.GetAverageCostAsync(FlourItem, CancellationToken.None);
        Assert.Equal(15.00m, before.Value!.AverageUnitCost);

        await harness.Service.CreateMovementAsync(
            ConsumptionRequest(MainWarehouse, FlourItem, 150m, "BS-1"),
            Context,
            CancellationToken.None);

        var after = await harness.Service.GetAverageCostAsync(FlourItem, CancellationToken.None);
        Assert.Equal(15.00m, after.Value!.AverageUnitCost);
    }

    [Fact]
    public async Task An_item_that_never_entered_stock_has_a_zero_cost_but_an_unknown_item_is_not_found()
    {
        await using var harness = await HarnessAsync();

        var known = await harness.Service.GetAverageCostAsync(OilItem, CancellationToken.None);

        Assert.True(known.Succeeded, known.Error);
        Assert.Equal(0m, known.Value!.AverageUnitCost);

        var unknown = await harness.Service.GetAverageCostAsync("PAS-UN-ARTICLE", CancellationToken.None);

        Assert.False(unknown.Succeeded);
        Assert.Equal(ApplicationErrorType.NotFound, unknown.ErrorType);
    }

    [Fact]
    public async Task Warehouse_stock_values_every_line_and_totals_them_server_side()
    {
        await using var harness = await HarnessAsync();

        await EnterAsync(harness, MainWarehouse, FlourItem, 100m, 10.00m, "BL-1");
        await EnterAsync(harness, MainWarehouse, FlourItem, 50m, 13.00m, "BL-2");
        await EnterAsync(harness, MainWarehouse, OilItem, 20m, 500.00m, "BL-3");

        var stock = await harness.Service.GetWarehouseStockAsync(MainWarehouse, CancellationToken.None);

        Assert.True(stock.Succeeded, stock.Error);

        var flour = stock.Value!.Rows.Single(row => row.ItemCode == FlourItem);
        Assert.Equal(150m, flour.Quantity);
        Assert.Equal(11.00m, flour.AverageUnitCost);
        Assert.Equal(1_650.00m, flour.StockValue);

        var oil = stock.Value.Rows.Single(row => row.ItemCode == OilItem);
        Assert.Equal(10_000.00m, oil.StockValue);

        // The total is the sum of the rows, computed by the server: the screen never adds up
        // financial figures on its own.
        Assert.Equal(11_650.00m, stock.Value.TotalValue);
    }

    // --------------------------------- Transfers --------------------------------

    [Fact]
    public async Task A_transfer_moves_the_quantity_as_two_linked_halves_in_one_operation()
    {
        await using var harness = await HarnessAsync();

        await EnterAsync(harness, MainWarehouse, FlourItem, 40m, 250.00m, "BL-1");

        var result = await harness.Service.TransferAsync(
            new CreateStockTransferRequest(
                MainWarehouse, SecondWarehouse, FlourItem, Today, 15m, "TR-1", null, null, null),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(result.Value!.OutMovement.TransferGroupId, result.Value.InMovement.TransferGroupId);

        Assert.Equal(25m, await CurrentStockAsync(harness, MainWarehouse, FlourItem));
        Assert.Equal(15m, await CurrentStockAsync(harness, SecondWarehouse, FlourItem));

        var halves = await harness.DbContext.Set<StockMovement>()
            .Where(movement => movement.TransferGroupId == result.Value.TransferGroupId)
            .ToArrayAsync();

        Assert.Equal(2, halves.Length);
        Assert.Contains(halves, movement => movement.Kind == StockMovementKind.TransferOut);
        Assert.Contains(halves, movement => movement.Kind == StockMovementKind.TransferIn);
    }

    [Fact]
    public async Task A_transfer_refused_for_lack_of_stock_leaves_NEITHER_half_behind()
    {
        await using var harness = await HarnessAsync();

        await EnterAsync(harness, MainWarehouse, FlourItem, 5m, 250.00m, "BL-1");

        var result = await harness.Service.TransferAsync(
            new CreateStockTransferRequest(
                MainWarehouse, SecondWarehouse, FlourItem, Today, 6m, "TR-1", null, null, null),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, result.ErrorType);

        // The destination did NOT receive a phantom quantity: a half-committed transfer would
        // create goods out of nothing.
        Assert.Equal(0m, await CurrentStockAsync(harness, SecondWarehouse, FlourItem));
        Assert.Equal(5m, await CurrentStockAsync(harness, MainWarehouse, FlourItem));

        Assert.Equal(0, await harness.DbContext.Set<StockMovement>()
            .CountAsync(movement => movement.TransferGroupId != null));
    }

    [Fact]
    public async Task A_single_transfer_half_cannot_be_captured_through_the_movement_route()
    {
        await using var harness = await HarnessAsync();

        var result = await harness.Service.CreateMovementAsync(
            new CreateStockMovementRequest(
                MainWarehouse, FlourItem, Today, StockMovementKind.TransferOut,
                1m, null, "TR-1", null, null, null, null),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);
        Assert.Contains("two linked halves", result.Error);
    }

    // ------------------------------ Referential ---------------------------------

    [Fact]
    public async Task A_purchase_entry_without_a_unit_cost_is_refused()
    {
        await using var harness = await HarnessAsync();

        var result = await harness.Service.CreateMovementAsync(
            new CreateStockMovementRequest(
                MainWarehouse, FlourItem, Today, StockMovementKind.PurchaseEntry,
                1m, UnitCost: null, "BL-1", null, null, null, null),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);
        Assert.Contains("weighted average", result.Error);
    }

    [Fact]
    public async Task A_movement_on_a_deactivated_warehouse_or_item_is_refused()
    {
        await using var harness = await HarnessAsync();

        await harness.Service.SetItemActiveAsync(OilItem, false, Context, CancellationToken.None);

        var onItem = await harness.Service.CreateMovementAsync(
            new CreateStockMovementRequest(
                MainWarehouse, OilItem, Today, StockMovementKind.PurchaseEntry,
                1m, 10.00m, "BL-1", null, null, null, null),
            Context,
            CancellationToken.None);

        Assert.False(onItem.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, onItem.ErrorType);

        await harness.Service.SetWarehouseActiveAsync(SecondWarehouse, false, Context, CancellationToken.None);

        var onWarehouse = await harness.Service.CreateMovementAsync(
            new CreateStockMovementRequest(
                SecondWarehouse, FlourItem, Today, StockMovementKind.PurchaseEntry,
                1m, 10.00m, "BL-1", null, null, null, null),
            Context,
            CancellationToken.None);

        Assert.False(onWarehouse.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, onWarehouse.ErrorType);
    }

    // ------------------------------ Threshold alerts ----------------------------

    [Fact]
    public async Task Alerts_fire_strictly_below_the_threshold_and_never_without_one()
    {
        await using var harness = await HarnessAsync();

        // Flour has no threshold (0 means "no alert"); oil alerts below 25.
        await EnterAsync(harness, MainWarehouse, FlourItem, 1m, 10.00m, "BL-1");
        await EnterAsync(harness, MainWarehouse, OilItem, 25m, 500.00m, "BL-2");

        var atThreshold = await harness.Service.GetLowStockAsync(CancellationToken.None);

        // Exactly at the threshold is NOT below it.
        Assert.Empty(atThreshold);

        await harness.Service.CreateMovementAsync(
            ConsumptionRequest(MainWarehouse, OilItem, 0.5m, "BS-1"),
            Context,
            CancellationToken.None);

        var belowThreshold = await harness.Service.GetLowStockAsync(CancellationToken.None);

        var alert = Assert.Single(belowThreshold);
        Assert.Equal(OilItem, alert.ItemCode);
        Assert.Equal(MainWarehouse, alert.WarehouseCode);
        Assert.Equal(24.5m, alert.Quantity);
        Assert.Equal(25m, alert.MinimumQuantity);

        // Flour sits at 1 with no threshold and never shows up.
        Assert.DoesNotContain(belowThreshold, row => row.ItemCode == FlourItem);
    }

    [Fact]
    public async Task A_deactivated_item_no_longer_raises_an_alert_nobody_could_act_on()
    {
        await using var harness = await HarnessAsync();

        await EnterAsync(harness, MainWarehouse, OilItem, 1m, 500.00m, "BL-1");

        Assert.Single(await harness.Service.GetLowStockAsync(CancellationToken.None));

        await harness.Service.SetItemActiveAsync(OilItem, false, Context, CancellationToken.None);

        Assert.Empty(await harness.Service.GetLowStockAsync(CancellationToken.None));
    }

    // ----------------------------- Inventory counts -----------------------------

    [Fact]
    public async Task Validating_a_count_generates_exactly_the_adjustments_that_close_the_gaps()
    {
        await using var harness = await HarnessAsync();

        // Theoretical stock: flour 100, oil 30.
        await EnterAsync(harness, MainWarehouse, FlourItem, 100m, 10.00m, "BL-1");
        await EnterAsync(harness, MainWarehouse, OilItem, 30m, 500.00m, "BL-2");

        var count = await harness.Service.CreateCountAsync(
            new CreateInventoryCountRequest(MainWarehouse, Today),
            Context,
            CancellationToken.None);

        Assert.True(count.Succeeded, count.Error);

        // Counted: flour 97 (3 short), oil 30 (matches). One adjustment expected, not two.
        var lines = await harness.Service.ReplaceCountLinesAsync(
            count.Value!.Id,
            new ReplaceInventoryCountLinesRequest(new[]
            {
                new InventoryCountLineRequest(FlourItem, 97m),
                new InventoryCountLineRequest(OilItem, 30m)
            }),
            Context,
            CancellationToken.None);

        Assert.True(lines.Succeeded, lines.Error);
        Assert.Equal(2, lines.Value!.Lines.Count);

        var validation = await harness.Service.ValidateCountAsync(
            count.Value.Id,
            Context,
            CancellationToken.None);

        Assert.True(validation.Succeeded, validation.Error);
        Assert.Equal(1, validation.Value!.AdjustmentCount);
        Assert.Equal(InventoryCountStatus.Validated, validation.Value.Count.Status);
        Assert.False(validation.Value.Count.CanEdit);
        Assert.Equal("magasinier", validation.Value.Count.ValidatedBy);

        var adjustments = await harness.DbContext.Set<StockMovement>()
            .Where(movement => movement.Kind == StockMovementKind.InventoryAdjustment)
            .ToArrayAsync();

        var adjustment = Assert.Single(adjustments);
        Assert.Equal(FlourItem, adjustment.ItemCode);
        Assert.Equal(3m, adjustment.Quantity);
        Assert.False(adjustment.AdjustmentIsIncrease);
        Assert.Equal(Today, adjustment.MovementDate);
        Assert.StartsWith("INV-20300315-", adjustment.Reference);

        // The registry now sums to the counted truth, in both directions.
        Assert.Equal(97m, await CurrentStockAsync(harness, MainWarehouse, FlourItem));
        Assert.Equal(30m, await CurrentStockAsync(harness, MainWarehouse, OilItem));
    }

    [Fact]
    public async Task A_surplus_count_generates_an_increasing_adjustment()
    {
        await using var harness = await HarnessAsync();

        await EnterAsync(harness, MainWarehouse, FlourItem, 10m, 10.00m, "BL-1");

        var count = await CreateCountWithLinesAsync(harness, new InventoryCountLineRequest(FlourItem, 12.5m));

        var validation = await harness.Service.ValidateCountAsync(count, Context, CancellationToken.None);

        Assert.True(validation.Succeeded, validation.Error);
        Assert.Equal(1, validation.Value!.AdjustmentCount);

        var adjustment = await harness.DbContext.Set<StockMovement>()
            .SingleAsync(movement => movement.Kind == StockMovementKind.InventoryAdjustment);

        Assert.Equal(2.5m, adjustment.Quantity);
        Assert.True(adjustment.AdjustmentIsIncrease);
        Assert.Equal(12.5m, await CurrentStockAsync(harness, MainWarehouse, FlourItem));
    }

    [Fact]
    public async Task A_count_that_matches_the_registry_everywhere_generates_no_adjustment_at_all()
    {
        await using var harness = await HarnessAsync();

        await EnterAsync(harness, MainWarehouse, FlourItem, 10m, 10.00m, "BL-1");

        var count = await CreateCountWithLinesAsync(harness, new InventoryCountLineRequest(FlourItem, 10m));

        var validation = await harness.Service.ValidateCountAsync(count, Context, CancellationToken.None);

        Assert.True(validation.Succeeded, validation.Error);
        Assert.Equal(0, validation.Value!.AdjustmentCount);

        Assert.Equal(0, await harness.DbContext.Set<StockMovement>()
            .CountAsync(movement => movement.Kind == StockMovementKind.InventoryAdjustment));
    }

    [Fact]
    public async Task A_validated_count_is_frozen_and_a_second_validation_generates_nothing_more()
    {
        await using var harness = await HarnessAsync();

        await EnterAsync(harness, MainWarehouse, FlourItem, 10m, 10.00m, "BL-1");

        var count = await CreateCountWithLinesAsync(harness, new InventoryCountLineRequest(FlourItem, 4m));

        await harness.Service.ValidateCountAsync(count, Context, CancellationToken.None);

        var again = await harness.Service.ValidateCountAsync(count, Context, CancellationToken.None);

        Assert.False(again.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, again.ErrorType);

        var rewrite = await harness.Service.ReplaceCountLinesAsync(
            count,
            new ReplaceInventoryCountLinesRequest(new[] { new InventoryCountLineRequest(FlourItem, 99m) }),
            Context,
            CancellationToken.None);

        Assert.False(rewrite.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, rewrite.ErrorType);

        // Exactly one adjustment ever existed, and the count still says what it said.
        Assert.Equal(1, await harness.DbContext.Set<StockMovement>()
            .CountAsync(movement => movement.Kind == StockMovementKind.InventoryAdjustment));

        var reloaded = await harness.Service.GetCountAsync(count, CancellationToken.None);
        Assert.Equal(4m, reloaded.Value!.Lines.Single().CountedQuantity);
    }

    [Fact]
    public async Task Only_one_count_can_be_open_on_a_warehouse_at_a_time()
    {
        await using var harness = await HarnessAsync();

        var first = await harness.Service.CreateCountAsync(
            new CreateInventoryCountRequest(MainWarehouse, Today),
            Context,
            CancellationToken.None);

        Assert.True(first.Succeeded, first.Error);

        var second = await harness.Service.CreateCountAsync(
            new CreateInventoryCountRequest(MainWarehouse, Today.AddDays(1)),
            Context,
            CancellationToken.None);

        Assert.False(second.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, second.ErrorType);

        // Another warehouse is unaffected: the rule is per warehouse, not global.
        var elsewhere = await harness.Service.CreateCountAsync(
            new CreateInventoryCountRequest(SecondWarehouse, Today),
            Context,
            CancellationToken.None);

        Assert.True(elsewhere.Succeeded, elsewhere.Error);
    }

    [Fact]
    public async Task A_count_line_on_an_unknown_item_is_refused()
    {
        await using var harness = await HarnessAsync();

        var count = await harness.Service.CreateCountAsync(
            new CreateInventoryCountRequest(MainWarehouse, Today),
            Context,
            CancellationToken.None);

        var lines = await harness.Service.ReplaceCountLinesAsync(
            count.Value!.Id,
            new ReplaceInventoryCountLinesRequest(new[] { new InventoryCountLineRequest("PAS-UN-ARTICLE", 1m) }),
            Context,
            CancellationToken.None);

        Assert.False(lines.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, lines.ErrorType);
    }

    [Fact]
    public async Task Replacing_the_lines_of_a_draft_count_really_replaces_them()
    {
        await using var harness = await HarnessAsync();

        var count = await harness.Service.CreateCountAsync(
            new CreateInventoryCountRequest(MainWarehouse, Today),
            Context,
            CancellationToken.None);

        await harness.Service.ReplaceCountLinesAsync(
            count.Value!.Id,
            new ReplaceInventoryCountLinesRequest(new[]
            {
                new InventoryCountLineRequest(FlourItem, 1m),
                new InventoryCountLineRequest(OilItem, 2m)
            }),
            Context,
            CancellationToken.None);

        var replaced = await harness.Service.ReplaceCountLinesAsync(
            count.Value.Id,
            new ReplaceInventoryCountLinesRequest(new[] { new InventoryCountLineRequest(OilItem, 7m) }),
            Context,
            CancellationToken.None);

        Assert.True(replaced.Succeeded, replaced.Error);

        var line = Assert.Single(replaced.Value!.Lines);
        Assert.Equal(OilItem, line.ItemCode);
        Assert.Equal(7m, line.CountedQuantity);
        Assert.Equal(1, line.LineNumber);

        Assert.Equal(1, await harness.DbContext.Set<InventoryCountLine>().CountAsync());
    }

    // --------------- Contract published to the purchasing module ----------------

    [Fact]
    public async Task A_purchase_receipt_becomes_one_entry_movement_per_line_at_the_receipt_cost()
    {
        await using var harness = await HarnessAsync();

        var result = await harness.Service.RegisterPurchaseReceiptAsync(
            new RegisterPurchaseReceiptRequest(MainWarehouse, "CMD-2030-0007", new[]
            {
                new StockEntryLine(FlourItem, 200m, 9.50m, "LOT-A", new DateOnly(2031, 1, 31)),
                new StockEntryLine(OilItem, 12m, 480.00m, null, null)
            }),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(2, result.Value!.MovementCount);

        Assert.Equal(200m, await CurrentStockAsync(harness, MainWarehouse, FlourItem));
        Assert.Equal(12m, await CurrentStockAsync(harness, MainWarehouse, OilItem));

        // The receipt cost feeds the weighted average straight away - that is what makes the
        // purchasing module's reception a stock EVENT and not just a document.
        var cost = await harness.Service.GetAverageCostAsync(FlourItem, CancellationToken.None);
        Assert.Equal(9.50m, cost.Value!.AverageUnitCost);

        var movements = await harness.DbContext.Set<StockMovement>().ToArrayAsync();
        Assert.All(movements, movement => Assert.Equal("CMD-2030-0007", movement.Reference));
        Assert.Contains(movements, movement => movement.LotNumber == "LOT-A");
    }

    [Fact]
    public async Task A_receipt_naming_an_unknown_item_is_refused_as_not_found()
    {
        await using var harness = await HarnessAsync();

        var result = await harness.Service.RegisterPurchaseReceiptAsync(
            new RegisterPurchaseReceiptRequest(MainWarehouse, "CMD-1", new[]
            {
                new StockEntryLine("PAS-UN-ARTICLE", 1m, 1.00m, null, null)
            }),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.NotFound, result.ErrorType);
        Assert.Equal(0, await harness.DbContext.Set<StockMovement>().CountAsync());
    }

    // ================================= Helpers ==================================

    private static CreateStockMovementRequest ConsumptionRequest(
        string warehouseCode,
        string itemCode,
        decimal quantity,
        string reference)
    {
        return new CreateStockMovementRequest(
            warehouseCode,
            itemCode,
            Today,
            StockMovementKind.Consumption,
            quantity,
            UnitCost: null,
            reference,
            LotNumber: null,
            ExpiryDate: null,
            Notes: null,
            AdjustmentIsIncrease: null);
    }

    private static async Task EnterAsync(
        Harness harness,
        string warehouseCode,
        string itemCode,
        decimal quantity,
        decimal unitCost,
        string reference)
    {
        var result = await harness.Service.CreateMovementAsync(
            new CreateStockMovementRequest(
                warehouseCode,
                itemCode,
                Today,
                StockMovementKind.PurchaseEntry,
                quantity,
                unitCost,
                reference,
                LotNumber: null,
                ExpiryDate: null,
                Notes: null,
                AdjustmentIsIncrease: null),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
    }

    private static async Task<Guid> CreateCountWithLinesAsync(Harness harness, params InventoryCountLineRequest[] lines)
    {
        var count = await harness.Service.CreateCountAsync(
            new CreateInventoryCountRequest(MainWarehouse, Today),
            Context,
            CancellationToken.None);

        Assert.True(count.Succeeded, count.Error);

        var replaced = await harness.Service.ReplaceCountLinesAsync(
            count.Value!.Id,
            new ReplaceInventoryCountLinesRequest(lines),
            Context,
            CancellationToken.None);

        Assert.True(replaced.Succeeded, replaced.Error);

        return count.Value.Id;
    }

    /// <summary>
    /// Re-derives the stock the way the module defines it - the sum of the registry - straight
    /// from the database, so the assertions never trust the service's own read model.
    /// </summary>
    private static async Task<decimal> CurrentStockAsync(Harness harness, string warehouseCode, string itemCode)
    {
        var movements = await harness.DbContext.Set<StockMovement>()
            .AsNoTracking()
            .Where(movement => movement.WarehouseCode == warehouseCode && movement.ItemCode == itemCode)
            .ToArrayAsync();

        return movements.Sum(movement => movement.SignedQuantity);
    }

    /// <summary>
    /// One isolated database per test: unit HTL1, warehouses MAG1 and MAG2, an item without a
    /// threshold (flour) and an item that alerts below 25 (oil).
    /// </summary>
    private static async Task<Harness> HarnessAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var dbContext = new RaqmiDbContext(
            new DbContextOptionsBuilder<RaqmiDbContext>()
                .UseSqlite(connection)
                .Options);

        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Set<HotelUnit>().Add(new HotelUnit(UnitCode, "Hotel Test", HotelUnitType.Hotel));
        dbContext.Set<Warehouse>().Add(new Warehouse(MainWarehouse, "Magasin central", UnitCode));
        dbContext.Set<Warehouse>().Add(new Warehouse(SecondWarehouse, "Magasin annexe", UnitCode));
        dbContext.Set<StockItem>().Add(new StockItem(FlourItem, "Farine T55", "kg", StockItemCategory.Alimentaire));
        dbContext.Set<StockItem>().Add(new StockItem(OilItem, "Huile 5 L", "bidon", StockItemCategory.Alimentaire, 25m));

        await dbContext.SaveChangesAsync();

        return new Harness(
            connection,
            dbContext,
            new InventoryService(dbContext, new AuditLogWriter(dbContext)));
    }

    private sealed class Harness(
        SqliteConnection connection,
        RaqmiDbContext dbContext,
        InventoryService service) : IAsyncDisposable
    {
        public RaqmiDbContext DbContext { get; } = dbContext;

        public InventoryService Service { get; } = service;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
