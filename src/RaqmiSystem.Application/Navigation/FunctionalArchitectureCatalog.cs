namespace RaqmiSystem.Application.Navigation;

public enum FunctionalMaturity
{
    Planned,
    TechnicalPreview,
    Functional,
    ProductionReady
}

public sealed record FunctionalDomainDefinition(
    string Id,
    string Name,
    string IconKey,
    FunctionalMaturity Maturity,
    IReadOnlyList<string> LegacyModuleOrders);

/// <summary>
/// Taxonomie fonctionnelle stable de Raqmi System. Elle ne dépend ni de WPF ni des TabIndex :
/// tous les clients peuvent donc partager les mêmes identifiants de domaine.
/// </summary>
/// <remarks>
/// Deux niveaux de lecture, une seule source :
/// <list type="bullet">
///   <item><see cref="Domains"/> : les 22 domaines et le rattachement primaire des 50 entrées
///   du catalogue historique (lot 0). Les lignes <c>Domain("NN", …)</c> sont lues par regex
///   par l'outillage : leur forme ne change pas.</item>
///   <item><see cref="Tree"/> : l'arbre complet Domaine → Module → Sous-module → Écran
///   (lot 1.1), défini dans <c>FunctionalArchitectureCatalog.Tree.cs</c> à partir des mêmes
///   domaines. Les 30 onglets actuels y sont placés par un adaptateur (<c>LegacyTabIndex</c>),
///   les sous-modules sans écran y existent comme nœuds planifiés.</item>
/// </list>
/// </remarks>
public static partial class FunctionalArchitectureCatalog
{
    public const int ExpectedDomainCount = 22;
    public const int ExpectedLegacyModuleCount = 50;

    public static IReadOnlyList<FunctionalDomainDefinition> Domains { get; } =
    [
        Domain("01", "Mon Espace", "MonEspace", FunctionalMaturity.Planned, "22.2", "25.2"),
        Domain("02", "Administration & Socle ERP", "Administration", FunctionalMaturity.Functional, "1", "2", "3"),
        Domain("03", "Finance & Comptabilité", "Finance", FunctionalMaturity.Functional, "4", "5", "5.2", "5.4", "6", "9"),
        Domain("04", "Commercial, Clients & CRM", "Commercial", FunctionalMaturity.Functional, "9.2", "10.4", "18", "20.2"),
        Domain("05", "Facturation & Ventes", "Facturation", FunctionalMaturity.Functional, "8"),
        Domain("06", "PMS / Hébergement", "Hebergement", FunctionalMaturity.Functional, "4.5", "10", "10.1"),
        Domain("07", "Revenue Management & Distribution", "Revenue", FunctionalMaturity.TechnicalPreview, "14.5"),
        Domain("08", "Housekeeping", "Housekeeping", FunctionalMaturity.Functional, "10.2"),
        Domain("09", "Groupes, MICE & Événementiel", "Evenementiel", FunctionalMaturity.Functional, "10.6"),
        Domain("10", "F&B / Restauration", "Restauration", FunctionalMaturity.TechnicalPreview, "11.5", "11.6"),
        Domain("11", "Stocks & Économat", "Stocks", FunctionalMaturity.Functional, "11"),
        Domain("12", "Achats & Fournisseurs", "Achats", FunctionalMaturity.TechnicalPreview, "12", "12.5"),
        Domain("13", "Ressources Humaines & Paie", "RessourcesHumaines", FunctionalMaturity.Functional, "21"),
        Domain("14", "Maintenance & Patrimoine", "Maintenance", FunctionalMaturity.Planned, "13", "23.4"),
        Domain("15", "Qualité, Audit & Contrôle interne", "Qualite", FunctionalMaturity.TechnicalPreview, "22", "22.4", "22.6", "22.8"),
        Domain("16", "Juridique & Conformité", "Juridique", FunctionalMaturity.Planned, "20", "23", "23.2", "23.6"),
        Domain("17", "GED / Gestion documentaire", "Documentaire", FunctionalMaturity.Planned, "27"),
        Domain("18", "PortMaster / Marina", "Marina", FunctionalMaturity.Planned, "26"),
        Domain("19", "Parking & Contrôle d'accès", "Parking", FunctionalMaturity.Planned),
        Domain("20", "Pilotage, KPI & BI", "Pilotage", FunctionalMaturity.Functional, "24", "24.2", "24.4", "25", "25.4"),
        Domain("21", "Intégrations & Matériels", "Integrations", FunctionalMaturity.TechnicalPreview, "13.5", "21.2"),
        Domain("22", "Administration Système", "Systeme", FunctionalMaturity.Functional, "28", "29", "30")
    ];

    private static readonly IReadOnlyDictionary<string, FunctionalDomainDefinition> ByLegacyOrder =
        Domains
            .SelectMany(domain => domain.LegacyModuleOrders.Select(order => (order, domain)))
            .ToDictionary(item => item.order, item => item.domain, StringComparer.Ordinal);

    /// <summary>
    /// L'arbre complet, dans l'ordre d'affichage. Déclaré après <see cref="Domains"/> à
    /// dessein : il en dérive, et l'ordre des initialiseurs statiques suit l'ordre textuel.
    /// </summary>
    public static IReadOnlyList<DomainNode> Tree { get; } = BuildTree();

    // Index de l'arbre, construits une fois : onglet -> chemin primaire, entrée historique ->
    // module d'accueil. Un seul parcours de l'arbre, puis des recherches en O(1).
    private static readonly IReadOnlyDictionary<int, NavigationPath> PrimaryPathByTab = IndexPrimaryPaths(Tree);

