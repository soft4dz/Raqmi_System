using System.Text.RegularExpressions;
using RaqmiSystem.Application.Navigation;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Tests;

/// <summary>
/// Forme de l'arbre Domaine → Module → Sous-module → Écran : identifiants, couverture des
/// 30 onglets, permissions, alias, rattachement des 50 entrées historiques.
/// </summary>
public sealed class NavigationTreeTests
{
    private const int HomeTab = 0;
    private const int TabCount = 30;

    private static readonly Regex IdSegment = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant);

    private static IReadOnlyList<DomainNode> Tree => FunctionalArchitectureCatalog.Tree;

    private static IEnumerable<INavigationNode> AllNodes()
    {
        foreach (var domain in Tree)
        {
            yield return domain;

            foreach (var module in domain.Modules)
            {
                yield return module;

                foreach (var submodule in module.Submodules)
                {
                    yield return submodule;

                    foreach (var screen in submodule.Screens)
                    {
                        yield return screen;
                    }
                }
            }
        }
    }

    [Fact]
    public void Tree_has_the_22_domains_in_catalog_order()
    {
        Assert.Equal(FunctionalArchitectureCatalog.ExpectedDomainCount, Tree.Count);
        Assert.Equal(
            FunctionalArchitectureCatalog.Domains.Select(domain => domain.Id),
            Tree.Select(domain => domain.Id));
        Assert.Equal(Enumerable.Range(1, Tree.Count), Tree.Select(domain => domain.Order));

        foreach (var (definition, node) in FunctionalArchitectureCatalog.Domains.Zip(Tree))
        {
            Assert.Equal(definition.Name, node.Label);
            Assert.Equal(definition.IconKey, node.IconKey);
            Assert.Equal(definition.Maturity, node.Maturity);
        }
    }

    [Fact]
    public void Domain_icon_keys_are_the_approved_set()
    {
        string[] expected =
        [
            "MonEspace", "Administration", "Finance", "Commercial", "Facturation", "Hebergement",
            "Revenue", "Housekeeping", "Evenementiel", "Restauration", "Stocks", "Achats",
            "RessourcesHumaines", "Maintenance", "Qualite", "Juridique", "Documentaire", "Marina",
            "Parking", "Pilotage", "Integrations", "Systeme"
        ];

        Assert.Equal(expected, FunctionalArchitectureCatalog.Domains.Select(domain => domain.IconKey));
    }

    [Fact]
    public void Node_ids_are_unique_stable_and_hierarchical()
    {
        var ids = AllNodes().Select(node => node.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());

        foreach (var domain in Tree)
        {
            Assert.Matches("^[0-9]{2}$", domain.Id);

            foreach (var module in domain.Modules)
            {
                AssertChildId(domain.Id, module.Id);

                foreach (var submodule in module.Submodules)
                {
                    AssertChildId(module.Id, submodule.Id);

                    foreach (var screen in submodule.Screens)
                    {
                        AssertChildId(submodule.Id, screen.Id);
                    }
                }
            }
        }
    }

    private static void AssertChildId(string parentId, string childId)
    {
        Assert.StartsWith(parentId + ".", childId, StringComparison.Ordinal);
        var segment = childId[(parentId.Length + 1)..];
        Assert.DoesNotContain('.', segment);
        Assert.Matches(IdSegment, segment);
    }

    [Fact]
    public void Tree_has_no_orphan_and_siblings_are_numbered_from_one()
    {
        foreach (var domain in Tree)
        {
            Assert.NotEmpty(domain.Modules);
            Assert.Equal(Enumerable.Range(1, domain.Modules.Count), domain.Modules.Select(module => module.Order));

            foreach (var module in domain.Modules)
            {
                Assert.NotEmpty(module.Submodules);
                Assert.Equal(Enumerable.Range(1, module.Submodules.Count), module.Submodules.Select(sub => sub.Order));

                foreach (var submodule in module.Submodules)
                {
                    Assert.Equal(Enumerable.Range(1, submodule.Screens.Count), submodule.Screens.Select(screen => screen.Order));
                }
            }
        }
    }

    [Fact]
    public void Every_tab_from_1_to_30_is_reached_by_exactly_one_primary_path()
    {
        var primaryTabs = FunctionalArchitectureCatalog.PrimaryPaths
            .Select(path => path.Screen.LegacyTabIndex!.Value)
            .ToList();

        Assert.Equal(Enumerable.Range(1, TabCount).OrderBy(tab => tab), primaryTabs.OrderBy(tab => tab));
        Assert.Equal(primaryTabs.Count, primaryTabs.Distinct().Count());

        foreach (var tab in Enumerable.Range(1, TabCount))
        {
            Assert.True(FunctionalArchitectureCatalog.TryGetPrimaryPath(tab, out var path));
            Assert.NotNull(path);
            Assert.False(path!.Screen.IsAlias);
            Assert.Equal(tab, path.Screen.LegacyTabIndex);
        }

        Assert.False(FunctionalArchitectureCatalog.TryGetPrimaryPath(HomeTab, out _));
    }

    [Fact]
    public void Every_screen_carries_a_permission_known_to_the_catalog_and_a_tab()
    {
        var known = PermissionCatalog.All.Select(permission => permission.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var path in FunctionalArchitectureCatalog.EnumeratePaths(Tree))
        {
            Assert.Contains(path.Screen.ReadPermissionKey, known);
            Assert.True(path.Screen.IsOpenable, $"L'écran '{path.Screen.Id}' n'a pas d'onglet.");
            Assert.Equal(FunctionalMaturity.Functional, path.Screen.Maturity);
            Assert.Null(path.Screen.LicenseFeature);
        }

        // Les conteneurs ne portent pas de clé propre : ils sont visibles dès qu'un enfant l'est.
        foreach (var node in AllNodes().Where(node => node is not ScreenNode))
        {
            Assert.Null(node.ReadPermissionKey);
        }
    }

    [Fact]
    public void Primary_screens_carry_the_permission_of_their_historical_tab()
    {
        var expected = new Dictionary<int, string>
        {
            [1] = PermissionCatalog.UnitsRead, [2] = PermissionCatalog.RevenueRead, [3] = PermissionCatalog.DashboardRead,
            [4] = PermissionCatalog.AuditRead, [5] = PermissionCatalog.ClosingRead, [6] = PermissionCatalog.TreasuryRead,
            [7] = PermissionCatalog.CustomersRead, [8] = PermissionCatalog.InvoicesRead, [9] = PermissionCatalog.SettingsRead,
            [10] = PermissionCatalog.UsersRead, [11] = PermissionCatalog.AccountingRead, [12] = PermissionCatalog.BudgetRead,
            [13] = PermissionCatalog.ReceivablesRead, [14] = PermissionCatalog.TariffsRead, [15] = PermissionCatalog.LodgingRead,
            [16] = PermissionCatalog.ApprovalsRead, [17] = PermissionCatalog.ReportsRead, [18] = PermissionCatalog.MaintenanceRead,
            [19] = PermissionCatalog.DashboardRead, [20] = PermissionCatalog.DashboardRead, [21] = PermissionCatalog.HousekeepingRead,
            [22] = PermissionCatalog.HrRead, [23] = PermissionCatalog.CrmRead, [24] = PermissionCatalog.InventoryRead,
            [25] = PermissionCatalog.PurchasingRead, [26] = PermissionCatalog.KitchenRead, [27] = PermissionCatalog.SyncRead,
            [28] = PermissionCatalog.MiceRead, [29] = PermissionCatalog.DashboardRead, [30] = PermissionCatalog.LodgingRead
        };

        foreach (var (tab, permission) in expected)
        {
            Assert.True(FunctionalArchitectureCatalog.TryGetPrimaryPath(tab, out var path));
            Assert.Equal(permission, path!.Screen.ReadPermissionKey);
        }
    }

    [Fact]
    public void Aliases_reuse_the_permission_and_tab_of_their_primary_screen()
    {
        var aliases = FunctionalArchitectureCatalog.EnumeratePaths(Tree).Where(path => path.Screen.IsAlias).ToList();
        Assert.NotEmpty(aliases);

        foreach (var alias in aliases)
        {
            Assert.True(FunctionalArchitectureCatalog.TryGetPrimaryPath(alias.Screen.LegacyTabIndex!.Value, out var primary));
            Assert.Equal(primary!.Screen.ReadPermissionKey, alias.Screen.ReadPermissionKey);
            Assert.NotEqual(primary.Screen.Id, alias.Screen.Id);
        }
    }

    [Theory]
    [InlineData(16, "01", "02")]
    [InlineData(4, "22", "15")]
    [InlineData(14, "07", "04")]
    [InlineData(5, "06", "03")]
    public void Shared_tabs_have_the_approved_primary_domain_and_an_alias(int tab, string primaryDomain, string aliasDomain)
    {
        Assert.True(FunctionalArchitectureCatalog.TryGetPrimaryPath(tab, out var primary));
        Assert.Equal(primaryDomain, primary!.Domain.Id);

        var alias = FunctionalArchitectureCatalog.EnumeratePaths(Tree)
            .Where(path => path.Screen.IsAlias && path.Screen.LegacyTabIndex == tab)
            .Select(path => path.Domain.Id);

        Assert.Contains(aliasDomain, alias);
    }

    [Fact]
    public void Approved_paths_of_the_mapping_are_respected()
    {
        AssertPrimaryPath(30, "06", "front-office", "arrivals");
        AssertPrimaryPath(15, "06", "inventaire", "chambres");
        AssertPrimaryPath(5, "06", "controle", "cloture");
        AssertPrimaryPath(11, "03", "comptabilite", "generale");
        AssertPrimaryPath(2, "03", "recettes", "ca-journalier");
        AssertPrimaryPath(16, "01", "travail", "mes-validations");
        AssertPrimaryPath(4, "22", "maintenance", "journal-audit");
        AssertPrimaryPath(27, "22", "diagnostic", "postes");
        AssertPrimaryPath(19, "20", "dashboards", "groupe");
        AssertPrimaryPath(29, "20", "kpi", "bibliotheque");
        AssertPrimaryPath(17, "20", "bi", "rapports");
        AssertPrimaryPath(14, "07", "tarification", "plans");
        AssertPrimaryPath(7, "04", "clients", "fichier");
    }

    private static void AssertPrimaryPath(int tab, string domainId, string moduleKey, string submoduleKey)
    {
        Assert.True(FunctionalArchitectureCatalog.TryGetPrimaryPath(tab, out var path));
        Assert.Equal(domainId, path!.Domain.Id);
        Assert.Equal($"{domainId}.{moduleKey}", path.Module.Id);
        Assert.Equal($"{domainId}.{moduleKey}.{submoduleKey}", path.Submodule.Id);
    }

    [Fact]
    public void Submodules_without_screen_are_planned_and_containers_inherit_their_best_child()
    {
        foreach (var domain in Tree)
        {
            foreach (var module in domain.Modules)
            {
                Assert.Equal(
                    FunctionalMaturityMapper.Highest(module.Submodules.Select(sub => sub.Maturity)),
                    module.Maturity);

                foreach (var submodule in module.Submodules)
                {
                    if (submodule.Screens.Count == 0)
                    {
                        Assert.Equal(FunctionalMaturity.Planned, submodule.Maturity);
                    }
                    else
                    {
                        Assert.Equal(FunctionalMaturity.Functional, submodule.Maturity);
                    }
                }
            }
        }

        // Chaque domaine planifié de la cartographie n'a aucun écran primaire.
        foreach (var domain in Tree.Where(domain => domain.Maturity == FunctionalMaturity.Planned && domain.Id != "01"))
        {
            Assert.DoesNotContain(
                FunctionalArchitectureCatalog.PrimaryPaths,
                path => path.Domain.Id == domain.Id);
        }
    }

    [Fact]
    public void Every_historical_entry_is_placed_under_one_module_of_its_domain()
    {
        var orders = FunctionalArchitectureCatalog.Domains.SelectMany(domain => domain.LegacyModuleOrders).ToList();
        Assert.Equal(FunctionalArchitectureCatalog.ExpectedLegacyModuleCount, orders.Count);

        var claimed = Tree.SelectMany(domain => domain.Modules).SelectMany(module => module.LegacyModuleOrders).ToList();
        Assert.Equal(orders.OrderBy(order => order, StringComparer.Ordinal), claimed.OrderBy(order => order, StringComparer.Ordinal));

        foreach (var order in orders)
        {
            var placement = FunctionalArchitectureCatalog.PlacementForLegacyOrder(order);
            Assert.Equal(FunctionalArchitectureCatalog.DomainForLegacyOrder(order).Id, placement.Domain.Id);
            Assert.Contains(order, placement.Module.LegacyModuleOrders);
            Assert.True(placement.ModuleRank >= 1);
        }

        // Le rang suit l'ordre de l'arbre : un module d'un domaine antérieur vient avant.
        Assert.True(
            FunctionalArchitectureCatalog.PlacementForLegacyOrder("1").ModuleRank
            < FunctionalArchitectureCatalog.PlacementForLegacyOrder("10.1").ModuleRank);
        Assert.True(
            FunctionalArchitectureCatalog.PlacementForLegacyOrder("10.1").ModuleRank
            < FunctionalArchitectureCatalog.PlacementForLegacyOrder("28").ModuleRank);

        Assert.Throws<KeyNotFoundException>(() => FunctionalArchitectureCatalog.PlacementForLegacyOrder("99"));
    }

    [Fact]
    public void Screens_with_a_historical_order_live_under_the_module_that_absorbs_it()
    {
        foreach (var path in FunctionalArchitectureCatalog.EnumeratePaths(Tree))
        {
            if (path.Screen.LegacyOrder is { } order)
            {
                Assert.Contains(order, path.Module.LegacyModuleOrders);
                Assert.Equal(path.Domain.Id, FunctionalArchitectureCatalog.DomainForLegacyOrder(order).Id);
            }
        }

        // Chaque onglet primaire porte l'entrée historique qui le nomme sur l'accueil.
        foreach (var path in FunctionalArchitectureCatalog.PrimaryPaths)
        {
            Assert.NotNull(path.Screen.LegacyOrder);
        }
    }

    [Fact]
    public void Breadcrumb_path_reads_domain_module_submodule_screen()
    {
        Assert.True(FunctionalArchitectureCatalog.TryGetPrimaryPath(30, out var path));
        Assert.Equal(
            ["PMS / Hébergement", "Front Office", "Arrivées et départs", "PMS front office"],
            path!.Labels);
    }

    [Fact]
    public void Domain_maturity_matches_the_target_cartography()
    {
        string[] functional = ["02", "03", "04", "05", "06", "08", "09", "11", "13", "20", "22"];
        string[] preview = ["07", "10", "12", "15", "21"];
        string[] planned = ["01", "14", "16", "17", "18", "19"];

        foreach (var domain in Tree)
        {
            var expected = functional.Contains(domain.Id) ? FunctionalMaturity.Functional
                : preview.Contains(domain.Id) ? FunctionalMaturity.TechnicalPreview
                : FunctionalMaturity.Planned;

            Assert.Equal(expected, domain.Maturity);
            Assert.Contains(domain.Id, functional.Concat(preview).Concat(planned));
        }

        Assert.DoesNotContain(AllNodes(), node => node.Maturity == FunctionalMaturity.ProductionReady);
    }
}
