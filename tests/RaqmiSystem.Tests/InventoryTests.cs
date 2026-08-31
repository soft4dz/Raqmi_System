using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Tests;

/// <summary>
/// Pure domain coverage of the stocks module: the direction rule of the registry, the quantity
/// and cost precision rules, the two-halves-or-nothing shape of a transfer, and the lifecycle of
/// an inventory count (draft-only edits, one line per item, immutable once validated).
///
/// These tests never touch a database: they pin the invariants that must hold whatever the
/// persistence, so a later refactoring of the service cannot quietly relax them.
/// </summary>
public sealed class InventoryTests
{
    private static readonly DateOnly MovementDate = new(2030, 3, 15);

    // ------------------------------ Direction rule ------------------------------

    [Fact]
    public void Signed_quantity_carries_the_direction_of_each_movement_kind()
    {
        var entry = StockMovement.PurchaseEntry("mag-1", "art-1", MovementDate, 10m, 250.00m, "BL-1");
        var consumption = StockMovement.Consumption("mag-1", "art-1", MovementDate, 4m, "BS-1");
        var increase = StockMovement.InventoryAdjustment("mag-1", "art-1", MovementDate, 2m, isIncrease: true, "INV-1");
        var decrease = StockMovement.InventoryAdjustment("mag-1", "art-1", MovementDate, 3m, isIncrease: false, "INV-1");

        var (transferOut, transferIn) = StockMovement.CreateTransferPair(
            "mag-1", "mag-2", "art-1", MovementDate, 5m, "TR-1");

        Assert.Equal(10m, entry.SignedQuantity);
        Assert.Equal(-4m, consumption.SignedQuantity);
        Assert.Equal(2m, increase.SignedQuantity);
        Assert.Equal(-3m, decrease.SignedQuantity);
        Assert.Equal(-5m, transferOut.SignedQuantity);
        Assert.Equal(5m, transferIn.SignedQuantity);

        // The stock of the pair is the sum of the registry, nothing else: 10 - 4 + 2 - 3 - 5.
        var stock = new[] { entry, consumption, increase, decrease, transferOut }
            .Sum(movement => movement.SignedQuantity);

        Assert.Equal(0m, stock);
    }

    [Fact]
    public void Codes_are_normalized_and_quantities_are_stored_positive()
    {
        var movement = StockMovement.Consumption(" mag-1 ", " art-1 ", MovementDate, 4.125m, " BS-1 ");

        Assert.Equal("MAG-1", movement.WarehouseCode);
        Assert.Equal("ART-1", movement.ItemCode);
        Assert.Equal("BS-1", movement.Reference);
        Assert.Equal(4.125m, movement.Quantity);
        Assert.Equal(-4.125m, movement.SignedQuantity);
    }

    // --------------------------- Precision and guards ---------------------------