    private static readonly IReadOnlyDictionary<string, NavigationModulePlacement> PlacementByLegacyOrder =
        IndexPlacements(Tree);

    static FunctionalArchitectureCatalog()
    {
        if (Domains.Count != ExpectedDomainCount)
        {
            throw new InvalidOperationException($"Le catalogue doit contenir {ExpectedDomainCount} domaines.");
        }

        if (ByLegacyOrder.Count != ExpectedLegacyModuleCount)
        {
            throw new InvalidOperationException($"Le mapping doit couvrir {ExpectedLegacyModuleCount} modules historiques.");
        }

        // L'arbre et la liste des domaines sont deux vues d'une même définition : un module qui
        // absorberait une entrée d'un autre domaine, ou une entrée que personne n'absorbe,
        // serait une incohérence d'édition, connue dès la compilation - pas un cas à tolérer.
        if (PlacementByLegacyOrder.Count != ExpectedLegacyModuleCount)
        {
            throw new InvalidOperationException(
                $"L'arbre doit rattacher les {ExpectedLegacyModuleCount} entrées historiques à un module, une seule fois chacune.");
        }

        foreach (var (order, placement) in PlacementByLegacyOrder)
        {
            if (!string.Equals(placement.Domain.Id, ByLegacyOrder[order].Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"L'entrée historique '{order}' est rattachée au domaine {ByLegacyOrder[order].Id} mais placée sous le module '{placement.Module.Id}'.");
            }
        }
    }

    public static FunctionalDomainDefinition DomainForLegacyOrder(string legacyOrder) =>
        ByLegacyOrder.TryGetValue(legacyOrder, out var domain)
            ? domain
            : throw new KeyNotFoundException($"Le module historique '{legacyOrder}' n'a aucun domaine cible.");

    public static bool TryGetDomainForLegacyOrder(string legacyOrder, out FunctionalDomainDefinition? domain) =>
        ByLegacyOrder.TryGetValue(legacyOrder, out domain);

    /// <summary>
    /// Domaine, module et rang d'affichage d'une entrée du catalogue historique : ce qui
    /// permet à l'accueil de regrouper ses cartes Domaine → Module dans l'ordre de l'arbre.
    /// </summary>
    public static NavigationModulePlacement PlacementForLegacyOrder(string legacyOrder) =>
        PlacementByLegacyOrder.TryGetValue(legacyOrder, out var placement)
            ? placement
            : throw new KeyNotFoundException($"Le module historique '{legacyOrder}' n'est placé sous aucun module de l'arbre.");

    /// <summary>
    /// Chemin primaire d'un onglet de <c>MainTabs</c>, pour le fil d'Ariane. Faux pour l'accueil
    /// et pour tout index qui n'est pas un écran de l'arbre.
    /// </summary>
    public static bool TryGetPrimaryPath(int legacyTabIndex, out NavigationPath? path) =>
        PrimaryPathByTab.TryGetValue(legacyTabIndex, out path);

    /// <summary>Les chemins primaires, un par onglet, dans l'ordre de l'arbre.</summary>
    public static IEnumerable<NavigationPath> PrimaryPaths => EnumeratePaths(Tree).Where(path => !path.Screen.IsAlias);

    /// <summary>Tous les chemins vers un écran, alias compris, dans l'ordre de l'arbre.</summary>
    public static IEnumerable<NavigationPath> EnumeratePaths(IEnumerable<DomainNode> domains)
    {
        foreach (var domain in domains)
        {
            foreach (var module in domain.Modules)
            {
                foreach (var submodule in module.Submodules)
                {
                    foreach (var screen in submodule.Screens)
                    {
                        yield return new NavigationPath(domain, module, submodule, screen);
                    }
                }
            }
        }
    }

    private static IReadOnlyDictionary<int, NavigationPath> IndexPrimaryPaths(IReadOnlyList<DomainNode> tree)
    {
        var index = new Dictionary<int, NavigationPath>();

        foreach (var path in EnumeratePaths(tree))
        {
            if (path.Screen.IsAlias || path.Screen.LegacyTabIndex is not { } tab)
            {
                continue;
            }

            if (!index.TryAdd(tab, path))
            {
                throw new InvalidOperationException(
                    $"L'onglet {tab} a deux chemins primaires : '{index[tab].Screen.Id}' et '{path.Screen.Id}'.");
            }
        }

        return index;
    }

    private static IReadOnlyDictionary<string, NavigationModulePlacement> IndexPlacements(IReadOnlyList<DomainNode> tree)
    {
        var index = new Dictionary<string, NavigationModulePlacement>(StringComparer.Ordinal);
        var rank = 0;

        foreach (var domain in tree)
        {
            foreach (var module in domain.Modules)
            {
                rank++;

                foreach (var order in module.LegacyModuleOrders)
                {
                    if (!index.TryAdd(order, new NavigationModulePlacement(domain, module, rank)))
                    {
                        throw new InvalidOperationException(
                            $"L'entrée historique '{order}' est absorbée par deux modules : '{index[order].Module.Id}' et '{module.Id}'.");
                    }
                }
            }
        }

        return index;
    }

    private static FunctionalDomainDefinition Domain(
        string id,
        string name,
        string iconKey,
        FunctionalMaturity maturity,
        params string[] legacyModuleOrders) =>
        new(id, name, iconKey, maturity, legacyModuleOrders);
}
