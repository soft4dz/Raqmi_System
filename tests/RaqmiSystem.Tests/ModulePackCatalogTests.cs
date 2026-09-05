using RaqmiSystem.Application.Navigation;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Kpi;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Tests;

/// <summary>
/// Le manifeste des paquets : couverture complète de la taxonomie (22 domaines, 50 entrées
/// historiques, aucun orphelin), paquets actifs par secteur, et le filtre de licence branché
/// sur le point d'extension de <see cref="NavigationTreeBuilder"/> sans le modifier.
/// </summary>
public sealed class ModulePackCatalogTests
{
    private static readonly IReadOnlySet<string> AllPermissions =
        PermissionCatalog.All.Select(permission => permission.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly ModulePack[] VerticalPacks = [ModulePack.Hospitality, ModulePack.FoodBeverage, ModulePack.Events];

    // ------------------------------------------------------------------ fiches

    [Fact]
    public void Every_pack_has_exactly_one_definition_and_three_can_never_be_switched_off()
    {
        Assert.Equal(Enum.GetValues<ModulePack>().Length, ModulePackCatalog.Definitions.Count);
        Assert.Equal(
            Enum.GetValues<ModulePack>().OrderBy(pack => pack),
            ModulePackCatalog.Definitions.Select(definition => definition.Pack).OrderBy(pack => pack));

        Assert.All(ModulePackCatalog.Definitions, definition =>
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Label));
            Assert.False(string.IsNullOrWhiteSpace(definition.Description));
        });

        Assert.Equal(
            new HashSet<ModulePack> { ModulePack.Core, ModulePack.Pilotage, ModulePack.System },
            ModulePackCatalog.AlwaysActivePacks);
    }

    // ------------------------------------------------------------- couverture

    [Fact]
    public void Each_of_the_22_domains_belongs_to_exactly_one_pack()
    {
        Assert.Equal(FunctionalArchitectureCatalog.ExpectedDomainCount, FunctionalArchitectureCatalog.Domains.Count);

        Assert.All(FunctionalArchitectureCatalog.Domains, domain =>
        {
            var pack = ModulePackCatalog.PackForDomain(domain.Id);
            Assert.True(Enum.IsDefined(pack), domain.Id);
        });

        Assert.Throws<KeyNotFoundException>(() => ModulePackCatalog.PackForDomain("99"));
    }

    [Fact]
    public void Each_of_the_50_historical_entries_belongs_to_exactly_one_pack()
    {
        var orders = FunctionalArchitectureCatalog.Domains.SelectMany(domain => domain.LegacyModuleOrders).ToArray();

        Assert.Equal(FunctionalArchitectureCatalog.ExpectedLegacyModuleCount, orders.Length);
        Assert.Equal(orders.Length, orders.Distinct(StringComparer.Ordinal).Count());

        Assert.All(orders, order =>
        {
            Assert.True(ModulePackCatalog.TryGetPackForLegacyOrder(order, out var pack), order);
            Assert.Equal(pack, ModulePackCatalog.PackForLegacyOrder(order));
        });

        Assert.False(ModulePackCatalog.TryGetPackForLegacyOrder("7", out _));
        Assert.Throws<KeyNotFoundException>(() => ModulePackCatalog.PackForLegacyOrder("7"));
    }

    /// <summary>
    /// Une entrée suit le paquet de son domaine, sauf exception nommée. La seule aujourd'hui :
    /// la conformité hôtelière (23), logée sous Juridique &amp; Conformité mais sans objet hors
    /// hôtellerie. Une exception ajoutée sans être déclarée ici est un rattachement à discuter.
    /// </summary>
    [Fact]
    public void An_entry_follows_its_domain_unless_the_exception_is_declared()
    {
        string[] declaredExceptions = ["23"];

        foreach (var domain in FunctionalArchitectureCatalog.Domains)
        {
            foreach (var order in domain.LegacyModuleOrders)
            {
                var expected = declaredExceptions.Contains(order)
                    ? ModulePack.Hospitality
                    : ModulePackCatalog.PackForDomain(domain.Id);

                Assert.Equal(expected, ModulePackCatalog.PackForLegacyOrder(order));
            }
        }

        Assert.Equal(ModulePack.Core, ModulePackCatalog.PackForDomain("16"));
        Assert.Equal(ModulePack.Hospitality, ModulePackCatalog.PackForLegacyOrder("23"));
    }

    [Theory]
    [InlineData("06", ModulePack.Hospitality)]
    [InlineData("08", ModulePack.Hospitality)]
    [InlineData("09", ModulePack.Events)]
    [InlineData("10", ModulePack.FoodBeverage)]
    [InlineData("03", ModulePack.Finance)]
    [InlineData("13", ModulePack.HumanResources)]
    [InlineData("20", ModulePack.Pilotage)]
    [InlineData("22", ModulePack.System)]
    public void Domains_land_where_a_reader_of_the_taxonomy_expects(string domainId, ModulePack expected)
    {
        Assert.Equal(expected, ModulePackCatalog.PackForDomain(domainId));
    }

    // ---------------------------------------------------------------- secteurs

    [Fact]
    public void Hospitality_activates_every_pack_and_retail_the_generic_ones_only()
    {
        Assert.Equal(Enum.GetValues<ModulePack>().ToHashSet(), ModulePackCatalog.ActivePacksFor(BusinessSector.Hospitality));

        Assert.Equal(
            new HashSet<ModulePack>
            {
                ModulePack.Core, ModulePack.Finance, ModulePack.Sales, ModulePack.Purchasing,
                ModulePack.Inventory, ModulePack.Crm, ModulePack.Pilotage, ModulePack.System
            },
            ModulePackCatalog.ActivePacksFor(BusinessSector.Retail));
    }

    [Theory]
    [InlineData(BusinessSector.Hospitality)]
    [InlineData(BusinessSector.Retail)]
    [InlineData(BusinessSector.Services)]
    [InlineData(BusinessSector.Manufacturing)]
    [InlineData(BusinessSector.Education)]
    [InlineData(BusinessSector.Health)]
    [InlineData(BusinessSector.Other)]
    public void Every_sector_keeps_the_always_active_packs_and_only_hospitality_gets_the_verticals(BusinessSector sector)
    {
        var packs = ModulePackCatalog.ActivePacksFor(sector);

        Assert.True(ModulePackCatalog.AlwaysActivePacks.IsSubsetOf(packs));
        Assert.Contains(ModulePack.Finance, packs);

        if (sector == BusinessSector.Hospitality)
        {
            Assert.True(VerticalPacks.All(packs.Contains));
        }
        else
        {
            Assert.DoesNotContain(packs, VerticalPacks.Contains);
        }
    }

    [Fact]
    public void An_unknown_sector_is_refused_rather_than_given_nothing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ModulePackCatalog.ActivePacksFor((BusinessSector)42));
    }

    /// <summary>
    /// Le manifeste et le catalogue KPI parlent la même langue : les paquets d'un commerce ne
    /// font remonter aucun indicateur de chambre, et ceux d'un hôtel font remonter tout.
    /// </summary>
    [Fact]
    public void The_kpi_library_of_a_retail_installation_never_mentions_a_room()
    {
        var retail = KpiCatalog.ForPacks(ModulePackCatalog.ActivePacksFor(BusinessSector.Retail));

        Assert.NotEmpty(retail);
        Assert.DoesNotContain(retail, definition => definition.Category == KpiCategory.Accommodation);
        Assert.DoesNotContain(retail, definition => definition.RequiredPermissions.Contains(PermissionCatalog.LodgingRead));

        Assert.Equal(KpiCatalog.All, KpiCatalog.ForPacks(ModulePackCatalog.ActivePacksFor(BusinessSector.Hospitality)));
    }

    // ----------------------------------------------------------------- licence

    [Fact]
    public void License_features_round_trip_and_ignore_what_is_not_a_pack()
    {
        Assert.Equal("pack:hospitality", ModulePackCatalog.LicenseFeatureFor(ModulePack.Hospitality));
        Assert.Equal("pack:finance", ModulePackCatalog.LicenseFeatureForDomain("03"));

        Assert.All(Enum.GetValues<ModulePack>(), pack =>
        {
            Assert.True(ModulePackCatalog.TryParseLicenseFeature(ModulePackCatalog.LicenseFeatureFor(pack), out var parsed));
            Assert.Equal(pack, parsed);
        });

        Assert.True(ModulePackCatalog.TryParseLicenseFeature("PACK:Hospitality", out var caseInsensitive));
        Assert.Equal(ModulePack.Hospitality, caseInsensitive);

        Assert.False(ModulePackCatalog.TryParseLicenseFeature(null, out _));
        Assert.False(ModulePackCatalog.TryParseLicenseFeature("", out _));
        Assert.False(ModulePackCatalog.TryParseLicenseFeature("pack:", out _));
        Assert.False(ModulePackCatalog.TryParseLicenseFeature("pack:astrology", out _));
        Assert.False(ModulePackCatalog.TryParseLicenseFeature("pack:4242", out _));
        Assert.False(ModulePackCatalog.TryParseLicenseFeature("feature:pms.channel-manager", out _));
    }

    [Fact]
    public void The_license_filter_blocks_only_the_packs_that_are_not_active()
    {
        var retail = ModulePackCatalog.LicenseFilter(ModulePackCatalog.ActivePacksFor(BusinessSector.Retail));

        Assert.True(retail(null));
        Assert.True(retail("pack:finance"));
        Assert.True(retail("pack:core"));
        Assert.False(retail("pack:hospitality"));
        Assert.False(retail("pack:foodbeverage"));

        // Une autre notion de licence n'est pas du ressort du filtre de paquets : il la laisse
        // au filtre qui la comprendra, plutôt que de masquer un écran par excès de zèle.
        Assert.True(retail("feature:pms.channel-manager"));

        Assert.Throws<ArgumentNullException>(() => ModulePackCatalog.LicenseFilter(null!));
    }

    /// <summary>
    /// Le câblage que l'intégrateur fera : la chaîne de licence posée sur chaque domaine de
    /// l'arbre, le filtre passé à <see cref="NavigationFilter.LicenseAllows"/>. Sans toucher à
    /// <see cref="NavigationTreeBuilder"/>, un commerce perd le PMS, le housekeeping, la
    /// restauration et l'événementiel, et garde la finance ; un hôtel voit tout.
    /// </summary>
    [Fact]
    public void Wired_on_the_license_extension_point_the_filter_prunes_the_hotel_domains_for_a_shop()
    {
        var stampedTree = FunctionalArchitectureCatalog.Tree
            .Select(domain => domain with { LicenseFeature = ModulePackCatalog.LicenseFeatureForDomain(domain.Id) })
            .ToList();

        var everything = NavigationTreeBuilder.Build(stampedTree, AllPermissions, NavigationFilter.Sidebar);

        var hotel = NavigationTreeBuilder.Build(
            stampedTree,
            AllPermissions,
            NavigationFilter.Sidebar with
            {
                LicenseAllows = ModulePackCatalog.LicenseFilter(ModulePackCatalog.ActivePacksFor(BusinessSector.Hospitality))
            });

        var shop = NavigationTreeBuilder.Build(
            stampedTree,
            AllPermissions,
            NavigationFilter.Sidebar with
            {
                LicenseAllows = ModulePackCatalog.LicenseFilter(ModulePackCatalog.ActivePacksFor(BusinessSector.Retail))
            });

        Assert.Equal(everything.Domains.Select(domain => domain.Id), hotel.Domains.Select(domain => domain.Id));

        var shopDomains = shop.Domains.Select(domain => domain.Id).ToArray();
        Assert.DoesNotContain("06", shopDomains);
        Assert.DoesNotContain("08", shopDomains);
        Assert.DoesNotContain("09", shopDomains);
        Assert.DoesNotContain("10", shopDomains);
        Assert.Contains("03", shopDomains);
        Assert.Contains("22", shopDomains);

        // Les écrans hôteliers ont disparu de l'ordre des onglets, aucun autre n'a bougé.
        Assert.Equal(
            everything.OpenableTabOrder.Except(everything.Paths
                .Where(path => !ModulePackCatalog.LicenseFilter(ModulePackCatalog.ActivePacksFor(BusinessSector.Retail))(path.Domain.LicenseFeature))
                .Select(path => path.Screen.LegacyTabIndex!.Value)),
            shop.OpenableTabOrder);
    }
}
