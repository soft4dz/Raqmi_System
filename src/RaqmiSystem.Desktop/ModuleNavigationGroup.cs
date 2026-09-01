using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using RaqmiSystem.Application.Navigation;

namespace RaqmiSystem.Desktop;

// Un domaine de la barre laterale : son intitule, et les modules et ecrans que le profil
// peut ouvrir, dans l'ordre de l'arbre. La barre laterale ne liste QUE des ecrans
// ouvrables ; le sommaire complet des 50 modules, planifies et verrouilles compris, reste
// l'ecran d'accueil.
//
// Les groupes sont construits une fois, depuis l'arbre complet, et gardent leur etat
// (section ouverte ou repliee) d'une session a l'autre. Ce qu'ils MONTRENT vient a chaque
// fois de l'arbre elague par NavigationTreeBuilder (permissions du JWT, recherche) : c'est
// Apply qui rejoue cet arbre sur le groupe, sans reconstruire les groupes eux-memes - ce
// qui replierait tout et ferait clignoter le panneau.
//
// Les ModuleTile references sont ceux de l'accueil, pas des copies : un changement de
// module courant (IsActive) se voit des deux cotes sans code de synchronisation.
public sealed class ModuleNavigationGroup : INotifyPropertyChanged
{
    // Onglets dont ce domaine est le chemin primaire : pour ouvrir la section du module
    // courant, meme quand une recherche l'a momentanement vide.
    private readonly IReadOnlySet<int> ownedTabs;
    private bool isExpanded;

    private ModuleNavigationGroup(DomainNode domain, IReadOnlySet<int> ownedTabs)
    {
        Id = domain.Id;
        Name = domain.Label;
        IconKey = domain.IconKey;
        // L'administration systeme est presentee en pied de panneau, hors de la liste
        // defilante : on l'ouvre rarement et jamais dans le flux de la journee.
        IsPinned = domain.Id == "22";
        this.ownedTabs = ownedTabs;
    }

    public string Id { get; }

    public string Name { get; }

    // Cle d'icone du domaine, resolue par ModuleGroupIconConverter.
    public string IconKey { get; }

    // Vrai pour la section presentee en pied de panneau, hors de la liste defilante.
    public bool IsPinned { get; }

    // Ecrans affiches, a plat. Le nom est celui que le gabarit d'en-tete du theme lit pour
    // son compteur (« VisibleModules.Count ») : il compte des ecrans, pas des modules.
    public ObservableCollection<ModuleNavigationScreen> VisibleModules { get; } = [];

    // Les memes ecrans, ranges par module : ce que le panneau rend.
    public ObservableCollection<ModuleNavigationSection> VisibleSections { get; } = [];

    // Section deroulee ou repliee. Liee en TwoWay a l'Expander : un clic sur
    // l'en-tete revient donc ici, et la fenetre peut a son tour ouvrir la section du
    // module qu'elle affiche.
    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (isExpanded == value)
            {
                return;
            }

            isExpanded = value;
            OnPropertyChanged();
        }
    }

    // Faux = aucun ecran a montrer : la section disparait entierement de la barre
    // laterale plutot que d'afficher un en-tete vide.
    public bool HasMatches => VisibleModules.Count > 0;

    /// <summary>
    /// Un groupe par domaine qui possede au moins un ecran ouvrable en chemin primaire,
    /// dans l'ordre de l'arbre. Un domaine entierement planifie, ou qui n'atteint des ecrans
    /// que par alias, n'a rien a montrer dans la barre laterale.
    /// </summary>
    public static IReadOnlyList<ModuleNavigationGroup> Build(IReadOnlyList<DomainNode> tree)
    {
        var groups = new List<ModuleNavigationGroup>();

        foreach (var domain in tree)
        {
            var tabs = FunctionalArchitectureCatalog.EnumeratePaths([domain])
                .Where(path => !path.Screen.IsAlias && path.Screen.LegacyTabIndex is not null)
                .Select(path => path.Screen.LegacyTabIndex!.Value)
                .ToHashSet();

            if (tabs.Count > 0)
            {
                groups.Add(new ModuleNavigationGroup(domain, tabs));
            }
        }

        return groups;
    }

    /// <summary>
    /// Rejoue sur ce groupe le domaine tel que l'elagage l'a laisse (nul = rien a montrer),
    /// et renvoie le nombre d'ecrans retenus.
    /// </summary>
    /// <remarks>
    /// Reconstruction plutot que filtrage en place : une section compte au plus une
    /// demi-douzaine de boutons, et la collection reste ainsi la seule verite de ce qui
    /// est affiche. Un ecran sans tuile de catalogue est ignore : la barre laterale ne
    /// propose que ce que l'accueil connait.
    /// </remarks>
    public int Apply(DomainNode? visibleDomain, Func<int, ModuleTile?> tileForTab)
    {
        VisibleModules.Clear();
        VisibleSections.Clear();

        if (visibleDomain is not null)
        {
            foreach (var module in visibleDomain.Modules)
            {
                var screens = new List<ModuleNavigationScreen>();

                foreach (var screen in module.Submodules.SelectMany(submodule => submodule.Screens))
                {
                    if (screen.LegacyTabIndex is { } tab
                        && screens.All(existing => existing.TabIndex != tab)
                        && tileForTab(tab) is { } tile)
                    {
                        screens.Add(new ModuleNavigationScreen(screen, tile));
                    }
                }

                if (screens.Count == 0)
                {
                    continue;
                }

                VisibleSections.Add(new ModuleNavigationSection(module.Id, module.Label, screens));

                foreach (var screen in screens)
                {
                    VisibleModules.Add(screen);
                }
            }
        }

        OnPropertyChanged(nameof(HasMatches));
        return VisibleModules.Count;
    }

    /// <summary>Vrai si l'onglet est un ecran de ce domaine (chemin primaire).</summary>
    public bool Owns(int tabIndex) => ownedTabs.Contains(tabIndex);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// Un module de la barre laterale : son libelle et ses ecrans ouvrables.
public sealed record ModuleNavigationSection(
    string Id,
    string Label,
    IReadOnlyList<ModuleNavigationScreen> Screens);

// Un ecran de la barre laterale : le noeud de l'arbre (libelle, chemin) et la tuile de
// l'accueil qui porte son etat vivant (module courant, verrouillage). Le bouton lie
// « Tile.NavTag » et « Tile.IsClickable » : les notifications de la tuile traversent le
// chemin de liaison.
public sealed record ModuleNavigationScreen(ScreenNode Screen, ModuleTile Tile)
{
    public string Label => Screen.Label;

    public int TabIndex => Screen.LegacyTabIndex ?? throw new InvalidOperationException(
        $"L'écran '{Screen.Id}' n'a pas d'onglet : il ne peut pas figurer dans la barre latérale.");
}
