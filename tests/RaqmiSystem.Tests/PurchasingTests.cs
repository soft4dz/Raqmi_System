using RaqmiSystem.Domain.Purchasing;

namespace RaqmiSystem.Tests;

/// <summary>
/// Pure domain coverage of the purchasing aggregates (no database, no service): the supplier
/// referential and its shared Algerian NIF rule, the numbering discipline of a purchase order
/// (allocated at APPROVAL, never before), the freezing of the lines that approval imposes, the
/// cumulative arithmetic of multiple partial receipts with the status that follows from it, the
/// refusal of any over-receipt, and the point of no return that a first delivery sets on
/// cancellation.
/// </summary>
public sealed class PurchasingTests
{
    private static readonly DateOnly OrderDate = new(2026, 4, 12);

    [Fact]
    public void Supplier_normalizes_its_code_and_accepts_valid_algerian_identifiers()
    {
        var supplier = new Supplier(
            " sarl-medina ",
            " SARL Medina Distribution ",
            SupplierType.Company,
            nif: "098765432112345",
            rc: "16/00-1234567B99",
            ai: "16012345678",
            nis: "543211234509876",
            address: "Zone industrielle, Rouiba",
            city: "Alger",
            phone: "+213 21 85 00 00",
            email: "contact@medina.dz");

        Assert.Equal("SARL-MEDINA", supplier.Code);
        Assert.Equal("SARL Medina Distribution", supplier.Name);
        Assert.Equal("098765432112345", supplier.Nif);
        Assert.True(supplier.IsActive);
    }

    /// <summary>
    /// The NIF rule has ONE owner in this codebase (Customer.NormalizeNif): a supplier is held
    /// to exactly the same 15-digit format as a customer or as the establishment itself. This
    /// test pins that the supplier really delegates instead of restating a divergent rule.
    /// </summary>
    [Fact]
    public void Supplier_nif_obeys_the_same_shared_fifteen_digit_rule_as_a_customer()
    {
        Assert.Throws<ArgumentException>(() =>
            new Supplier("FRN-1", "Fournisseur", SupplierType.Company, nif: "12345"));

        Assert.Throws<ArgumentException>(() =>
            new Supplier("FRN-1", "Fournisseur", SupplierType.Company, nif: "09876543211234A"));

        Assert.Throws<ArgumentException>(() =>
            new Supplier("FRN-1", "Fournisseur", SupplierType.Company, nif: "0987654321123456"));

        // Fiscal identifiers stay optional - an individual supplier carries none.
        var individual = new Supplier("FRN-2", "Artisan", SupplierType.Individual);

        Assert.Null(individual.Nif);
        Assert.Null(individual.Rc);
        Assert.Null(individual.Ai);
        Assert.Null(individual.Nis);
    }

    [Fact]
    public void Draft_purchase_order_carries_no_number_and_totals_its_lines()
    {
        var order = CreateDraft();

        order.ReplaceLines(
        [
            new PurchaseOrderLine("SUC-01", "Sucre cristallise 50 kg", 20m, 4_500.00m),
            new PurchaseOrderLine("HUI-02", "Huile de table 5 L", 12m, 1_250.50m)
        ]);

        Assert.Null(order.Number);
        Assert.Null(order.ApprovedYear);
        Assert.Null(order.ApprovedSequence);
        Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
        Assert.True(order.CanEdit);
        Assert.False(order.CanReceive);

        // 20 x 4 500.00 = 90 000.00 ; 12 x 1 250.50 = 15 006.00
        Assert.Equal(90_000.00m, order.Lines.Single(line => line.ItemCode == "SUC-01").LineTotalExclVat);
        Assert.Equal(15_006.00m, order.Lines.Single(line => line.ItemCode == "HUI-02").LineTotalExclVat);
        Assert.Equal(105_006.00m, order.TotalExclVat);

        // Lines are numbered in capture order, from 1.
        Assert.Equal(
            new[] { 1, 2 },
            order.Lines.Select(line => line.LineNumber).OrderBy(number => number).ToArray());
    }

    [Fact]
    public void Number_is_allocated_at_approval_and_follows_the_approval_year()
    {
        var order = CreateDraftWithOneLine();

        order.Approve(2026, 7, "acheteur", DateTimeOffset.UtcNow);

        Assert.Equal("BC-2026-000007", order.Number);
        Assert.Equal(2026, order.ApprovedYear);
        Assert.Equal(7, order.ApprovedSequence);
        Assert.Equal(PurchaseOrderStatus.Approved, order.Status);
        Assert.Equal("acheteur", order.ApprovedBy);
        Assert.True(order.CanReceive);
        Assert.False(order.CanEdit);
    }

