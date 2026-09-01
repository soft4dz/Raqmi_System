namespace RaqmiSystem.Application.Navigation;

/// <summary>
/// Options d'élagage de l'arbre de navigation.
/// </summary>
/// <remarks>
/// Deux surfaces, deux réglages : l'accueil montre tout, planifié compris (<see cref="Home"/>) ;
/// la barre latérale ne montre que ce qui s'ouvre (<see cref="Sidebar"/>). Les deux points
/// d'extension <see cref="LicenseAllows"/> et <see cref="ScopeAllows"/> laissent tout passer
/// par défaut : aucune licence ni aucun périmètre d'unité n'est modélisé aujourd'hui, et le
/// filtre ne doit rien inventer derrière.
/// </remarks>
public sealed record NavigationFilter
{
    public static NavigationFilter Sidebar { get; } = new();

    public static NavigationFilter Home { get; } = new() { IncludePlanned = true };

    /// <summary>Garder les nœuds planifiés (sous-modules sans écran). Faux = barre latérale.</summary>
    public bool IncludePlanned { get; init; }

    /// <summary>
    /// Garder les chemins secondaires vers un écran déjà atteint par son chemin primaire.
    /// Faux par défaut : une barre latérale qui listerait deux fois le même écran tromperait.
    /// </summary>
    public bool IncludeAliases { get; init; }

    /// <summary>Saisie de recherche brute ; normalisée par <see cref="NavigationSearch"/>.</summary>
    public string? SearchText { get; init; }

    /// <summary>Niveaux de maturité retenus ; nul = tous.</summary>
    public IReadOnlySet<FunctionalMaturity>? Maturities { get; init; }

    /// <summary>Restreindre à un domaine ; nul = tous.</summary>
    public string? DomainId { get; init; }

    /// <summary>Point d'extension licence : reçoit <see cref="INavigationNode.LicenseFeature"/>.</summary>
    public Func<string?, bool> LicenseAllows { get; init; } = static _ => true;

    /// <summary>Point d'extension périmètre : reçoit <see cref="INavigationNode.Scope"/> et l'identifiant du nœud.</summary>
    public Func<NavigationScope, string, bool> ScopeAllows { get; init; } = static (_, _) => true;
}

/// <summary>
/// Résultat de l'élagage : les domaines conservés, dans l'ordre de l'arbre source.
/// </summary>
public sealed record NavigationTree(IReadOnlyList<DomainNode> Domains)
{
    public static NavigationTree Empty { get; } = new([]);

    public bool IsEmpty => Domains.Count == 0;

    /// <summary>Tous les chemins conservés, dans l'ordre de l'arbre.</summary>
    public IEnumerable<NavigationPath> Paths => FunctionalArchitectureCatalog.EnumeratePaths(Domains);

    /// <summary>
    /// Les onglets ouvrables dans l'ordre de l'arbre, chacun une fois : l'ordre de la barre
    /// latérale, donc celui des raccourcis module précédent / suivant.
    /// </summary>
    public IReadOnlyList<int> OpenableTabOrder => Paths
        .Where(path => !path.Screen.IsAlias && path.Screen.LegacyTabIndex is not null)
        .Select(path => path.Screen.LegacyTabIndex!.Value)
        .Distinct()
        .ToList();

    public DomainNode? FindDomain(string domainId) =>
        Domains.FirstOrDefault(domain => string.Equals(domain.Id, domainId, StringComparison.Ordinal));
}

/// <summary>
/// Élagage pur de l'arbre de navigation : permissions, maturité, recherche, domaine, puis les
/// deux points d'extension. Aucune dépendance WPF : la barre latérale et les tests lisent le
/// même résultat.
/// </summary>
/// <remarks>
/// Le masquage n'est jamais une sécurité : chaque route reste protégée par sa politique côté
/// serveur. Ce filtre ne fait que ne pas proposer ce que le profil ne pourrait pas ouvrir.
/// </remarks>
public static class NavigationTreeBuilder
{
    public static NavigationTree Build(
        IReadOnlyList<DomainNode> catalogue,
        IReadOnlySet<string> grantedPermissions,
        NavigationFilter options)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(grantedPermissions);
        ArgumentNullException.ThrowIfNull(options);

        // La recherche lit tous les chemins d'un écran, alias compris, même quand les alias
        // ne sont pas rendus : « circuits » doit trouver les validations bien que le chemin
        // « Paramétrage → Circuits » ne figure pas dans la barre latérale.
        var searchIndex = string.IsNullOrWhiteSpace(options.SearchText)
            ? null
            : BuildSearchIndex(catalogue);

        var domains = new List<DomainNode>();

        foreach (var domain in catalogue)
        {
            if (options.DomainId is { } domainId && !string.Equals(domain.Id, domainId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!PassesExtensionPoints(domain, options))
            {
                continue;
            }

            var modules = new List<ModuleNode>();

            foreach (var module in domain.Modules)
            {
                if (!PassesExtensionPoints(module, options))
                {
                    continue;
                }

                var submodules = new List<SubmoduleNode>();

                foreach (var submodule in module.Submodules)
                {
                    if (Prune(domain, module, submodule, grantedPermissions, options, searchIndex) is { } kept)
                    {
                        submodules.Add(kept);
                    }
                }

                if (submodules.Count > 0)
                {
                    modules.Add(module with { Submodules = submodules });
                }
            }

            if (modules.Count > 0)
            {
                domains.Add(domain with { Modules = modules });
            }
        }

        return new NavigationTree(domains);
    }

