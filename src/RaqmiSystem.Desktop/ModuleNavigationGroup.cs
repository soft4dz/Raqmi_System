using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using RaqmiSystem.Application.Navigation;

namespace RaqmiSystem.Desktop;

// Un domaine de la barre laterale : son intitule (complet et court), et les ecrans que le
// profil peut ouvrir, a plat, dans l'ordre de l'arbre. La barre laterale ne liste QUE des
// ecrans ouvrables ; le sommaire complet des 50 modules, planifies et verrouilles compris,
// reste le catalogue de l'accueil.
//
// Deux niveaux seulement, Domaine › Écran : le module ne survit que comme libelle de
// section, et seulement quand il regroupe au moins deux ecrans retenus. Cette regle
// d'aplatissement n'est pas ecrite ici : elle vit dans SidebarProjection (Application),
// ou elle est testee ; le groupe ne fait que rejouer ses rangees dans une collection
// observable.
//
// Les groupes sont construits une fois, depuis l'arbre complet, et gardent leur etat
// (section ouverte ou repliee) d'une session a l'autre - il est meme memorise par poste
// (DesktopSettings.SidebarExpandedDomains). Ce qu'ils MONTRENT vient a chaque fois de
// l'arbre elague par NavigationTreeBuilder (permissions du JWT, recherche) : c'est Apply
// qui rejoue cet arbre sur le groupe, sans reconstruire les groupes eux-memes - ce qui
// replierait tout et ferait clignoter le panneau.
//
// Les ModuleTile references sont ceux de l'accueil, pas des copies : un changement de
// module courant (IsActive) se voit des deux cotes sans code de synchronisation.
public sealed class ModuleNavigationGroup : INotifyPropertyChanged
{
    // Onglets dont ce domaine est le chemin primaire : pour ouvrir la section du module
    // courant, meme quand une recherche l'a momentanement vide.
    private readonly IReadOnlySet<int> ownedTabs;
    private bool isExpanded;
    private bool containsActiveScreen;
    private bool showResultCount;
    private int screenCount;

    private ModuleNavigationGroup(SidebarDomain domain)
    {
        Id = domain.Id;
        Name = domain.Label;
        ShortLabel = domain.ShortLabel;
        IconKey = domain.IconKey;
        IsHome = domain.IsHome;
        IsPinned = domain.IsPinned;
        ownedTabs = domain.Screens.Select(screen => screen.TabIndex).ToHashSet();
    }

    public string Id { get; }

    // Nom complet du domaine : fil d'Ariane, info-bulle, nom d'automatisation.
    public string Name { get; }

    // Libelle court porte par le catalogue (« Admin & Socle ERP ») : le texte de l'en-tete.
    // Contraction du nom officiel, jamais un renommage - le nom complet reste a un survol.
    public string ShortLabel { get; }

    // « 06 · PMS / Hébergement » : l'info-bulle de l'en-tete quand le libelle court est
    // tronque, avec le numero de domaine que la rangee ne montre pas.
    public string FullTitle => $"{Id} · {Name}";

    // Cle d'icone du domaine, resolue par ModuleGroupIconConverter.
    public string IconKey { get; }

    // Vrai pour Mon Espace (domaine 01) : rendu en tete de panneau, sous la rangee fixe
    // « Mon Espace », jamais dans la liste - sinon il y figurerait deux fois.
    public bool IsHome { get; }

    // Vrai pour la section presentee en pied de panneau, hors de la liste defilante :
    // l'administration systeme, qu'on ouvre rarement et jamais dans le flux de la journee.
    public bool IsPinned { get; }

    // Ce que le panneau rend sous l'en-tete : des ModuleNavigationScreen et, devant un
    // module qui en retient au moins deux, un ModuleNavigationSectionLabel. Le gabarit
    // choisit par type (deux DataTemplate implicites).
    public ObservableCollection<object> Rows { get; } = [];

