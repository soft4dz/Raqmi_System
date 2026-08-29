using RaqmiSystem.Domain.Billing;

namespace RaqmiSystem.Tests;

public sealed class BillingTests
{
    [Fact]
    public void Customer_constructor_normalizes_code_and_accepts_valid_algerian_identifiers()
    {
        var customer = new Customer(
            " sonatrach-dz ",
            " Sonatrach Spa ",
            CustomerType.Company,
            nif: "098765432112345",
            rc: "16/00-1234567B99",
            ai: "16012345678",
            nis: "543211234509876",
            address: "Djenane El Malik, Hydra",
            city: "Alger",
            phone: "+213 21 54 70 00",
            email: "contact@sonatrach.dz");

        Assert.Equal("SONATRACH-DZ", customer.Code);
        Assert.Equal("Sonatrach Spa", customer.Name);
        Assert.Equal("098765432112345", customer.Nif);
        Assert.True(customer.IsActive);
    }

    [Fact]
    public void Customer_rejects_nif_that_is_not_exactly_15_digits()
    {
        Assert.Throws<ArgumentException>(() =>
            new Customer("CLI-1", "Client", CustomerType.Company, nif: "12345"));

        Assert.Throws<ArgumentException>(() =>
            new Customer("CLI-1", "Client", CustomerType.Company, nif: "09876543211234A"));

        Assert.Throws<ArgumentException>(() =>
            new Customer("CLI-1", "Client", CustomerType.Company, nif: "0987654321123456"));
    }

    [Fact]
    public void Customer_fiscal_identifiers_are_optional()
    {
        var customer = new Customer("PART-1", "Particulier", CustomerType.Individual);

        Assert.Null(customer.Nif);
        Assert.Null(customer.Rc);
        Assert.Null(customer.Ai);
        Assert.Null(customer.Nis);
    }

    [Fact]
    public void Invoice_totals_are_exact_across_the_three_algerian_vat_rates()
    {
        var invoice = CreateDraft();

        invoice.ReplaceLines(new[]
        {
            // 2 nights at 12 500.00 DZD, reduced 9% hospitality rate.
            new InvoiceLine("Hebergement chambre double", 2m, 12_500.00m, 9m),
            // 3 meals at 1 850.50 DZD, standard 19% rate: VAT 1 054.785 rounds to 1 054.79.
            new InvoiceLine("Restauration", 3m, 1_850.50m, 19m),
            // Exempt line at 0%.
            new InvoiceLine("Taxe de sejour", 2m, 150.00m, 0m)
        });

        Assert.Equal(25_000.00m, invoice.Lines.Single(line => line.VatRate == 9m).LineTotalExclVat);
        Assert.Equal(2_250.00m, invoice.Lines.Single(line => line.VatRate == 9m).VatAmount);
        Assert.Equal(5_551.50m, invoice.Lines.Single(line => line.VatRate == 19m).LineTotalExclVat);
        Assert.Equal(1_054.79m, invoice.Lines.Single(line => line.VatRate == 19m).VatAmount);
        Assert.Equal(0m, invoice.Lines.Single(line => line.VatRate == 0m).VatAmount);

        Assert.Equal(30_851.50m, invoice.TotalExclVat);
        Assert.Equal(3_304.79m, invoice.TotalVat);
        Assert.Equal(34_156.29m, invoice.TotalInclVat);
    }