    private static SubmoduleNode? Prune(
        DomainNode domain,
        ModuleNode module,
        SubmoduleNode submodule,
        IReadOnlySet<string> grantedPermissions,
        NavigationFilter options,
        IReadOnlyDictionary<int, string>? searchIndex)
    {
        if (!PassesExtensionPoints(submodule, options))
        {
            return null;
        }

        // Sous-module sans écran : un nœud planifié. Il n'a pas de permission - il n'y a rien
        // à ouvrir - et n'apparaît que là où l'on montre ce qui reste à construire.
        if (submodule.Screens.Count == 0)
        {
            var visible = options.IncludePlanned
                && PassesMaturity(submodule.Maturity, options)
                && (searchIndex is null
                    || NavigationSearch.Matches(SearchTextOf(domain, module, submodule, screen: null), options.SearchText));

            return visible ? submodule : null;
        }

        var screens = new List<ScreenNode>();

        foreach (var screen in submodule.Screens)
        {
            if (screen.IsAlias && !options.IncludeAliases)
            {
                continue;
            }

            if (!screen.IsOpenable && !options.IncludePlanned)
            {
                continue;
            }

            if (!grantedPermissions.Contains(screen.ReadPermissionKey))
            {
                continue;
            }

            if (!PassesMaturity(screen.Maturity, options) || !PassesExtensionPoints(screen, options))
            {
                continue;
            }

            if (searchIndex is not null)
            {
                var searchText = screen.LegacyTabIndex is { } tab && searchIndex.TryGetValue(tab, out var indexed)
                    ? indexed
                    : SearchTextOf(domain, module, submodule, screen);

                if (!NavigationSearch.Matches(searchText, options.SearchText))
                {
                    continue;
                }
            }

            screens.Add(screen);
        }

        return screens.Count > 0 ? submodule with { Screens = screens } : null;
    }

    private static bool PassesMaturity(FunctionalMaturity maturity, NavigationFilter options) =>
        options.Maturities is null || options.Maturities.Contains(maturity);

    private static bool PassesExtensionPoints(INavigationNode node, NavigationFilter options) =>
        options.LicenseAllows(node.LicenseFeature) && options.ScopeAllows(node.Scope, node.Id);

    // Onglet -> texte de recherche cumulé sur tous ses chemins. Construit à chaque recherche
    // plutôt que mémorisé : une trentaine d'écrans, et l'arbre passé en entrée peut déjà être
    // un sous-arbre.
    private static IReadOnlyDictionary<int, string> BuildSearchIndex(IReadOnlyList<DomainNode> catalogue)
    {
        var index = new Dictionary<int, string>();

        foreach (var path in FunctionalArchitectureCatalog.EnumeratePaths(catalogue))
        {
            if (path.Screen.LegacyTabIndex is not { } tab)
            {
                continue;
            }

            var text = SearchTextOf(path.Domain, path.Module, path.Submodule, path.Screen);
            index[tab] = index.TryGetValue(tab, out var existing) ? existing + " " + text : text;
        }

        return index;
    }

    /// <summary>
    /// Ce sur quoi une recherche porte : le chemin complet, la description et le numéro
    /// historique - ce que les cartes de l'accueil cherchent aussi, pour qu'une surface ne
    /// trouve pas ce que l'autre ignore.
    /// </summary>
    public static string SearchTextOf(DomainNode domain, ModuleNode module, SubmoduleNode submodule, ScreenNode? screen)
    {
        var parts = new List<string> { domain.Id, domain.Label, module.Label, submodule.Label };

        if (screen is not null)
        {
            parts.Add(screen.Label);

            if (screen.Description is { } description)
            {
                parts.Add(description);
            }

            if (screen.LegacyOrder is { } order)
            {
                parts.Add(order);
            }
        }

        return NavigationSearch.Normalize(string.Join(' ', parts));
    }
}

/// <summary>
/// Ordre des raccourcis module précédent / suivant : l'accueil, puis les écrans ouvrables dans
/// l'ordre de l'arbre visible. La liste boucle ; l'accueil, gardé par aucune permission, sert
/// de point fixe.
/// </summary>
public static class NavigationKeyboardOrder
{
    public static int Next(IReadOnlyList<int> openableTabOrder, int homeTabIndex, int currentTabIndex, int direction)
    {
        ArgumentNullException.ThrowIfNull(openableTabOrder);

        if (direction == 0)
        {
            return currentTabIndex;
        }

        var cycle = new List<int>(openableTabOrder.Count + 1) { homeTabIndex };
        cycle.AddRange(openableTabOrder.Where(tab => tab != homeTabIndex));

        var position = cycle.IndexOf(currentTabIndex);

        // Un onglet qui n'est plus dans l'ordre visible (droits retirés en cours de session) se
        // comporte comme l'accueil : le pas suivant mène au premier écran, le précédent au dernier.
        if (position < 0)
        {
            position = 0;
        }

        var step = direction > 0 ? 1 : -1;
        return cycle[(position + step + cycle.Count) % cycle.Count];
    }
}