    // Nombre d'ecrans retenus : le compteur affiche pendant une recherche.
    public int ScreenCount
    {
        get => screenCount;
        private set
        {
            if (screenCount == value)
            {
                return;
            }

            screenCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasMatches));
        }
    }

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

    // Vrai quand l'ecran affiche est l'un des ecrans de ce domaine : l'en-tete porte alors
    // son icone en accent et, s'il est replie, un point - la position se lit meme quand la
    // rangee active est cachee. Pose par la fenetre a chaque changement d'onglet.
    public bool ContainsActiveScreen
    {
        get => containsActiveScreen;
        set
        {
            if (containsActiveScreen == value)
            {
                return;
            }

            containsActiveScreen = value;
            OnPropertyChanged();
        }
    }

    // Vrai pendant une recherche seulement : au repos, un compteur sur chaque en-tete
    // n'est que du bruit, en recherche il dit combien d'ecrans repondent.
    public bool ShowResultCount
    {
        get => showResultCount;
        set
        {
            if (showResultCount == value)
            {
                return;
            }

            showResultCount = value;
            OnPropertyChanged();
        }
    }

    // Faux = aucun ecran a montrer : la section disparait entierement de la barre
    // laterale plutot que d'afficher un en-tete vide.
    public bool HasMatches => ScreenCount > 0;

    /// <summary>
    /// Un groupe par domaine qui possede au moins un ecran ouvrable, dans l'ordre de
    /// l'arbre : Mon Espace (<see cref="IsHome"/>), les domaines de la liste, puis
    /// l'administration systeme (<see cref="IsPinned"/>). L'arbre attendu est celui que
    /// le profil le plus large peut ouvrir (toutes les cles, filtre de la barre) : un domaine
    /// entierement planifie, ou qui n'atteint des ecrans que par alias, n'a rien a montrer.
    /// </summary>
    public static IReadOnlyList<ModuleNavigationGroup> Build(NavigationTree openableTree)
    {
        var layout = SidebarProjection.Project(openableTree);
        var groups = new List<ModuleNavigationGroup>();

        if (layout.Home is { } home)
        {
            // Mon Espace n'a pas d'en-tete repliable : ses ecrans sont toujours visibles
            // sous la rangee fixe, donc le groupe reste deplie une fois pour toutes.
            groups.Add(new ModuleNavigationGroup(home) { IsExpanded = true });
        }

        groups.AddRange(layout.Domains.Select(domain => new ModuleNavigationGroup(domain)));

        if (layout.Pinned is { } pinned)
        {
            groups.Add(new ModuleNavigationGroup(pinned));
        }

        return groups;
    }

    /// <summary>
    /// Rejoue sur ce groupe le domaine tel que l'elagage l'a laisse (nul = rien a montrer),
    /// et renvoie le nombre d'ecrans retenus.
    /// </summary>
    /// <remarks>
    /// Reconstruction plutot que filtrage en place : une section compte au plus une
    /// demi-douzaine de rangees, et la collection reste ainsi la seule verite de ce qui
    /// est affiche. Un ecran sans tuile de catalogue est ignore : la barre laterale ne
    /// propose que ce que l'accueil connait. Le libelle de section n'est emis que si le
    /// module garde au moins deux ecrans APRES ce filtre : il n'y a jamais de titre
    /// au-dessus d'un ecran seul.
    /// </remarks>
    public int Apply(DomainNode? visibleDomain, Func<int, ModuleTile?> tileForTab)
    {
        Rows.Clear();
        var count = 0;

        if (visibleDomain is not null)
        {
            foreach (var row in SidebarProjection.FlattenForSidebar(visibleDomain))
            {
                switch (row)
                {
                    case SidebarSectionRow section:
                        Rows.Add(new ModuleNavigationSectionLabel(section.Label));
                        break;
                    case SidebarScreenRow screen when tileForTab(screen.TabIndex) is { } tile:
                        Rows.Add(new ModuleNavigationScreen(screen.Screen, tile));
                        count++;
                        break;
                }
            }

            RemoveOrphanSectionLabels();
        }

        ScreenCount = count;
        return count;
    }

    /// <summary>Vrai si l'onglet est un ecran de ce domaine (chemin primaire).</summary>
    public bool Owns(int tabIndex) => ownedTabs.Contains(tabIndex);

    public event PropertyChangedEventHandler? PropertyChanged;

    // Un ecran sans tuile de catalogue ayant ete ignore, un libelle de section peut se
    // retrouver au-dessus d'un seul ecran, ou d'aucun : on le retire, la regle « separateur
    // seulement devant deux ecrans ou plus » vaut pour ce qui est affiche.
    private void RemoveOrphanSectionLabels()
    {
        for (var index = Rows.Count - 1; index >= 0; index--)
        {
            if (Rows[index] is not ModuleNavigationSectionLabel)
            {
                continue;
            }

            var screensBelow = 0;

            for (var next = index + 1; next < Rows.Count && Rows[next] is ModuleNavigationScreen; next++)
            {
                screensBelow++;
            }

            if (screensBelow < SidebarProjection.SectionLabelThreshold)
            {
                Rows.RemoveAt(index);
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// Libelle de section de la barre laterale : le nom d'un module qui regroupe au moins deux
// ecrans retenus. Non cliquable, non focalisable : il separe, il n'ouvre rien.
public sealed record ModuleNavigationSectionLabel(string Label)
{
    // En majuscules, comme un intertitre : WPF n'a pas de transformation de casse, le
    // texte affiche est donc calcule ici, dans la culture du poste.
    public string DisplayText => Label.ToUpper(CultureInfo.CurrentCulture);
}

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
