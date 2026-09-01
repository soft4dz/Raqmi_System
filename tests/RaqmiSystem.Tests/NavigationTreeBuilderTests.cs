using RaqmiSystem.Application.Navigation;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Tests;

/// <summary>
/// Élagage de l'arbre (permissions, planifié, alias, recherche, filtres, points
/// d'extension), conversion des statuts historiques et ordre de navigation clavier.
/// </summary>
public sealed class NavigationTreeBuilderTests
{
    private const int HomeTab = 0;

    private static IReadOnlyList<DomainNode> Tree => FunctionalArchitectureCatalog.Tree;

    private static readonly IReadOnlySet<string> AllPermissions =
        PermissionCatalog.All.Select(permission => permission.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> NoPermission = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlySet<string> Only(params string[] permissions) =>
        permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<int> Tabs(NavigationTree tree) =>
        tree.Paths.Select(path => path.Screen.LegacyTabIndex!.Value);

    // ------------------------------------------------------------- permissions

    [Fact]
    public void Lodging_read_alone_yields_domain_06_with_its_two_screens_and_nothing_else()
    {
        var tree = NavigationTreeBuilder.Build(Tree, Only(PermissionCatalog.LodgingRead), NavigationFilter.Sidebar);

        var domain = Assert.Single(tree.Domains);
        Assert.Equal("06", domain.Id);
        Assert.Equal([15, 30], tree.OpenableTabOrder);
        Assert.Equal(2, tree.Paths.Count());

        // La clôture (closing.read) et les alias ne sont pas là : ni doublon, ni écran fermé.
        Assert.DoesNotContain(5, Tabs(tree));
        Assert.All(tree.Paths, path => Assert.False(path.Screen.IsAlias));
        Assert.All(tree.Paths, path => Assert.Equal(PermissionCatalog.LodgingRead, path.Screen.ReadPermissionKey));

        // Les sous-modules planifiés du domaine (équipements) n'apparaissent pas dans la barre latérale.
        Assert.All(domain.Modules.SelectMany(module => module.Submodules), submodule => Assert.NotEmpty(submodule.Screens));
    }

    [Fact]
    public void Permission_comparison_follows_the_set_comparer()
    {
        var tree = NavigationTreeBuilder.Build(Tree, Only("LODGING.READ"), NavigationFilter.Sidebar);
        Assert.Equal([15, 30], tree.OpenableTabOrder);
    }

    [Fact]
    public void No_permission_yields_an_empty_sidebar_tree()
    {
        var tree = NavigationTreeBuilder.Build(Tree, NoPermission, NavigationFilter.Sidebar);

        Assert.True(tree.IsEmpty);
        Assert.Empty(tree.OpenableTabOrder);
    }

    [Fact]
    public void No_permission_on_the_home_keeps_only_planned_nodes()
    {
        var tree = NavigationTreeBuilder.Build(Tree, NoPermission, NavigationFilter.Home);

        Assert.False(tree.IsEmpty);
        Assert.Empty(tree.Paths);

        foreach (var submodule in tree.Domains.SelectMany(domain => domain.Modules).SelectMany(module => module.Submodules))
        {
            Assert.Empty(submodule.Screens);
            Assert.Equal(FunctionalMaturity.Planned, submodule.Maturity);
        }

        // Le parking, entièrement planifié, est visible sur l'accueil ; jamais dans la barre latérale.
        Assert.NotNull(tree.FindDomain("19"));
        Assert.Null(NavigationTreeBuilder.Build(Tree, AllPermissions, NavigationFilter.Sidebar).FindDomain("19"));
    }

    [Fact]
    public void All_permissions_open_the_30_tabs_once_each_in_tree_order()
    {
        var tree = NavigationTreeBuilder.Build(Tree, AllPermissions, NavigationFilter.Sidebar);

        Assert.Equal(30, tree.OpenableTabOrder.Count);
        Assert.Equal(Enumerable.Range(1, 30).OrderBy(tab => tab), tree.OpenableTabOrder.OrderBy(tab => tab));
        Assert.Equal(
            FunctionalArchitectureCatalog.PrimaryPaths.Select(path => path.Screen.LegacyTabIndex!.Value),
            tree.OpenableTabOrder);
        Assert.All(tree.Paths, path => Assert.False(path.Screen.IsAlias));
    }

    // ------------------------------------------------------------------- alias

    [Fact]
    public void Aliases_never_duplicate_a_tab_unless_explicitly_requested()
    {
        var sidebar = NavigationTreeBuilder.Build(Tree, AllPermissions, NavigationFilter.Sidebar);
        Assert.Equal(Tabs(sidebar).Count(), Tabs(sidebar).Distinct().Count());
        Assert.Null(sidebar.FindDomain("15"));

        var withAliases = NavigationTreeBuilder.Build(Tree, AllPermissions, NavigationFilter.Sidebar with { IncludeAliases = true });
        Assert.True(Tabs(withAliases).Count() > 30);
        Assert.Equal(["01", "02"], withAliases.Paths.Where(path => path.Screen.LegacyTabIndex == 16).Select(path => path.Domain.Id));
        Assert.NotNull(withAliases.FindDomain("15"));

        // Même avec les alias, l'ordre clavier ne compte chaque onglet qu'une fois.
        Assert.Equal(30, withAliases.OpenableTabOrder.Count);
    }

    // ---------------------------------------------------------------- recherche

    [Theory]
    [InlineData("HÉBERGEMENT")]
    [InlineData("hebergement")]
    [InlineData("  Hébergement ")]
    public void Search_ignores_accents_case_and_surrounding_spaces(string query)
    {
        var tree = NavigationTreeBuilder.Build(Tree, AllPermissions, NavigationFilter.Sidebar with { SearchText = query });

        Assert.Contains(15, Tabs(tree));
        Assert.Contains(30, Tabs(tree));
        Assert.DoesNotContain(11, Tabs(tree));
    }

    [Fact]
    public void Search_reaches_a_screen_through_its_alias_path_without_rendering_the_alias()
    {
        var tree = NavigationTreeBuilder.Build(Tree, AllPermissions, NavigationFilter.Sidebar with { SearchText = "circuits" });

        var path = Assert.Single(tree.Paths);
        Assert.Equal(16, path.Screen.LegacyTabIndex);
        Assert.Equal("01", path.Domain.Id);
        Assert.False(path.Screen.IsAlias);
    }

    [Fact]
    public void Search_matches_the_historical_order_and_the_description()
    {
        var byOrder = NavigationTreeBuilder.Build(Tree, AllPermissions, NavigationFilter.Sidebar with { SearchText = "10.1" });
        Assert.Equal([30], byOrder.OpenableTabOrder);

        var byDescription = NavigationTreeBuilder.Build(Tree, AllPermissions, NavigationFilter.Sidebar with { SearchText = "night audit" });
        Assert.Contains(30, Tabs(byDescription));
        Assert.Contains(5, Tabs(byDescription));
    }

    [Fact]
    public void Search_never_shows_what_the_profile_cannot_open()
    {
        var tree = NavigationTreeBuilder.Build(Tree, Only(PermissionCatalog.HrRead), NavigationFilter.Sidebar with { SearchText = "comptabilit" });
        Assert.True(tree.IsEmpty);
    }

    [Fact]
    public void Search_on_the_home_finds_planned_nodes()
    {
        var tree = NavigationTreeBuilder.Build(Tree, AllPermissions, NavigationFilter.Home with { SearchText = "parking" });

        var domain = Assert.Single(tree.Domains);
        Assert.Equal("19", domain.Id);
        Assert.Empty(tree.Paths);
    }

    [Fact]
    public void Empty_search_is_no_filter()
    {
        var tree = NavigationTreeBuilder.Build(Tree, AllPermissions, NavigationFilter.Sidebar with { SearchText = "   " });
        Assert.Equal(30, tree.OpenableTabOrder.Count);
    }

    // ------------------------------------------------------------------ filtres

    [Fact]
    public void Domain_filter_keeps_a_single_domain()
    {
        var tree = NavigationTreeBuilder.Build(Tree, AllPermissions, NavigationFilter.Sidebar with { DomainId = "03" });

        var domain = Assert.Single(tree.Domains);
        Assert.Equal("03", domain.Id);
        Assert.Equal([11, 6, 13, 12, 2], tree.OpenableTabOrder);
    }

    [Fact]
    public void Maturity_filter_applies_to_screens_and_planned_nodes()
    {
        var plannedOnly = NavigationTreeBuilder.Build(
            Tree,
            AllPermissions,
            NavigationFilter.Home with { Maturities = new HashSet<FunctionalMaturity> { FunctionalMaturity.Planned } });

        Assert.Empty(plannedOnly.Paths);
        Assert.NotEmpty(plannedOnly.Domains);

        var functionalOnly = NavigationTreeBuilder.Build(
            Tree,
            AllPermissions,
            NavigationFilter.Home with { Maturities = new HashSet<FunctionalMaturity> { FunctionalMaturity.Functional } });

        Assert.Equal(30, functionalOnly.OpenableTabOrder.Count);
        Assert.All(
            functionalOnly.Domains.SelectMany(domain => domain.Modules).SelectMany(module => module.Submodules),
            submodule => Assert.NotEmpty(submodule.Screens));
    }

    // ------------------------------------------------------- points d'extension

    [Fact]
    public void Extension_points_let_everything_through_by_default()
    {
        var filter = new NavigationFilter();

        Assert.True(filter.LicenseAllows(null));
        Assert.True(filter.LicenseAllows("anything"));
        Assert.True(filter.ScopeAllows(NavigationScope.Unit, "06"));
        Assert.True(filter.ScopeAllows(NavigationScope.Global, "02"));
    }

    [Fact]
    public void License_gate_can_prune_everything()
    {
        var tree = NavigationTreeBuilder.Build(
            Tree,
            AllPermissions,
            NavigationFilter.Sidebar with { LicenseAllows = _ => false });

        Assert.True(tree.IsEmpty);
    }

    [Fact]
    public void Scope_gate_can_restrict_to_global_domains()
    {
        var tree = NavigationTreeBuilder.Build(
            Tree,
            AllPermissions,
            NavigationFilter.Sidebar with { ScopeAllows = (scope, _) => scope == NavigationScope.Global });

        Assert.NotEmpty(tree.Domains);
        Assert.All(tree.Domains, domain => Assert.Equal(NavigationScope.Global, domain.Scope));
        Assert.Null(tree.FindDomain("06"));
        Assert.NotNull(tree.FindDomain("02"));
    }

    [Fact]
    public void Builder_does_not_mutate_the_source_tree()
    {
        var before = FunctionalArchitectureCatalog.PrimaryPaths.Count();
        _ = NavigationTreeBuilder.Build(Tree, Only(PermissionCatalog.HrRead), NavigationFilter.Sidebar);

        Assert.Equal(before, FunctionalArchitectureCatalog.PrimaryPaths.Count());
        Assert.Equal(22, Tree.Count);
    }

    // ------------------------------------------------- conversion des statuts

    [Theory]
    [InlineData(LegacyModuleStatus.Disponible, FunctionalMaturity.Functional)]
    [InlineData(LegacyModuleStatus.ApiPrete, FunctionalMaturity.TechnicalPreview)]
    [InlineData(LegacyModuleStatus.Partiel, FunctionalMaturity.TechnicalPreview)]
    [InlineData(LegacyModuleStatus.Planifie, FunctionalMaturity.Planned)]
    public void Legacy_status_converts_to_the_four_level_model(LegacyModuleStatus status, FunctionalMaturity expected)
    {
        Assert.Equal(expected, FunctionalMaturityMapper.FromLegacyStatus(status));
    }

    [Fact]
    public void Production_ready_is_never_granted_by_conversion()
    {
        foreach (var status in Enum.GetValues<LegacyModuleStatus>())
        {
            Assert.NotEqual(FunctionalMaturity.ProductionReady, FunctionalMaturityMapper.FromLegacyStatus(status));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => FunctionalMaturityMapper.FromLegacyStatus((LegacyModuleStatus)42));
    }

    [Theory]
    [InlineData(FunctionalMaturity.Planned, "Planifié")]
    [InlineData(FunctionalMaturity.TechnicalPreview, "Aperçu technique")]
    [InlineData(FunctionalMaturity.Functional, "Fonctionnel")]
    [InlineData(FunctionalMaturity.ProductionReady, "Prêt pour la production")]
    public void Maturity_labels_are_the_approved_french_wording(FunctionalMaturity maturity, string label)
    {
        Assert.Equal(label, FunctionalMaturityMapper.Label(maturity));
    }

    [Fact]
    public void Highest_maturity_is_planned_without_children()
    {
        Assert.Equal(FunctionalMaturity.Planned, FunctionalMaturityMapper.Highest([]));
        Assert.Equal(
            FunctionalMaturity.Functional,
            FunctionalMaturityMapper.Highest([FunctionalMaturity.Planned, FunctionalMaturity.Functional, FunctionalMaturity.TechnicalPreview]));
    }

    // ---------------------------------------------------- normalisation

    [Fact]
    public void Search_normalization_strips_accents_and_case()
    {
        Assert.Equal("hebergement & occupation", NavigationSearch.Normalize("Hébergement & Occupation"));
        Assert.Equal("tva", NavigationSearch.Normalize("TVA"));
        Assert.True(NavigationSearch.Matches("comptabilite scf", "  SCF "));
        Assert.True(NavigationSearch.Matches("comptabilite scf", null));
        Assert.False(NavigationSearch.Matches("comptabilite scf", "paie"));
    }

    // ------------------------------------------------------- ordre clavier

    [Fact]
    public void Keyboard_order_follows_the_visible_tree_and_wraps_through_home()
    {
        var order = NavigationTreeBuilder.Build(Tree, AllPermissions, NavigationFilter.Sidebar).OpenableTabOrder;
        var first = order[0];
        var last = order[^1];

        // Le premier écran de l'arbre est celui de Mon Espace (validations), pas l'onglet 1.
        Assert.Equal(16, first);
        Assert.Equal(first, NavigationKeyboardOrder.Next(order, HomeTab, HomeTab, +1));
        Assert.Equal(last, NavigationKeyboardOrder.Next(order, HomeTab, HomeTab, -1));
        Assert.Equal(HomeTab, NavigationKeyboardOrder.Next(order, HomeTab, last, +1));
        Assert.Equal(HomeTab, NavigationKeyboardOrder.Next(order, HomeTab, first, -1));

        for (var index = 0; index < order.Count - 1; index++)
        {
            Assert.Equal(order[index + 1], NavigationKeyboardOrder.Next(order, HomeTab, order[index], +1));
            Assert.Equal(order[index], NavigationKeyboardOrder.Next(order, HomeTab, order[index + 1], -1));
        }
    }

    [Fact]
    public void Keyboard_order_skips_screens_the_profile_cannot_open()
    {
        var order = NavigationTreeBuilder.Build(Tree, Only(PermissionCatalog.LodgingRead), NavigationFilter.Sidebar).OpenableTabOrder;

        Assert.Equal([15, 30], order);
        Assert.Equal(15, NavigationKeyboardOrder.Next(order, HomeTab, HomeTab, +1));
        Assert.Equal(30, NavigationKeyboardOrder.Next(order, HomeTab, 15, +1));
        Assert.Equal(HomeTab, NavigationKeyboardOrder.Next(order, HomeTab, 30, +1));
        Assert.Equal(30, NavigationKeyboardOrder.Next(order, HomeTab, HomeTab, -1));

        // Un onglet hors de l'ordre visible (droit retiré) repart de l'accueil.
        Assert.Equal(15, NavigationKeyboardOrder.Next(order, HomeTab, 11, +1));
        Assert.Equal(30, NavigationKeyboardOrder.Next(order, HomeTab, 11, -1));
    }

    [Fact]
    public void Keyboard_order_without_any_screen_stays_home()
    {
        Assert.Equal(HomeTab, NavigationKeyboardOrder.Next([], HomeTab, HomeTab, +1));
        Assert.Equal(HomeTab, NavigationKeyboardOrder.Next([], HomeTab, HomeTab, -1));
        Assert.Equal(7, NavigationKeyboardOrder.Next([7], HomeTab, 7, 0));
    }
}
