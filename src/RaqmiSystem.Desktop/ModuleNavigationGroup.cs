using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using RaqmiSystem.Application.Navigation;

namespace RaqmiSystem.Desktop;

// Une section de la barre laterale : un intitule et les ecrans qu'elle regroupe.
// L'ordre des sections vient du catalogue fonctionnel cible ; le panneau ne liste
// QUE des écrans ouvrables - le sommaire complet des 50 modules,
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
    /// Construit les sections dans l'ordre des 22 domaines fonctionnels cibles, puis
    /// rattache chaque écran historique par son numéro de catalogue stable.
    /// </summary>
    /// <remarks>
    /// Un seul module par onglet : le catalogue peut decrire deux modules servis par
    /// le meme ecran (« Audit &amp; controle interne » et « Journalisation &amp;
    /// tracabilite » partagent l'onglet 4), et la barre laterale liste des ecrans,
    /// pas des lignes de catalogue. Le premier du catalogue nomme la ligne.
    /// </remarks>
    public static IReadOnlyList<ModuleNavigationGroup> Build(IEnumerable<ModuleTile> tiles)
    {
        var tileList = tiles.ToList();
        var byOrder = tileList.ToDictionary(tile => tile.Order, StringComparer.Ordinal);
        var placedTabs = new HashSet<int>();
        var drafts = new List<Draft>();

        foreach (var domain in FunctionalArchitectureCatalog.Domains)
        {
            var draft = new Draft(domain.Name, domain.IconKey, domain.Id == "22", []);

            foreach (var order in domain.LegacyModuleOrders)
            {
                if (byOrder.TryGetValue(order, out var tile)
                    && tile.TabIndex is { } tabIndex
                    && placedTabs.Add(tabIndex))
                {
                    draft.Modules.Add(Describe(tile));
                }
            }

            if (draft.Modules.Count > 0)
            {
                drafts.Add(draft);
            }
        }

        // Garde de compatibilite : un ecran ajoute avant son rattachement explicite
        // reste visible. Le test de couverture doit normalement rendre ce chemin vide.
        foreach (var family in tileList
                     .Where(tile => tile.TabIndex is { } tabIndex && !placedTabs.Contains(tabIndex))
                     .OrderBy(tile => tile.TabIndex)
                     .GroupBy(tile => tile.Group))
        {
            var draft = new Draft(family.Key, family.First().GroupIconKey, IsPinned: false, []);

            foreach (var tile in family)
            {
                if (tile.TabIndex is { } tabIndex && placedTabs.Add(tabIndex))
                {
                    draft.Modules.Add(Describe(tile));
                }
            }

            var pinned = drafts.FindIndex(candidate => candidate.IsPinned);
            drafts.Insert(pinned < 0 ? drafts.Count : pinned, draft);
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
            if (!module.Tile.IsLocked
                && (normalized is null || module.SearchText.Contains(normalized, StringComparison.Ordinal)))
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
