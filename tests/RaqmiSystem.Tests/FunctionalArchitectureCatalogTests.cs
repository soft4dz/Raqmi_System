using RaqmiSystem.Application.Navigation;

namespace RaqmiSystem.Tests;

public sealed class FunctionalArchitectureCatalogTests
{
    [Fact]
    public void Catalog_contains_the_22_stable_domains_in_order()
    {
        Assert.Equal(FunctionalArchitectureCatalog.ExpectedDomainCount, FunctionalArchitectureCatalog.Domains.Count);
        Assert.Equal(
            Enumerable.Range(1, 22).Select(number => number.ToString("00")),
            FunctionalArchitectureCatalog.Domains.Select(domain => domain.Id));
    }

    [Fact]
    public void Domain_ids_names_and_legacy_orders_are_unique()
    {
        var domains = FunctionalArchitectureCatalog.Domains;
        var legacyOrders = domains.SelectMany(domain => domain.LegacyModuleOrders).ToArray();

        Assert.Equal(domains.Count, domains.Select(domain => domain.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(domains.Count, domains.Select(domain => domain.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(legacyOrders.Length, legacyOrders.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Every_historical_module_has_exactly_one_primary_domain()
    {
        string[] expectedOrders =
        [
            "1", "2", "3", "4", "4.5", "5", "5.2", "5.4", "6", "8", "9", "9.2",
            "10", "10.1", "10.2", "10.4", "10.6", "11", "11.5", "11.6", "12", "12.5",
            "13", "13.5", "14.5", "18", "20", "20.2", "21", "21.2", "22", "22.2",
            "22.4", "22.6", "22.8", "23", "23.2", "23.4", "23.6", "24", "24.2",
            "24.4", "25", "25.2", "25.4", "26", "27", "28", "29", "30"
        ];

        Assert.Equal(FunctionalArchitectureCatalog.ExpectedLegacyModuleCount, expectedOrders.Length);

        foreach (var order in expectedOrders)
        {
            Assert.True(FunctionalArchitectureCatalog.TryGetDomainForLegacyOrder(order, out var domain));
            Assert.NotNull(domain);
        }
    }

    [Theory]
    [InlineData("5.2", "03")]
    [InlineData("10", "06")]
    [InlineData("22.2", "01")]
    [InlineData("11", "11")]
    [InlineData("28", "22")]
    public void Critical_legacy_modules_keep_the_approved_primary_mapping(string order, string domainId)
    {
        Assert.Equal(domainId, FunctionalArchitectureCatalog.DomainForLegacyOrder(order).Id);
    }

    [Fact]
    public void Parking_domain_exists_without_claiming_a_historical_module()
    {
        var parking = Assert.Single(FunctionalArchitectureCatalog.Domains, domain => domain.Id == "19");

        Assert.Equal(FunctionalMaturity.Planned, parking.Maturity);
        Assert.Empty(parking.LegacyModuleOrders);
    }
}
