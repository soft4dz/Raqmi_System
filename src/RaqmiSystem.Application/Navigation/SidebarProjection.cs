namespace RaqmiSystem.Application.Navigation;

/// <summary>
/// Une rangée de la barre latérale sous un en-tête de domaine : un libellé de section
/// (<see cref="SidebarSectionRow"/>) ou un écran ouvrable (<see cref="SidebarScreenRow"/>).
/// Le module d'origine est porté par chaque rangée : le client n'a pas à le retrouver.
/// </summary>
public abstract record SidebarRow(ModuleNode Module);

/// <summary>
/// Libellé de section : le nom d'un module qui regroupe au moins deux écrans retenus. Il
/// n'est pas cliquable ; il ne sert qu'à séparer (« DASHBOARDS » au-dessus des trois tableaux
/// de bord). Un module à écran unique n'en produit jamais : son écran est rendu à plat.
/// </summary>
public sealed record SidebarSectionRow(ModuleNode Module) : SidebarRow(Module)
{
    public string Label => Module.Label;
}

/// <summary>
/// Un écran ouvrable, à plat sous son domaine. <see cref="TabIndex"/> est garanti : la
/// projection ne retient que les écrans qui ont un onglet, un écran sans onglet n'ayant rien
/// à ouvrir.
/// </summary>
public sealed record SidebarScreenRow(ModuleNode Module, ScreenNode Screen, int TabIndex) : SidebarRow(Module)
{
    public string Label => Screen.Label;
}

/// <summary>
/// Un domaine tel que la barre latérale le présente : le nœud élagué (libellés, icône,
/// modules) et ses rangées à plat, dans l'ordre de l'arbre.
/// </summary>
public sealed record SidebarDomain(DomainNode Domain, IReadOnlyList<SidebarRow> Rows)
{
    public string Id => Domain.Id;

    /// <summary>Nom complet : fil d'Ariane, info-bulle, nom d'automatisation.</summary>
    public string Label => Domain.Label;

    /// <summary>Libellé court : le texte de l'en-tête de domaine.</summary>
    public string ShortLabel => Domain.ShortLabel;

    public string IconKey => Domain.IconKey;

    /// <summary>Mon Espace : rendu en tête de panneau avec ses écrans, jamais dans la liste.</summary>
    public bool IsHome => SidebarProjection.IsHome(Domain.Id);

    /// <summary>Administration Système : épinglée en pied de panneau, hors de la liste défilante.</summary>
    public bool IsPinned => SidebarProjection.IsPinned(Domain.Id);

    /// <summary>Les écrans seuls, dans l'ordre des rangées : le compteur de résultats de la recherche.</summary>
    public IEnumerable<SidebarScreenRow> Screens => Rows.OfType<SidebarScreenRow>();

    public int ScreenCount => Rows.Count(row => row is SidebarScreenRow);

    /// <summary>Vrai si l'onglet est l'un des écrans de ce domaine.</summary>
    public bool Owns(int tabIndex) => Screens.Any(screen => screen.TabIndex == tabIndex);
}

/// <summary>
/// Les trois strates de la barre latérale, calculées depuis un arbre élagué : Mon Espace en
/// tête (nul si le profil n'y ouvre aucun écran), la liste défilante des domaines 02 → 21,
/// et le pied épinglé (nul pour les profils sans écran d'administration système, dont le
/// Directeur d'unité).
/// </summary>
public sealed record SidebarLayout(SidebarDomain? Home, IReadOnlyList<SidebarDomain> Domains, SidebarDomain? Pinned);

/// <summary>
/// Projection pure de l'arbre élagué vers ce que la barre latérale affiche : deux niveaux,
/// Domaine › Écran, le module ne survivant que comme séparateur. Aucune règle métier ici : les
/// permissions, la maturité et la recherche ont déjà été appliquées par
/// <see cref="NavigationTreeBuilder"/> ; cette classe ne fait que réordonner ce qui reste.
/// Sans dépendance WPF, pour que la règle d'aplatissement soit testée là où elle vit.
/// </summary>
public static class SidebarProjection
{
    /// <summary>Domaine rendu en tête de panneau, avec ses écrans, jamais dans la liste.</summary>
    public const string HomeDomainId = "01";

    /// <summary>Domaine épinglé en pied de panneau : on l'ouvre rarement, jamais dans le flux de la journée.</summary>
    public const string PinnedDomainId = "22";

    /// <summary>Nombre d'écrans à partir duquel un module mérite un libellé de section.</summary>
    public const int SectionLabelThreshold = 2;

    public static bool IsHome(string domainId) => string.Equals(domainId, HomeDomainId, StringComparison.Ordinal);

    public static bool IsPinned(string domainId) => string.Equals(domainId, PinnedDomainId, StringComparison.Ordinal);

    /// <summary>
    /// Répartit les domaines d'un arbre élagué dans les trois strates, dans l'ordre de l'arbre.
    /// Un domaine sans écran ouvrable (possible avec le filtre de l'accueil, qui garde les
    /// nœuds planifiés) n'a pas d'en-tête : il n'y aurait rien à déplier.
    /// </summary>
    public static SidebarLayout Project(NavigationTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        SidebarDomain? home = null;
        SidebarDomain? pinned = null;
        var domains = new List<SidebarDomain>();

        foreach (var domain in tree.Domains)
        {
            var projected = ProjectDomain(domain);

            if (projected.ScreenCount == 0)
            {
                continue;
            }

            if (projected.IsHome)
            {
                home = projected;
            }
            else if (projected.IsPinned)
            {
                pinned = projected;
            }
            else
            {
                domains.Add(projected);
            }
        }

        return new SidebarLayout(home, domains, pinned);
    }

    public static SidebarDomain ProjectDomain(DomainNode domain) => new(domain, FlattenForSidebar(domain));

    /// <summary>
    /// Aplatit un domaine élagué en rangées : pour chaque module, ses écrans dans l'ordre de
    /// l'arbre, précédés d'un libellé de section seulement si le module en retient au moins
    /// <see cref="SectionLabelThreshold"/>. Le sous-module n'apparaît jamais : il vit dans le
    /// fil d'Ariane. Un onglet n'est listé qu'une fois par domaine, même si l'arbre reçu garde
    /// des alias : une barre qui montrerait deux fois le même écran tromperait.
    /// </summary>
    public static IReadOnlyList<SidebarRow> FlattenForSidebar(DomainNode domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        var rows = new List<SidebarRow>();
        var listedTabs = new HashSet<int>();

        foreach (var module in domain.Modules)
        {
            var screens = new List<SidebarScreenRow>();

            foreach (var screen in module.Submodules.SelectMany(submodule => submodule.Screens))
            {
                if (screen.LegacyTabIndex is { } tab && listedTabs.Add(tab))
                {
                    screens.Add(new SidebarScreenRow(module, screen, tab));
                }
            }

            if (screens.Count == 0)
            {
                continue;
            }

            if (screens.Count >= SectionLabelThreshold)
            {
                rows.Add(new SidebarSectionRow(module));
            }

            rows.AddRange(screens);
        }

        return rows;
    }
}
