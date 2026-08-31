using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace RaqmiSystem.Desktop;

// Une section de la barre laterale : un intitule et les ecrans qu'elle regroupe.
// L'ordre des sections et celui des modules viennent de SidebarLayout ; le panneau
// ne liste QUE des ecrans ouvrables - le sommaire complet des 49 modules,
// planifies compris, reste l'ecran d'accueil.
//
// Les ModuleTile sont ceux de l'accueil, pas des copies : un changement de
// permission (ModuleTile.IsLocked) ou de module courant (IsActive) se voit des
// deux cotes sans code de synchronisation.
public sealed class ModuleNavigationGroup : INotifyPropertyChanged
{
    // Nom, description, famille et numero d'ordre normalises une fois pour toutes :
    // la recherche compare sans accent ni casse, a chaque frappe.
    private sealed record NavigableModule(ModuleTile Tile, string SearchText);

    // Section en cours de composition : le plan pose les sections, le rattrapage du
    // catalogue y ajoute ses ecrans, et seules celles qui portent au moins un module
    // deviennent des ModuleNavigationGroup.
    private sealed record Draft(string Name, string IconKey, bool IsPinned, List<NavigableModule> Modules);

    private readonly IReadOnlyList<NavigableModule> modules;
    private bool isExpanded;

    private ModuleNavigationGroup(
        string name,
        string iconKey,
        bool isPinned,
        IReadOnlyList<NavigableModule> modules)
    {
        Name = name;
        IconKey = iconKey;
        IsPinned = isPinned;
        this.modules = modules;
        VisibleModules = new ObservableCollection<ModuleTile>(modules.Select(module => module.Tile));
    }

    public string Name { get; }

    // Cle d'icone de la section, resolue par ModuleGroupIconConverter.
    public string IconKey { get; }

    // Vrai pour la section presentee en pied de panneau (le parametrage), hors de
    // la liste defilante des sections de travail.
    public bool IsPinned { get; }

    // Tous les modules de la section, filtre de recherche non applique.
    public IEnumerable<ModuleTile> Modules => modules.Select(module => module.Tile);

    // Modules affiches : la section entiere hors recherche, les seuls resultats
    // pendant une recherche.
    public ObservableCollection<ModuleTile> VisibleModules { get; }

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

    // Faux = aucun resultat dans cette section : elle disparait entierement de la
    // barre laterale plutot que d'afficher un en-tete vide.
    public bool HasMatches => VisibleModules.Count > 0;

    /// <summary>
    /// Construit les sections de la barre laterale : celles de <see cref="SidebarLayout"/>
    /// dans leur ordre, puis les ecrans que le plan ne cite pas, rendus a leur famille
    /// de catalogue, et enfin la section epinglee.
    /// </summary>
    /// <remarks>
    /// Un seul module par onglet : le catalogue peut decrire deux modules servis par
    /// le meme ecran (« Audit &amp; controle interne » et « Journalisation &amp;
    /// tracabilite » partagent l'onglet 4), et la barre laterale liste des ecrans,
    /// pas des lignes de catalogue. Le premier du catalogue nomme la ligne.
    /// </remarks>
    public static IReadOnlyList<ModuleNavigationGroup> Build(IEnumerable<ModuleTile> tiles)
    {
        var byTab = new Dictionary<int, ModuleTile>();

        foreach (var tile in tiles)
        {
            if (tile.TabIndex is { } tabIndex)
            {
                byTab.TryAdd(tabIndex, tile);
            }
        }

        var placed = new HashSet<int>();
        var drafts = new List<Draft>();

        foreach (var section in SidebarLayout.Sections)
        {
            var draft = new Draft(section.Name, section.IconKey, section.IsPinned, []);
            drafts.Add(draft);

            foreach (var tabIndex in section.Tabs)
            {
                // Un onglet cite par le plan mais absent du catalogue est ignore,
                // plutot que de poser une ligne qui n'ouvrirait rien. placed garde
                // contre un onglet cite par deux sections : un ecran, une ligne.
                if (byTab.TryGetValue(tabIndex, out var tile) && placed.Add(tabIndex))
                {
                    draft.Modules.Add(Describe(tile));
                }
            }
        }

        // Ecrans livres depuis la derniere revision du plan : ils reprennent leur
        // famille de catalogue. Si une section porte deja ce nom ils la rejoignent -
        // sans quoi la barre laterale afficherait deux en-tetes « Exploitation ».
        // Sinon une section nait juste avant celle epinglee en pied : rien ne
        // disparait de la navigation parce qu'une table n'a pas suivi.
        foreach (var family in byTab
                     .Where(entry => !placed.Contains(entry.Key))
                     .OrderBy(entry => entry.Key)
                     .GroupBy(entry => entry.Value.Group))
        {
            var draft = drafts.Find(candidate => candidate.Name == family.Key);

            if (draft is null)
            {
                draft = new Draft(family.Key, ModuleCatalog.GroupIconKey(family.Key), IsPinned: false, []);
                var pinned = drafts.FindIndex(candidate => candidate.IsPinned);
                drafts.Insert(pinned < 0 ? drafts.Count : pinned, draft);
            }

            draft.Modules.AddRange(family.Select(entry => Describe(entry.Value)));
        }

        return drafts
            .Where(draft => draft.Modules.Count > 0)
            .Select(draft => new ModuleNavigationGroup(
                draft.Name,
                draft.IconKey,
                draft.IsPinned,
                draft.Modules))
            .ToList();
    }

    /// <summary>
    /// Applique la recherche a cette section et renvoie le nombre de modules retenus.
    /// Une saisie vide retablit la section complete.
    /// </summary>
    public int ApplySearch(string? query)
    {
        var normalized = string.IsNullOrWhiteSpace(query) ? null : ModuleTile.NormalizeForSearch(query);

        // Reconstruction plutot que filtrage en place : une section compte au plus une
        // demi-douzaine de boutons, et la collection reste ainsi la seule verite de ce
        // qui est affiche.
        VisibleModules.Clear();

        foreach (var module in modules)
        {
            if (normalized is null || module.SearchText.Contains(normalized, StringComparison.Ordinal))
            {
                VisibleModules.Add(module.Tile);
            }
        }

        OnPropertyChanged(nameof(HasMatches));
        return VisibleModules.Count;
    }

    /// <summary>
    /// Marque le module de l'onglet donne comme courant et renvoie vrai si cette
    /// section est celle qui le contient.
    /// </summary>
    public bool SetActiveTab(int tabIndex)
    {
        var owns = false;

        foreach (var module in modules)
        {
            var isActive = module.Tile.TabIndex == tabIndex;
            module.Tile.IsActive = isActive;
            owns |= isActive;
        }

        return owns;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static NavigableModule Describe(ModuleTile tile) => new(tile, tile.SearchText);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
