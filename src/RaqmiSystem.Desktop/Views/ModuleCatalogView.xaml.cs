using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Shapes;
using RaqmiSystem.Application.Navigation;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Catalogue des modules : la seconde section de l'onglet 0.
/// </summary>
/// <remarks>
/// Extraite telle quelle de l'ancien onglet Accueil, avec les corrections dues depuis
/// <c>navigation-shell.md</c> § 5.1 : regroupement par domaine avec icone et badge de
/// maturite dans l'en-tete, filtre Maturite, noms accessibles sur les cartes, et une
/// recherche qui descend jusqu'aux ecrans et aux noeuds planifies de l'arbre.
///
/// La vue ne fait aucun appel reseau : ses chiffres sont ceux du binaire
/// (<see cref="ModuleCatalog"/>), et elle le dit. Elle ne connait pas
/// <c>MainWindow</c> : la fenetre lui prete les 50 tuiles, les cles du profil et un
/// delegue de navigation, exactement comme aux autres vues son <c>ModuleViewContext</c>.
/// </remarks>
public partial class ModuleCatalogView : UserControl
{
    private IReadOnlyList<ModuleTile> tiles = [];
    private Func<IReadOnlySet<string>> grantedKeys = () => new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private ICollectionView? catalogView;
    private ModuleStatus? statusFilter;
    private string? priorityFilter;
    private string? domainFilter;
    private FunctionalMaturity? maturityFilter;

    // Recherche deja normalisee (sans accent ni casse) pour ne pas la recalculer a
    // chaque tuile testee. Null = pas de recherche en cours.
    private string? searchFilter;

    public ModuleCatalogView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Ouverture d'un ecran demandee par une carte ou un resultat de recherche. La vue
    /// ne navigue pas elle-meme : elle demande, la fenetre garde (<c>CanOpenModule</c>).
    /// </summary>
    public event Action<int>? NavigateRequested;

    /// <summary>
    /// Prete a la vue ce qu'elle ne peut pas construire seule : les 50 tuiles partagees
    /// avec la barre laterale et les cles du profil (relues a chaque recherche : elles
    /// changent a chaque connexion).
    /// </summary>
    public void Initialize(IReadOnlyList<ModuleTile> moduleTiles, Func<IReadOnlySet<string>> permissionKeys)
    {
        ArgumentNullException.ThrowIfNull(moduleTiles);
        ArgumentNullException.ThrowIfNull(permissionKeys);

        tiles = moduleTiles;
        grantedKeys = permissionKeys;

        FunctionalDomainItemsControl.ItemsSource = FunctionalDomainOption.Build(tiles);
        BuildCatalogView();
        RefreshProgress();
        RefreshEmptyDomains();
        RefreshEmptyState();
    }

    /// <summary>Donne le focus au champ de recherche (Ctrl+K, Ctrl+F).</summary>
    public void FocusSearch()
    {
        HomeSearchTextBox.Focus();
        HomeSearchTextBox.SelectAll();
    }

    /// <summary>
    /// Rejoue les filtres apres un changement de permissions : les cadenas des cartes
    /// sont poses par <c>ApplyModulePermissions</c> sur les tuiles partagees, mais la
    /// liste des resultats de recherche, elle, doit etre recalculee.
    /// </summary>
    public void RefreshPermissions()
    {
        RefreshSearchResults();
    }

    // Regroupement PAR DOMAINE (22 en-tetes) et non plus par couple domaine-module : le
    // module reste lisible en pied de carte. Le tri par rang de module donne l'ordre de
    // l'arbre entre les domaines et a l'interieur de chacun ; le rang de catalogue
    // departage les cartes d'un meme module.
    private void BuildCatalogView()
    {
        var source = new CollectionViewSource { Source = tiles };
        source.GroupDescriptions.Add(new PropertyGroupDescription(
            nameof(ModuleTile.FunctionalDomainId),
            new HomeCatalogDomainConverter()));

        source.SortDescriptions.Add(new SortDescription(nameof(ModuleTile.HomeGroupRank), ListSortDirection.Ascending));
        source.SortDescriptions.Add(new SortDescription(nameof(ModuleTile.CatalogIndex), ListSortDirection.Ascending));

        catalogView = source.View;
        catalogView.Filter = Matches;
        ModuleCatalogItemsControl.ItemsSource = catalogView;
    }