    [Fact]
    public void An_order_without_lines_cannot_be_approved()
    {
        var order = CreateDraft();

        Assert.Throws<InvalidOperationException>(() =>
            order.Approve(2026, 1, "acheteur", DateTimeOffset.UtcNow));

        // The refusal leaves a pristine draft: no number was burnt.
        Assert.Null(order.Number);
        Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
    }

    [Fact]
    public void Lines_are_frozen_once_the_order_is_approved()
    {
        var order = CreateDraftWithOneLine();

        order.Approve(2026, 1, "acheteur", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            order.ReplaceLines([new PurchaseOrderLine("AUT-01", "Autre article", 1m, 10.00m)]));

        Assert.Throws<InvalidOperationException>(() =>
            order.UpdateHeader("FRN-AUTRE", "DEP-2", OrderDate));

        // The original line survived both attempts untouched.
        var line = Assert.Single(order.Lines);
        Assert.Equal("SUC-01", line.ItemCode);
        Assert.Equal(20m, line.Quantity);
    }

    [Fact]
    public void Approval_number_can_be_reassigned_only_after_a_collision_on_an_approved_order()
    {
        var order = CreateDraftWithOneLine();

        // Before approval there is nothing to renumber.
        Assert.Throws<InvalidOperationException>(() => order.ReassignApprovalNumber(2026, 2));

        order.Approve(2026, 1, "acheteur", DateTimeOffset.UtcNow);
        order.ReassignApprovalNumber(2026, 2);

        Assert.Equal("BC-2026-000002", order.Number);
        Assert.Equal(2, order.ApprovedSequence);
    }

    [Fact]
    public void Successive_partial_receipts_cumulate_and_drive_the_status()
    {
        var order = CreateApprovedWithTwoLines();

        var sugar = order.Lines.Single(line => line.ItemCode == "SUC-01");
        var oil = order.Lines.Single(line => line.ItemCode == "HUI-02");

        // First delivery: 8 of the 20 sugar bags, nothing else.
        order.RegisterReceipt(new Dictionary<Guid, decimal> { [sugar.Id] = 8m });

        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);
        Assert.Equal(8m, sugar.QuantityReceived);
        Assert.Equal(12m, sugar.RemainingQuantity);
        Assert.Equal(0m, oil.QuantityReceived);
        Assert.True(order.HasAnyReceipt);
        Assert.True(order.CanReceive);