    [Fact]
    public void Quantity_must_be_strictly_positive_and_at_most_three_decimals()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StockMovement.Consumption("MAG-1", "ART-1", MovementDate, 0m, "BS-1"));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StockMovement.Consumption("MAG-1", "ART-1", MovementDate, -1m, "BS-1"));

        Assert.Throws<ArgumentException>(() =>
            StockMovement.Consumption("MAG-1", "ART-1", MovementDate, 1.0001m, "BS-1"));
    }

    [Fact]
    public void A_unit_cost_is_money_two_decimals_at_most_and_never_negative()
    {
        // The purchase-entry factory takes a NON-nullable cost, so "an entry without a cost"
        // is not expressible through it at all - the compiler enforces what feeds the weighted
        // average. What remains to pin here is the shape of the cost when it is present.
        Assert.Throws<ArgumentException>(() =>
            StockMovement.PurchaseEntry("MAG-1", "ART-1", MovementDate, 1m, 10.005m, "BL-1"));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StockMovement.PurchaseEntry("MAG-1", "ART-1", MovementDate, 1m, -1m, "BL-1"));

        // A consumption may carry the average cost known at the time, for traceability only.
        var consumption = StockMovement.Consumption("MAG-1", "ART-1", MovementDate, 1m, "BS-1", unitCost: 12.34m);

        Assert.Equal(12.34m, consumption.UnitCost);
    }

    [Fact]
    public void An_adjustment_direction_belongs_to_adjustments_only()
    {
        // An adjustment without a direction cannot be signed at all.
        Assert.Throws<InvalidOperationException>(() =>
            StockMovement.IsInbound(StockMovementKind.InventoryAdjustment, adjustmentIsIncrease: null));

        // ... and every other kind carries its direction in the kind itself.
        Assert.True(StockMovement.IsInbound(StockMovementKind.PurchaseEntry, null));
        Assert.True(StockMovement.IsInbound(StockMovementKind.TransferIn, null));
        Assert.False(StockMovement.IsInbound(StockMovementKind.Consumption, null));
        Assert.False(StockMovement.IsInbound(StockMovementKind.TransferOut, null));
    }

    // --------------------------------- Transfer ---------------------------------

    [Fact]
    public void A_transfer_is_two_linked_halves_of_the_same_quantity_sharing_one_group_id()
    {
        var (outMovement, inMovement) = StockMovement.CreateTransferPair(
            "MAG-1", "MAG-2", "ART-1", MovementDate, 7.5m, "TR-2030-01");

        Assert.Equal(StockMovementKind.TransferOut, outMovement.Kind);
        Assert.Equal(StockMovementKind.TransferIn, inMovement.Kind);
        Assert.Equal("MAG-1", outMovement.WarehouseCode);
        Assert.Equal("MAG-2", inMovement.WarehouseCode);
        Assert.Equal(outMovement.Quantity, inMovement.Quantity);

        Assert.NotNull(outMovement.TransferGroupId);
        Assert.Equal(outMovement.TransferGroupId, inMovement.TransferGroupId);

        // Nothing is created and nothing is destroyed by a transfer: the two halves cancel out.
        Assert.Equal(0m, outMovement.SignedQuantity + inMovement.SignedQuantity);
    }

    [Fact]
    public void A_transfer_requires_two_distinct_warehouses()
    {
        Assert.Throws<ArgumentException>(() =>
            StockMovement.CreateTransferPair("MAG-1", " mag-1 ", "ART-1", MovementDate, 1m, "TR-1"));
    }

    // ----------------------------- Items and alerts -----------------------------

    [Fact]
    public void An_item_carries_its_threshold_but_never_a_quantity()
    {
        var item = new StockItem(" far-t55 ", " Farine T55 ", " kg ", StockItemCategory.Alimentaire, 25m);

        Assert.Equal("FAR-T55", item.Code);
        Assert.Equal("Farine T55", item.Designation);
        Assert.Equal("kg", item.UnitOfMeasure);
        Assert.Equal(25m, item.MinimumQuantity);
        Assert.True(item.IsActive);

        // No quantity property exists on the item: the stock is the registry, by construction.
        Assert.Null(typeof(StockItem).GetProperty("Quantity"));
        Assert.Null(typeof(StockItem).GetProperty("CurrentStock"));
    }

    [Fact]
    public void An_item_threshold_is_never_negative_and_keeps_three_decimals_at_most()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StockItem("ART-1", "Article", "kg", StockItemCategory.Autre, -1m));

        Assert.Throws<ArgumentException>(() =>
            new StockItem("ART-1", "Article", "kg", StockItemCategory.Autre, 1.0001m));
    }

    // ----------------------------- Inventory counts -----------------------------

    [Fact]
    public void A_count_numbers_its_lines_and_refuses_the_same_item_twice()
    {
        var count = new InventoryCount(" mag-1 ", new DateOnly(2030, 6, 30));

        Assert.Equal("MAG-1", count.WarehouseCode);
        Assert.Equal(InventoryCountStatus.Draft, count.Status);
        Assert.True(count.CanEdit);

        count.ReplaceLines(new[]
        {
            new InventoryCountLine("ART-1", 12m),
            new InventoryCountLine("ART-2", 0m)
        });

        Assert.Equal(new[] { 1, 2 }, count.Lines.Select(line => line.LineNumber).ToArray());

        // Zero is a meaningful count: "nothing left on the shelf".
        Assert.Equal(0m, count.Lines.Single(line => line.ItemCode == "ART-2").CountedQuantity);

        Assert.Throws<ArgumentException>(() => count.ReplaceLines(new[]
        {
            new InventoryCountLine("ART-1", 12m),
            new InventoryCountLine(" art-1 ", 13m)
        }));
    }

    [Fact]
    public void A_counted_quantity_is_never_negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryCountLine("ART-1", -1m));
        Assert.Throws<ArgumentException>(() => new InventoryCountLine("ART-1", 1.0001m));
    }

    [Fact]
    public void An_empty_count_cannot_be_validated()
    {
        var count = new InventoryCount("MAG-1", new DateOnly(2030, 6, 30));

        Assert.Throws<InvalidOperationException>(() => count.Validate("controleur", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void A_validated_count_is_immutable_and_cannot_be_validated_twice()
    {
        var count = new InventoryCount("MAG-1", new DateOnly(2030, 6, 30));
        count.ReplaceLines(new[] { new InventoryCountLine("ART-1", 12m) });

        var validatedAt = new DateTimeOffset(2030, 6, 30, 18, 0, 0, TimeSpan.Zero);
        count.Validate(" controleur ", validatedAt);

        Assert.Equal(InventoryCountStatus.Validated, count.Status);
        Assert.False(count.CanEdit);
        Assert.Equal(validatedAt, count.ValidatedAt);
        Assert.Equal("controleur", count.ValidatedBy);

        // It is the documentary proof behind the adjustments it generated: rewriting it would
        // orphan them.
        Assert.Throws<InvalidOperationException>(() =>
            count.ReplaceLines(new[] { new InventoryCountLine("ART-1", 99m) }));

        Assert.Throws<InvalidOperationException>(() =>
            count.Validate("controleur", DateTimeOffset.UtcNow));
    }

}
