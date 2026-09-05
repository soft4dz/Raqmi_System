using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Documents;
using RaqmiSystem.Domain.Billing;

namespace RaqmiSystem.Tests;

/// <summary>
/// Le constructeur du modele de facture est une fonction pure : ces tests fixent la regle « aucun
/// montant n'est recalcule » et les provenances (instantane de l'emission pour l'emetteur, fiche
/// courante pour l'adresse du client), sans base ni HTTP.
/// </summary>
public sealed class DocumentsInvoiceModelTests
{
    [Fact]
    public void Build_copies_the_invoice_totals_verbatim_even_when_they_disagree_with_the_lines()
    {
        // Totaux volontairement incoherents avec les lignes : si le constructeur recalculait quoi
        // que ce soit, le modele porterait la somme des lignes et non ces valeurs.
        var invoice = DocumentsTestData.IssuedInvoice(
            totalExclVat: 999.99m,
            totalVat: 88.88m,
            totalInclVat: 1_088.87m);

        var model = InvoiceDocumentModelBuilder.Build(invoice, DocumentsTestData.CompanyCustomer(), DocumentsTestData.Settings());

        Assert.Equal(999.99m, model.TotalExclVat);
        Assert.Equal(88.88m, model.TotalVat);
        Assert.Equal(1_088.87m, model.TotalInclVat);
        Assert.NotEqual(model.Lines.Sum(line => line.LineTotalExclVat), model.TotalExclVat);
    }

    [Fact]
    public void Build_summarises_vat_per_rate_from_the_line_amounts_served_by_the_server()
    {
        var invoice = DocumentsTestData.IssuedInvoice();

        var model = InvoiceDocumentModelBuilder.Build(invoice, DocumentsTestData.CompanyCustomer(), DocumentsTestData.Settings());

        Assert.Equal([0m, 9m, 19m], model.VatSummary.Select(row => row.VatRate).ToArray());

        foreach (var row in model.VatSummary)
        {
            var linesAtRate = invoice.Lines.Where(line => line.VatRate == row.VatRate).ToArray();
            Assert.Equal(linesAtRate.Sum(line => line.LineTotalExclVat), row.BaseExclVat);
            Assert.Equal(linesAtRate.Sum(line => line.VatAmount), row.VatAmount);
        }

        // Sur une facture coherente, le recapitulatif retombe sur les totaux de la facture.
        Assert.Equal(invoice.TotalExclVat, model.VatSummary.Sum(row => row.BaseExclVat));
        Assert.Equal(invoice.TotalVat, model.VatSummary.Sum(row => row.VatAmount));
    }

    [Fact]
    public void Build_copies_every_line_amount_and_orders_lines_by_number()
    {
        var shuffled = DocumentsTestData.ThreeVatRateLines().Reverse().ToArray();
        var invoice = DocumentsTestData.IssuedInvoice(lines: shuffled);

        var model = InvoiceDocumentModelBuilder.Build(invoice, DocumentsTestData.CompanyCustomer(), DocumentsTestData.Settings());

        Assert.Equal([1, 2, 3], model.Lines.Select(line => line.LineNumber).ToArray());

        foreach (var line in model.Lines)
        {
            var source = invoice.Lines.Single(candidate => candidate.LineNumber == line.LineNumber);
            Assert.Equal(source.Designation, line.Designation);
            Assert.Equal(source.Quantity, line.Quantity);
            Assert.Equal(source.UnitPrice, line.UnitPrice);
            Assert.Equal(source.VatRate, line.VatRate);
            Assert.Equal(source.LineTotalExclVat, line.LineTotalExclVat);
        }
    }