        // Second delivery: the remaining 12 bags and 5 of the 12 oil cans.
        order.RegisterReceipt(new Dictionary<Guid, decimal> { [sugar.Id] = 12m, [oil.Id] = 5m });

        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);
        Assert.Equal(20m, sugar.QuantityReceived);
        Assert.True(sugar.IsFullyReceived);
        Assert.Equal(5m, oil.QuantityReceived);
        Assert.Equal(7m, oil.RemainingQuantity);

        // Third delivery completes the order: the status turns to Received on its own.
        order.RegisterReceipt(new Dictionary<Guid, decimal> { [oil.Id] = 7m });

        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
        Assert.Equal(12m, oil.QuantityReceived);
        Assert.All(order.Lines, line => Assert.True(line.IsFullyReceived));
        Assert.False(order.CanReceive);
    }

    [Fact]
    public void Over_receiving_a_line_is_refused_and_leaves_every_line_untouched()
    {
        var order = CreateApprovedWithTwoLines();

        var sugar = order.Lines.Single(line => line.ItemCode == "SUC-01");
        var oil = order.Lines.Single(line => line.ItemCode == "HUI-02");

        order.RegisterReceipt(new Dictionary<Guid, decimal> { [sugar.Id] = 15m });

        // 6 more bags exceed the 5 that remain: the WHOLE receipt is refused, including the
        // perfectly valid oil quantity submitted in the same delivery.
        Assert.Throws<InvalidOperationException>(() =>
            order.RegisterReceipt(new Dictionary<Guid, decimal> { [sugar.Id] = 6m, [oil.Id] = 4m }));

        Assert.Equal(15m, sugar.QuantityReceived);
        Assert.Equal(0m, oil.QuantityReceived);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);
    }

    [Fact]
    public void A_receipt_is_refused_on_an_unknown_line_a_non_positive_quantity_or_a_draft()
    {
        var draft = CreateDraftWithOneLine();

        Assert.Throws<InvalidOperationException>(() =>
            draft.RegisterReceipt(new Dictionary<Guid, decimal> { [Guid.NewGuid()] = 1m }));

        var order = CreateApprovedWithTwoLines();
        var sugar = order.Lines.Single(line => line.ItemCode == "SUC-01");

        Assert.Throws<ArgumentException>(() =>
            order.RegisterReceipt(new Dictionary<Guid, decimal> { [Guid.NewGuid()] = 1m }));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            order.RegisterReceipt(new Dictionary<Guid, decimal> { [sugar.Id] = 0m }));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            order.RegisterReceipt(new Dictionary<Guid, decimal> { [sugar.Id] = -3m }));

        // Quantities carry at most 3 decimals (numeric(18,3)).
        Assert.Throws<ArgumentException>(() =>
            order.RegisterReceipt(new Dictionary<Guid, decimal> { [sugar.Id] = 1.2345m }));

        Assert.Throws<ArgumentException>(() => order.RegisterReceipt(new Dictionary<Guid, decimal>()));

        Assert.False(order.HasAnyReceipt);
    }

    [Fact]
    public void Cancellation_requires_a_reason_and_is_refused_once_anything_has_been_received()
    {
        var order = CreateApprovedWithTwoLines();

        Assert.Throws<ArgumentException>(() => order.Cancel("   ", "acheteur", DateTimeOffset.UtcNow));
        Assert.Equal(PurchaseOrderStatus.Approved, order.Status);

        var sugar = order.Lines.Single(line => line.ItemCode == "SUC-01");
        order.RegisterReceipt(new Dictionary<Guid, decimal> { [sugar.Id] = 1m });

        // A single unit in stock makes the order the supporting document of real stock
        // movements: voiding it would leave those movements without one.
        Assert.Throws<InvalidOperationException>(() =>
            order.Cancel("Fournisseur defaillant", "acheteur", DateTimeOffset.UtcNow));

        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);
        Assert.Null(order.CancelledAt);
        Assert.Null(order.CancellationReason);
    }

    [Fact]
    public void An_approved_order_without_any_receipt_can_still_be_cancelled_with_a_reason()
    {
        var order = CreateApprovedWithTwoLines();
        var now = DateTimeOffset.UtcNow;

        order.Cancel("Rupture chez le fournisseur", "acheteur", now);

        Assert.Equal(PurchaseOrderStatus.Cancelled, order.Status);
        Assert.Equal("Rupture chez le fournisseur", order.CancellationReason);
        Assert.Equal("acheteur", order.CancelledBy);
        Assert.Equal(now, order.CancelledAt);

        // Cancelled is terminal for both the cancellation and the reception.
        Assert.Throws<InvalidOperationException>(() => order.Cancel("Encore", "acheteur", now));
        Assert.False(order.CanReceive);
    }

    [Fact]
    public void Purchase_order_line_rejects_invalid_values_and_excess_precision()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PurchaseOrderLine("ART-1", "Article", 0m, 100m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PurchaseOrderLine("ART-1", "Article", -1m, 100m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PurchaseOrderLine("ART-1", "Article", 1m, -5m));
        Assert.Throws<ArgumentException>(() => new PurchaseOrderLine("   ", "Article", 1m, 100m));
        Assert.Throws<ArgumentException>(() => new PurchaseOrderLine("ART-1", "   ", 1m, 100m));

        // Quantity: at most 3 decimal places; unit price: at most 2 (numeric(18,3) / (18,2)).
        Assert.Throws<ArgumentException>(() => new PurchaseOrderLine("ART-1", "Article", 1.2345m, 100m));
        Assert.Throws<ArgumentException>(() => new PurchaseOrderLine("ART-1", "Article", 1m, 100.555m));

        // A free line (unit price 0) stays legitimate: a supplier gift is still received.
        var free = new PurchaseOrderLine("art-1", "Article offert", 1.375m, 0m);

        Assert.Equal("ART-1", free.ItemCode);
        Assert.Equal(0m, free.LineTotalExclVat);
    }

    [Fact]
    public void Number_format_is_the_padded_year_sequence_pair()
    {
        Assert.Equal("BC-2026-000001", PurchaseOrder.FormatNumber(2026, 1));
        Assert.Equal("BC-2026-123456", PurchaseOrder.FormatNumber(2026, 123456));

        var order = CreateDraftWithOneLine();

        Assert.Throws<ArgumentOutOfRangeException>(() => order.Approve(1999, 1, "acheteur", DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => order.Approve(2026, 0, "acheteur", DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => order.Approve(2026, 1_000_000, "acheteur", DateTimeOffset.UtcNow));
    }

    private static PurchaseOrder CreateDraft()
    {
        return new PurchaseOrder("frn-medina", "dep-central", OrderDate);
    }

    private static PurchaseOrder CreateDraftWithOneLine()
    {
        var order = CreateDraft();
        order.ReplaceLines([new PurchaseOrderLine("SUC-01", "Sucre cristallise 50 kg", 20m, 4_500.00m)]);
        return order;
    }

    private static PurchaseOrder CreateApprovedWithTwoLines()
    {
        var order = CreateDraft();

        order.ReplaceLines(
        [
            new PurchaseOrderLine("SUC-01", "Sucre cristallise 50 kg", 20m, 4_500.00m),
            new PurchaseOrderLine("HUI-02", "Huile de table 5 L", 12m, 1_250.50m)
        ]);

        order.Approve(2026, 1, "acheteur", DateTimeOffset.UtcNow);

        return order;
    }
}