    // Compteurs par statut, largeurs proportionnelles de la barre segmentee, libelles de
    // synthese, puis la seconde lecture : les domaines par maturite. Les deux echelles ne
    // s'additionnent jamais, d'ou deux lignes distinctes.
    private void RefreshProgress()
    {
        var total = ModuleCatalog.Entries.Count;
        var available = ModuleCatalog.CountOf(ModuleStatus.Disponible);
        var apiReady = ModuleCatalog.CountOf(ModuleStatus.ApiPrete);
        var partial = ModuleCatalog.CountOf(ModuleStatus.Partiel);
        var planned = ModuleCatalog.CountOf(ModuleStatus.Planifie);

        ModuleCountAvailableTextBlock.Text = available.ToString(CultureInfo.CurrentCulture);
        ModuleCountApiTextBlock.Text = apiReady.ToString(CultureInfo.CurrentCulture);
        ModuleCountPartialTextBlock.Text = partial.ToString(CultureInfo.CurrentCulture);
        ModuleCountPlannedTextBlock.Text = planned.ToString(CultureInfo.CurrentCulture);

        ModuleProgressAvailableColumn.Width = new GridLength(available, GridUnitType.Star);
        ModuleProgressApiColumn.Width = new GridLength(apiReady, GridUnitType.Star);
        ModuleProgressPartialColumn.Width = new GridLength(partial, GridUnitType.Star);
        ModuleProgressPlannedColumn.Width = new GridLength(planned, GridUnitType.Star);

        var availableShare = total == 0 ? 0 : (int)Math.Round(available * 100d / total);
        ModuleProgressHeadlineTextBlock.Text = $"{availableShare} % du périmètre déjà utilisable";

        // Les quatre statuts restent distincts dans la legende : les additionner
        // (« livres cote serveur ») surestimerait le travail fait, car « Partiel »
        // recouvre des situations tres differentes.
        ModuleProgressCaptionTextBlock.Text =
            $"{available} modules disponibles sur {total}  ·  {apiReady} avec API livrée, écran à venir  ·  {partial} partiellement couverts  ·  {planned} planifiés";

        BuildDomainMaturityLine();
    }

    // « 11 fonctionnels · 5 aperçus techniques · 6 planifiés · 0 prêt pour la production ».
    // Construite en code plutot qu'en XAML : les quatre niveaux viennent de l'enumeration,
    // et un niveau ajoute un jour doit apparaitre sans rouvrir le gabarit.
    private void BuildDomainMaturityLine()
    {
        DomainMaturityPanel.Children.Clear();

        var domains = FunctionalArchitectureCatalog.Domains;

        foreach (var maturity in Enum.GetValues<FunctionalMaturity>())
        {
            var count = domains.Count(domain => domain.Maturity == maturity);
            var label = FunctionalMaturityMapper.Label(maturity);

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 18, 0)
            };

            row.Children.Add(new Ellipse
            {
                Style = TryFindResource($"MaturityDot.{maturity}") as Style ?? TryFindResource("MaturityDot") as Style,
                VerticalAlignment = VerticalAlignment.Center
            });

            var text = new TextBlock
            {
                Text = $"{count.ToString(CultureInfo.CurrentCulture)} {label.ToLowerInvariant()}",
                Style = TryFindResource("CaptionText") as Style,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };

            // Une ellipse n'a pas de nom accessible : la phrase est portee par le texte,
            // qui dit deja le niveau. Le point ne fait que doubler la couleur.
            AutomationProperties.SetName(text, $"{count} domaines : {label}");