    [Fact]
    public void Build_takes_the_issuer_identity_from_the_invoice_snapshot_not_from_the_current_settings()
    {
        var invoice = DocumentsTestData.IssuedInvoice();

        // Le parametrage a change depuis l'emission : le document doit ignorer le nouveau NIF.
        var settings = DocumentsTestData.Settings(companyNif: "000000000000000");

        var model = InvoiceDocumentModelBuilder.Build(invoice, DocumentsTestData.CompanyCustomer(), settings);

        Assert.Equal(invoice.IssuerName, model.Issuer.Name);
        Assert.Equal(DocumentsTestData.IssuerNif, model.Issuer.Nif);
        Assert.Equal(invoice.IssuerRc, model.Issuer.Rc);
        Assert.Equal(invoice.IssuerAi, model.Issuer.Ai);
        Assert.Equal(invoice.IssuerNis, model.Issuer.Nis);
        Assert.Equal(invoice.IssuerAddress, model.Issuer.Address);

        // Ville et coordonnees ne font pas partie de l'instantane : elles viennent du parametrage.
        Assert.Equal(settings.CompanyCity, model.Issuer.City);
        Assert.Equal(settings.CompanyPhone, model.Issuer.Phone);
        Assert.Equal(settings.CurrencyLabel, model.CurrencyLabel);
    }

    [Fact]
    public void Build_prints_the_customer_name_frozen_on_the_invoice_and_the_fiscal_identifiers_of_a_company()
    {
        var invoice = DocumentsTestData.IssuedInvoice(customerName: "Sonatrach Spa (nom fige)");
        var customer = DocumentsTestData.CompanyCustomer();

        var model = InvoiceDocumentModelBuilder.Build(invoice, customer, DocumentsTestData.Settings());

        Assert.Equal("Sonatrach Spa (nom fige)", model.Customer.Name);
        Assert.Equal(customer.Address, model.Customer.Address);
        Assert.Equal(customer.City, model.Customer.City);
        Assert.Equal(customer.Nif, model.Customer.Nif);
        Assert.Equal(customer.Rc, model.Customer.Rc);
        Assert.Equal(customer.Ai, model.Customer.Ai);
        Assert.Equal(customer.Nis, model.Customer.Nis);
    }

    [Fact]
    public void Build_hides_the_fiscal_identifiers_of_an_individual_customer()
    {
        var invoice = DocumentsTestData.IssuedInvoice(customerName: "Karim Benali");

        var model = InvoiceDocumentModelBuilder.Build(invoice, DocumentsTestData.IndividualCustomer(), DocumentsTestData.Settings());

        Assert.Equal("Karim Benali", model.Customer.Name);
        Assert.Equal("12 rue Didouche Mourad", model.Customer.Address);
        Assert.Null(model.Customer.Nif);
        Assert.Null(model.Customer.Rc);
        Assert.Null(model.Customer.Ai);
        Assert.Null(model.Customer.Nis);
    }

    [Fact]
    public void Build_survives_a_missing_customer_record_by_keeping_the_frozen_name()
    {
        var invoice = DocumentsTestData.IssuedInvoice(customerName: "Client disparu Sarl");

        var model = InvoiceDocumentModelBuilder.Build(invoice, customer: null, DocumentsTestData.Settings());

        Assert.Equal("Client disparu Sarl", model.Customer.Name);
        Assert.Null(model.Customer.Address);
    }

    [Fact]
    public void Build_refuses_a_draft_because_it_has_no_legal_document()
    {
        var draft = DocumentsTestData.IssuedInvoice(number: null, status: InvoiceStatus.Draft);

        Assert.Throws<ArgumentException>(() =>
            InvoiceDocumentModelBuilder.Build(draft, DocumentsTestData.CompanyCustomer(), DocumentsTestData.Settings()));
    }

    [Fact]
    public void Build_carries_the_documented_footer_mentions_until_settings_provide_them()
    {
        var model = InvoiceDocumentModelBuilder.Build(
            DocumentsTestData.IssuedInvoice(),
            DocumentsTestData.CompanyCustomer(),
            DocumentsTestData.Settings());

        Assert.Equal(InvoiceDocumentDefaults.PaymentTermsMention, model.PaymentTerms);
        Assert.Equal(InvoiceDocumentDefaults.FooterMention, model.FooterMentions);
        Assert.Contains("expert-comptable", model.FooterMentions, StringComparison.Ordinal);
    }
}
