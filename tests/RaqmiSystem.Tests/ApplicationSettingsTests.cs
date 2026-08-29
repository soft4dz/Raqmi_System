using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Settings;

namespace RaqmiSystem.Tests;

/// <summary>
/// Domain invariants of the global settings singleton. The establishment described here is the
/// EMITTER of every commercial document, so its fiscal identifiers are held to exactly the same
/// rules as a customer's, and the exploitation defaults may not carry a value the modules
/// reading them would refuse.
/// </summary>
public sealed class ApplicationSettingsTests
{
    [Fact]
    public void Defaults_are_usable_before_anything_is_configured()
    {
        var settings = ApplicationSettings.CreateDefault();

        Assert.Equal(ApplicationSettings.UnconfiguredCompanyName, settings.CompanyName);
        Assert.Equal("DZD", settings.CurrencyLabel);
        Assert.Equal(365, settings.AuditRetentionDays);
        Assert.Equal(19m, settings.DefaultVatRate);
        Assert.Null(settings.CompanyNif);
        Assert.Equal(ApplicationSettings.SingletonId, settings.Id);
        Assert.Equal(ApplicationSettings.SingletonKeyValue, settings.SingletonKey);
    }

    [Fact]
    public void Company_identity_normalizes_and_accepts_valid_algerian_identifiers()
    {
        var settings = new ApplicationSettings(
            " Hotel El Manar Spa ",
            companyNif: "098765432112345",
            companyRc: "16/00-1234567B99",
            companyAi: "16012345678",
            companyNis: "543211234509876",
            companyAddress: "Boulevard des Martyrs",
            companyCity: "Alger",
            companyPhone: "+213 21 00 00 00",
            companyEmail: "contact@elmanar.dz");

        Assert.Equal("Hotel El Manar Spa", settings.CompanyName);
        Assert.Equal("098765432112345", settings.CompanyNif);
        Assert.Equal("Alger", settings.CompanyCity);
    }

    [Fact]
    public void Company_name_is_required()
    {
        Assert.Throws<ArgumentException>(() => new ApplicationSettings("   "));

        var settings = ApplicationSettings.CreateDefault();

        Assert.Throws<ArgumentException>(() =>
            settings.UpdateCompanyIdentity("", null, null, null, null, null, null, null, null));
    }

    [Fact]
    public void Company_nif_follows_the_customer_rule_of_exactly_15_digits()
    {
        Assert.Throws<ArgumentException>(() => new ApplicationSettings("Hotel", companyNif: "12345"));
        Assert.Throws<ArgumentException>(() => new ApplicationSettings("Hotel", companyNif: "09876543211234A"));
        Assert.Throws<ArgumentException>(() => new ApplicationSettings("Hotel", companyNif: "0987654321123456"));

        // Exactly the rule enforced on the recipient side of the very same document.
        Assert.Equal(
            new Customer("CLI-1", "Client", CustomerType.Company, nif: "098765432112345").Nif,
            new ApplicationSettings("Hotel", companyNif: "098765432112345").CompanyNif);
    }

    [Fact]
    public void Default_vat_rate_is_restricted_to_the_rates_invoice_lines_accept()
    {
        Assert.Throws<ArgumentException>(() => new ApplicationSettings("Hotel", defaultVatRate: 7m));
        Assert.Throws<ArgumentException>(() => new ApplicationSettings("Hotel", defaultVatRate: 20m));
        Assert.Throws<ArgumentException>(() => new ApplicationSettings("Hotel", defaultVatRate: -19m));

        // Every rate a line accepts must be settable as the default, or the default would be a trap.
        Assert.All(InvoiceLine.AllowedVatRates, rate =>
        {
            var settings = new ApplicationSettings("Hotel", defaultVatRate: rate);
            Assert.Equal(rate, settings.DefaultVatRate);
            Assert.Equal(rate, new InvoiceLine("Ligne", 1m, 100m, settings.DefaultVatRate).VatRate);
        });
    }

    [Fact]
    public void Audit_retention_stays_inside_its_bounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ApplicationSettings("Hotel", auditRetentionDays: 29));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ApplicationSettings("Hotel", auditRetentionDays: 3651));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ApplicationSettings("Hotel", auditRetentionDays: 0));

        Assert.Equal(30, new ApplicationSettings("Hotel", auditRetentionDays: 30).AuditRetentionDays);
        Assert.Equal(3650, new ApplicationSettings("Hotel", auditRetentionDays: 3650).AuditRetentionDays);
    }

    [Fact]
    public void Update_operations_validates_and_falls_back_to_the_default_currency_label()
    {
        var settings = ApplicationSettings.CreateDefault();

        settings.UpdateOperations(9m, "  ", 90);

        Assert.Equal(9m, settings.DefaultVatRate);
        Assert.Equal("DZD", settings.CurrencyLabel);
        Assert.Equal(90, settings.AuditRetentionDays);

        // A refused update leaves the previous, valid configuration untouched.
        Assert.Throws<ArgumentException>(() => settings.UpdateOperations(7m, "EUR", 90));
        Assert.Equal(9m, settings.DefaultVatRate);
        Assert.Equal("DZD", settings.CurrencyLabel);
    }

    [Fact]
    public void Issuer_snapshot_can_only_be_captured_on_a_draft_invoice()
    {
        var invoice = new Invoice("SONATRACH-DZ", "EL-MANAR", new DateOnly(2026, 3, 10));
        invoice.ReplaceLines(new[] { new InvoiceLine("Hebergement", 1m, 10_000.00m, 9m) });

        invoice.CaptureIssuerSnapshot(
            "Hotel El Manar Spa",
            "098765432112345",
            "16/00-1234567B99",
            "16012345678",
            "543211234509876",
            "Boulevard des Martyrs");

        invoice.Issue(2026, 1, "issuer", DateTimeOffset.UtcNow);

        Assert.Equal("Hotel El Manar Spa", invoice.IssuerNameSnapshot);
        Assert.Equal("098765432112345", invoice.IssuerNifSnapshot);
        Assert.Equal("Boulevard des Martyrs", invoice.IssuerAddressSnapshot);

        Assert.Throws<InvalidOperationException>(() =>
            invoice.CaptureIssuerSnapshot("Autre Etablissement", null, null, null, null, null));
    }
}