            row.Children.Add(text);
            DomainMaturityPanel.Children.Add(row);
        }

        AutomationProperties.SetName(DomainMaturityPanel, "Domaines par maturité");
    }

    // Un domaine de la cartographie cible sans entree historique garde son en-tete : le
    // cacher ferait croire qu'il n'existe pas. Il ne repond a aucun filtre, donc il
    // disparait des qu'un filtre est actif.
    private void RefreshEmptyDomains()
    {
        var covered = tiles.Select(tile => tile.FunctionalDomainId).ToHashSet(StringComparer.Ordinal);

        EmptyDomainsItemsControl.ItemsSource = FunctionalArchitectureCatalog.Domains
            .Where(domain => !covered.Contains(domain.Id))
            .Select(HomeCatalogDomainHeader.From)
            .ToList();
    }

    // Filtre courant de la vue : domaine, recherche, statut, priorite, maturite. Les
    // criteres se croisent - chercher « tva » dans les seuls modules disponibles est une
    // question legitime, et remplacer un critere par un autre y repondrait mal.
    private bool Matches(object item)
    {
        if (item is not ModuleTile tile)
        {
            return false;
        }

        if (domainFilter is not null
            && !string.Equals(tile.FunctionalDomainId, domainFilter, StringComparison.Ordinal))
        {
            return false;
        }

        if (searchFilter is { } search && !tile.SearchText.Contains(search, StringComparison.Ordinal))
        {
            return false;
        }

        if (statusFilter is { } status && tile.Status != status)
        {
            return false;
        }

        if (maturityFilter is { } maturity && tile.FunctionalMaturity != maturity)
        {
            return false;
        }

        return priorityFilter is null
            || string.Equals(tile.Priority, priorityFilter, StringComparison.Ordinal);
    }

    private void FunctionalDomainFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FunctionalDomainOption option })
        {
            return;
        }

        domainFilter = option.Id;

        // Les puces sont cochees pendant InitializeComponent, avant que le XAML ne soit
        // entierement charge : ce premier evenement est sans objet.
        if (FunctionalDomainBreadcrumbTextBlock is not null)
        {
            FunctionalDomainBreadcrumbTextBlock.Text = option.Id is null
                ? "Tous les domaines  →  modules"
                : $"{option.Id}  {option.Name}  →  modules";
        }

        ApplyFilters();
    }

    private void HomeSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = HomeSearchTextBox.Text;

        searchFilter = string.IsNullOrWhiteSpace(query) ? null : NavigationSearch.Normalize(query);

        ClearHomeSearchButton.Visibility = query.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshSearchResults();
        ApplyFilters();
    }

    // Echap efface la saisie sans quitter le champ - meme geste que dans la barre
    // laterale. Entree ouvre le premier resultat ouvrable : la recherche sert a aller
    // quelque part, pas a contempler une liste.
    private void HomeSearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && HomeSearchTextBox.Text.Length > 0)
        {
            HomeSearchTextBox.Clear();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        if (SearchResultsItemsControl.ItemsSource is IEnumerable<HomeCatalogSearchResult> results
            && results.FirstOrDefault(result => result.IsOpenable) is { TabIndex: { } tab })
        {
            NavigateRequested?.Invoke(tab);
            e.Handled = true;
        }
    }

    private void ClearHomeSearchButton_Click(object sender, RoutedEventArgs e)
    {
        HomeSearchTextBox.Clear();
        HomeSearchTextBox.Focus();
    }

    private void ModuleStatusFilterChip_Checked(object sender, RoutedEventArgs e)
    {
        // Les puces par defaut sont cochees pendant InitializeComponent, donc avant que
        // la vue ne recoive ses tuiles : ces premiers evenements sont sans objet.
        if (catalogView is null)
        {
            return;
        }

        var tag = (sender as RadioButton)?.Tag as string;
        statusFilter = Enum.TryParse<ModuleStatus>(tag, out var status) ? status : null;
        ApplyFilters();
    }

    private void ModulePriorityFilterChip_Checked(object sender, RoutedEventArgs e)
    {
        if (catalogView is null)
        {
            return;
        }

        var tag = (sender as RadioButton)?.Tag as string;
        priorityFilter = string.IsNullOrEmpty(tag) ? null : tag;
        ApplyFilters();
    }

    private void ModuleMaturityFilterChip_Checked(object sender, RoutedEventArgs e)
    {
        if (catalogView is null)
        {
            return;
        }

        var tag = (sender as RadioButton)?.Tag as string;
        maturityFilter = Enum.TryParse<FunctionalMaturity>(tag, out var maturity) ? maturity : null;
        ApplyFilters();
    }

    private void ModuleTile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ModuleTile { TabIndex: { } tab })
        {
            NavigateRequested?.Invoke(tab);
        }
    }

    private void SearchResult_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is HomeCatalogSearchResult { IsOpenable: true, TabIndex: { } tab })
        {
            NavigateRequested?.Invoke(tab);
        }
    }

    // La recherche universelle : l'arbre elague sur la saisie, construit avec TOUTES les
    // cles pour que les ecrans non autorises restent listes, cadenasses. Comparer ensuite
    // chaque ecran aux cles du profil pose le cadenas.
    private void RefreshSearchResults()
    {
        if (searchFilter is null)
        {
            SearchResultsPanel.Visibility = Visibility.Collapsed;
            SearchResultsItemsControl.ItemsSource = null;
            return;
        }

        var tree = NavigationTreeBuilder.Build(
            FunctionalArchitectureCatalog.Tree,
            AllPermissionKeys,
            NavigationFilter.Home with { SearchText = HomeSearchTextBox.Text, IncludeAliases = true });

        var results = HomeCatalogSearchResult.From(tree, grantedKeys());

        SearchResultsItemsControl.ItemsSource = results;
        SearchResultsPanel.Visibility = results.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SearchResultsHeaderTextBlock.Text = results.Count == 1
            ? "Sous-modules et écrans (1)"
            : $"Sous-modules et écrans ({results.Count})";

        AutomationProperties.SetName(SearchResultsHeaderTextBlock, $"{results.Count} sous-modules et écrans trouvés");
    }

    private void ApplyFilters()
    {
        catalogView?.Refresh();

        var filtered = domainFilter is not null
            || searchFilter is not null
            || statusFilter is not null
            || priorityFilter is not null
            || maturityFilter is not null;

        EmptyDomainsItemsControl.Visibility = filtered ? Visibility.Collapsed : Visibility.Visible;
        RefreshEmptyState();
    }

    // Un etat vide utile dit comment en sortir (charte 3.5). Ici la sortie depend de ce
    // qui a vide la liste : effacer la recherche, ou elargir les puces.
    private void RefreshEmptyState()
    {
        var isEmpty = catalogView is null || catalogView.IsEmpty;
        ModuleCatalogEmptyTextBlock.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;

        if (!isEmpty)
        {
            return;
        }

        ModuleCatalogEmptyTextBlock.Text = searchFilter is null
            ? $"Aucun module ne correspond à ces filtres. Revenez à « Tous » pour voir les {ModuleCatalog.Entries.Count} modules."
            : $"Aucun module ne correspond à « {HomeSearchTextBox.Text.Trim()} ». Échap efface la recherche.";
    }

    // L'arbre de recherche est construit sur toutes les cles connues : la recherche du
    // catalogue montre tout, cadenas compris, la ou la barre laterale elague.
    private static readonly IReadOnlySet<string> AllPermissionKeys =
        PermissionCatalog.All.Select(permission => permission.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