    [Fact]
    public void Invoice_line_rejects_non_algerian_vat_rate_and_non_positive_quantity()
    {
        Assert.Throws<ArgumentException>(() => new InvoiceLine("Ligne", 1m, 100m, 7m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InvoiceLine("Ligne", 0m, 100m, 19m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InvoiceLine("Ligne", 1m, -5m, 19m));
    }

    [Fact]
    public void Invoice_line_rejects_excess_decimal_precision()
    {
        // Quantity: at most 3 decimal places (stored as numeric(18,3)).
        Assert.Throws<ArgumentException>(() => new InvoiceLine("Ligne", 1.2345m, 100m, 19m));

        // Unit price: at most 2 decimal places (stored as numeric(18,2)).
        Assert.Throws<ArgumentException>(() => new InvoiceLine("Ligne", 1m, 100.555m, 19m));

        // Values at exactly the allowed scale remain accepted.
        var line = new InvoiceLine("Ligne", 1.375m, 100.55m, 19m);
        Assert.Equal(1.375m, line.Quantity);
        Assert.Equal(100.55m, line.UnitPrice);
    }

    [Fact]
    public void Customer_snapshot_can_only_be_captured_on_a_draft_invoice()
    {
        var invoice = CreateDraftWithOneLine();

        invoice.CaptureCustomerSnapshot(
            "Sonatrach Spa",
            "098765432112345",
            "16/00-1234567B99",
            "16012345678",
            "543211234509876",
            "Djenane El Malik, Hydra");

        invoice.Issue(2026, 1, "issuer", DateTimeOffset.UtcNow);

        Assert.Equal("Sonatrach Spa", invoice.CustomerNameSnapshot);
        Assert.Equal("098765432112345", invoice.CustomerNifSnapshot);
        Assert.Equal("Djenane El Malik, Hydra", invoice.CustomerAddressSnapshot);

        // Once issued, the frozen identification can no longer be replaced.
        Assert.Throws<InvalidOperationException>(() =>
            invoice.CaptureCustomerSnapshot("Autre Nom", null, null, null, null, null));
    }

    [Fact]
    public void Issuing_assigns_the_formatted_number_and_freezes_the_invoice()
    {
        var invoice = CreateDraftWithOneLine();

        invoice.Issue(2026, 1, "issuer", DateTimeOffset.UtcNow);

        Assert.Equal("FAC-2026-000001", invoice.Number);
        Assert.Equal(InvoiceStatus.Issued, invoice.Status);
        Assert.Equal("issuer", invoice.IssuedBy);
        Assert.False(invoice.CanEdit);

        Assert.Throws<InvalidOperationException>(() =>
            invoice.ReplaceLines(new[] { new InvoiceLine("Autre", 1m, 10m, 19m) }));

        Assert.Throws<InvalidOperationException>(() =>
            invoice.Issue(2026, 2, "issuer", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Issuing_an_invoice_without_lines_is_refused()
    {
        var invoice = CreateDraft();

        Assert.Throws<InvalidOperationException>(() =>
            invoice.Issue(2026, 1, "issuer", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Paid_is_only_reachable_from_issued()
    {
        var draft = CreateDraftWithOneLine();

        Assert.Throws<InvalidOperationException>(() =>
            draft.MarkPaid("cashier", DateTimeOffset.UtcNow));

        draft.Issue(2026, 1, "issuer", DateTimeOffset.UtcNow);
        draft.MarkPaid("cashier", DateTimeOffset.UtcNow);

        Assert.Equal(InvoiceStatus.Paid, draft.Status);
        Assert.Equal("cashier", draft.PaidBy);
    }

    [Fact]
    public void Cancellation_requires_a_reason_and_is_refused_once_paid()
    {
        var invoice = CreateDraftWithOneLine();

        Assert.Throws<ArgumentException>(() =>
            invoice.Cancel("  ", "manager", DateTimeOffset.UtcNow));

        invoice.Issue(2026, 1, "issuer", DateTimeOffset.UtcNow);
        invoice.MarkPaid("cashier", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            invoice.Cancel("Erreur de saisie", "manager", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Cancelling_an_issued_invoice_records_reason_and_actor()
    {
        var invoice = CreateDraftWithOneLine();

        invoice.Issue(2026, 1, "issuer", DateTimeOffset.UtcNow);
        invoice.Cancel("Facture en double", "manager", DateTimeOffset.UtcNow);

        Assert.Equal(InvoiceStatus.Cancelled, invoice.Status);
        Assert.Equal("Facture en double", invoice.CancellationReason);
        Assert.Equal("manager", invoice.CancelledBy);
    }

    [Fact]
    public void Replacing_lines_renumbers_them_and_recomputes_stored_totals()
    {
        var invoice = CreateDraftWithOneLine();

        invoice.ReplaceLines(new[]
        {
            new InvoiceLine("Ligne A", 1m, 100.00m, 19m),
            new InvoiceLine("Ligne B", 1m, 200.00m, 9m)
        });

        Assert.Equal(new[] { 1, 2 }, invoice.Lines.OrderBy(line => line.LineNumber).Select(line => line.LineNumber));
        Assert.Equal(300.00m, invoice.TotalExclVat);
        Assert.Equal(37.00m, invoice.TotalVat);
        Assert.Equal(337.00m, invoice.TotalInclVat);
    }

    private static Invoice CreateDraft()
    {
        return new Invoice("SONATRACH-DZ", "EL-MANAR", new DateOnly(2026, 3, 10));
    }

    private static Invoice CreateDraftWithOneLine()
    {
        var invoice = CreateDraft();
        invoice.ReplaceLines(new[] { new InvoiceLine("Hebergement", 1m, 10_000.00m, 9m) });
        return invoice;
    }
}
