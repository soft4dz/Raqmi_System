using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using RaqmiSystem.Application.Navigation;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Desktop;

/// <summary>
/// Navigation de la fenetre principale : l'accueil (cartes du catalogue regroupees
/// Domaine → Module), la barre laterale (Domaine → Module → Écran), le fil d'Ariane de
/// l'ecran actif, et le garde de permission des onglets.
///
/// Une seule source pour la structure : <see cref="FunctionalArchitectureCatalog.Tree"/>,
/// elague par <see cref="NavigationTreeBuilder"/> avec les permissions du JWT. Ce que le
/// profil ne peut pas ouvrir n'apparait pas dans la barre laterale ; l'accueil, lui, montre
/// tout, cadenas compris - c'est la reponse a « ou en est le produit ? », pas un menu.
///
/// Le masquage n'est jamais une securite : chaque route reste protegee par sa politique
/// cote serveur, et chaque onglet reste desactive par ApplyModuleAccess.
/// </summary>
public partial class MainWindow
{
    // Ordre des onglets de MainTabs : 0=Accueil, 1=Unités hôtelières,
    // 2=Recettes journalières, 3=Tableau de bord, 4=Journal d'audit,
    // 5=Clôture journalière, 6=Trésorerie, 7=Clients, 8=Facturation,
    // 9=Paramétrage global, 10=Administration & utilisateurs,
    // 11=Comptabilité SCF, 12=Budget & prévisions, 13=Créances & recouvrement,
    // 14=Tarifs & conventions, 15=Hébergement & occupation,
    // 16=Validations, 17=Rapports, 18=Sauvegarde,
    // 19=Tableau de bord PDG, 20=Cockpit DEC, 21=Housekeeping & chambres,
    // 22=RH & paie, 23=CRM & expérience client, 24=Stocks, 25=Achats, 26=Cuisine,
    // 27=Postes & erreurs, 28=Groupes & MICE, 29=Bibliothèque KPI, 30=PMS front office.
    //
    // Cet ordre est celui des TabItem, pas celui de la barre latérale : l'index d'un
    // onglet est l'identité d'un module dans tout le code (ModuleCatalog.TabIndex y
    // compris), donc les nouveaux modules s'ajoutent à la fin. La barre latérale, elle,
    // les présente à leur place métier, celle que l'arbre de navigation leur donne
    // (ScreenNode.LegacyTabIndex est l'adaptateur entre les deux).
    private const int HomeTabIndex = 0;

    // Largeurs du panneau lateral et de sa gouttiere quand il est affiche - reprises
    // telles quelles des ColumnDefinition de MainWindow.xaml.
    private const double SidebarWidth = 248;
    private const double SidebarGapWidth = 20;

    // Les 50 modules du catalogue, exposes une seule fois et partages par la barre
    // laterale et le catalogue de l'onglet 0 : une seule collection, donc pas de
    // clignotement quand un changement de profil repose les cadenas, et jamais deux
    // etats differents du meme module sur deux surfaces.
    private readonly IReadOnlyList<ModuleTile> moduleTiles =
        ModuleCatalog.Entries.Select((entry, index) => new ModuleTile(entry, index)).ToList();

    // Barre laterale : un groupe par domaine qui possede un ecran ouvrable, construit une
    // fois depuis l'arbre complet. Ce qu'un groupe montre est rejoue a chaque changement
    // de permissions ou de recherche (RefreshSidebar).
    private readonly IReadOnlyList<ModuleNavigationGroup> sidebarGroups;

    // Onglet de MainTabs -> tuile de l'accueil qui le porte (celle du chemin primaire) :
    // le garde de permission de la navigation (CanOpenModule), quel que soit le chemin
    // emprunte, et l'etat vivant (IsActive) des boutons de la barre laterale.
    private readonly IReadOnlyDictionary<int, ModuleTile> tilesByTab;

    // Sections ouvertes avant le debut d'une recherche : une recherche ouvre celles
    // qui ont un resultat, et l'effacer doit rendre la barre laterale telle que
    // l'utilisateur l'avait laissee, pas la replier arbitrairement.
    private readonly HashSet<ModuleNavigationGroup> groupsExpandedBeforeSearch = [];
    private bool isSidebarSearchActive;

    // L'arbre que le profil peut ouvrir (permissions seules, sans la recherche) : l'ordre
    // des raccourcis module precedent / suivant. Vide tant que rien n'a ete calcule.
    private NavigationTree navigableTree = NavigationTree.Empty;

    // Hors session, l'accueil est dans son etat par defaut (tout accessible) : l'elagage
    // recoit alors l'ensemble des cles connues plutot qu'un cas particulier « pas de filtre ».
    private static readonly IReadOnlySet<string> AllPermissionKeys =
        PermissionCatalog.All.Select(permission => permission.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

    // Tuile de l'accueil de chaque onglet, par le chemin primaire de l'arbre. Deux entrees
    // du catalogue peuvent partager un onglet (« Audit & controle interne » et
    // « Journalisation & tracabilite ») : c'est celle que l'arbre designe qui le represente.
    private static IReadOnlyDictionary<int, ModuleTile> BuildTilesByTab(IReadOnlyList<ModuleTile> tiles)
    {
        var byOrder = tiles.ToDictionary(tile => tile.Order, StringComparer.Ordinal);
        var byTab = new Dictionary<int, ModuleTile>();

        foreach (var path in FunctionalArchitectureCatalog.PrimaryPaths)
        {
            if (path.Screen.LegacyTabIndex is { } tab
                && path.Screen.LegacyOrder is { } order
                && byOrder.TryGetValue(order, out var tile))
            {
                byTab[tab] = tile;
            }
        }

        // Garde de compatibilite : un onglet que l'arbre ne connaitrait pas encore reste
        // ouvrable depuis l'accueil par sa premiere tuile. Le test de couverture doit
        // normalement rendre ce chemin vide.
        foreach (var tile in tiles)
        {
            if (tile.TabIndex is { } tab)
            {
                byTab.TryAdd(tab, tile);
            }
        }

        return byTab;
    }

    // Mise en place de tout ce qui depend du XAML : appelee une fois, apres
    // InitializeComponent, par InitializeDefaults.
    private void InitializeNavigation()
    {
        // Le catalogue des 50 modules vit desormais dans sa propre vue : la fenetre lui
        // prete les tuiles qu'elle partage avec la barre laterale et les cles du profil,
        // et garde pour elle le seul chemin de navigation.
        ModuleCatalogView.Initialize(moduleTiles, GrantedPermissionKeys);
        ModuleCatalogView.NavigateRequested -= HomeNavigateRequested;
        ModuleCatalogView.NavigateRequested += HomeNavigateRequested;

        // Deux zones de lecture : les domaines métier défilent, l'administration
        // système reste épinglée en pied de panneau.
        SidebarGroupsItemsControl.ItemsSource = sidebarGroups.Where(group => !group.IsPinned).ToList();
        SidebarPinnedGroupsItemsControl.ItemsSource = sidebarGroups.Where(group => group.IsPinned).ToList();
        ApplyModulePermissions();

        // L'application demarre sur l'accueil, meme avant la premiere connexion :
        // le TabControl n'a pas d'en-tetes, sa selection doit donc etre explicite.
        NavigateToModule(HomeTabIndex);
    }

    // ==================== Navigation entre onglets ====================

    // Unique chemin de navigation entre modules : selection de l'onglet + remise en
    // phase de la barre laterale. Utilise par la barre laterale ET par les cartes de
    // l'ecran d'accueil, pour ne jamais desynchroniser les deux.
    private void NavigateToModule(int tabIndex)
    {
        MainTabs.SelectedIndex = tabIndex;
        SyncSidebarToTab(tabIndex);
    }

    // Aligne la barre laterale et le fil d'Ariane sur l'onglet affiche : surbrillance du
    // module courant (le style ModuleNavButton reagit a Tag="Active" : filet accent de
    // 3px, teinte douce, texte en semi-gras), ouverture de son domaine, et repli complet
    // du panneau sur l'accueil.
    //
    // L'accueil ne montre PAS la barre laterale : cet ecran est deja le sommaire des
    // 50 modules, un second sommaire a cote ferait doublon et volerait aux cartes la
    // largeur dont elles ont besoin. Partout ailleurs elle est la, avec son bouton
    // « Accueil » comme chemin de retour.
    private void SyncSidebarToTab(int tabIndex)
    {
        var isHome = tabIndex == HomeTabIndex;

        ShowHomeButton.Tag = isHome ? "Active" : null;

        // Le module courant est marque sur la tuile, que la barre laterale et l'accueil
        // partagent ; les deux entrees d'un ecran partage s'allument ensemble, c'est voulu.
        foreach (var tile in moduleTiles)
        {
            tile.IsActive = tile.TabIndex == tabIndex;
        }

        foreach (var group in sidebarGroups)
        {
            // Ouvrir la section du module courant sans replier les autres : la barre
            // laterale suit la navigation, elle ne defait pas ce que l'utilisateur a
            // ouvert lui-meme.
            if (group.Owns(tabIndex))
            {
                group.IsExpanded = true;
            }
        }

        SidebarBorder.Visibility = isHome ? Visibility.Collapsed : Visibility.Visible;
        SidebarColumn.Width = isHome ? new GridLength(0) : new GridLength(SidebarWidth);
        SidebarGapColumn.Width = isHome ? new GridLength(0) : new GridLength(SidebarGapWidth);

        UpdateBreadcrumb(tabIndex);
    }

    // Fil d'Ariane de l'ecran actif : « Domaine → Module → Sous-module → Écran », par le
    // chemin primaire de l'arbre. Masque sur l'accueil, qui a son propre en-tete. Le
    // dernier segment est mis en avant : c'est la ou l'on est, le reste dit d'ou l'on vient.
    private void UpdateBreadcrumb(int tabIndex)
    {
        BreadcrumbTextBlock.Inlines.Clear();

        if (tabIndex == HomeTabIndex
            || !FunctionalArchitectureCatalog.TryGetPrimaryPath(tabIndex, out var path)
            || path is null)
        {
            BreadcrumbBorder.Visibility = Visibility.Collapsed;
            AutomationProperties.SetName(BreadcrumbTextBlock, string.Empty);
            return;
        }

        string[] segments =
        [
            $"{path.Domain.Id} {path.Domain.Label}",
            path.Module.Label,
            path.Submodule.Label,
            path.Screen.Label
        ];

        for (var index = 0; index < segments.Length; index++)
        {
            if (index > 0)
            {
                BreadcrumbTextBlock.Inlines.Add(new Run("  →  ")
                {
                    Foreground = TryFindResource("TextMutedBrush") as Brush ?? BreadcrumbTextBlock.Foreground
                });
            }

            var run = new Run(segments[index]);

            if (index == segments.Length - 1)
            {
                run.FontWeight = FontWeights.SemiBold;
                run.Foreground = TryFindResource("TextPrimaryBrush") as Brush ?? BreadcrumbTextBlock.Foreground;
            }

            BreadcrumbTextBlock.Inlines.Add(run);
        }

        // Le lecteur d'ecran annonce le chemin en une phrase, sans les fleches decoratives.
        AutomationProperties.SetName(BreadcrumbTextBlock, string.Join(" → ", segments));
        BreadcrumbBorder.Visibility = Visibility.Visible;
    }

    // Un module ne s'ouvre que s'il a un ecran connu du catalogue ET que le profil a
    // le droit de le lire. L'accueil n'est garde par aucune permission : c'est le
    // point de repli de toute navigation refusee.
    private bool CanOpenModule(int tabIndex) =>
        tabIndex == HomeTabIndex
        || (tilesByTab.TryGetValue(tabIndex, out var tile) && tile.IsClickable);

    // Fondu discret (150 ms) du contenu a chaque changement de module, et
    // resynchronisation de la surbrillance de la sidebar : quel que soit le chemin
    // de navigation (cartes, sidebar, cycle clavier), les deux restent alignes.
    private async void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged est un evenement routé : ignorer ceux qui remontent des
        // DataGrid/ComboBox internes.
        if (!ReferenceEquals(e.OriginalSource, MainTabs))
        {
            return;
        }

        // Ceinture et bretelles en plus des TabItem desactives : si un chemin de
        // navigation futur atteint un module non autorise, retour a l'accueil - la
        // nouvelle selection repasse aussitot par ce meme handler.
        if (!CanOpenModule(MainTabs.SelectedIndex))
        {
            MainTabs.SelectedIndex = HomeTabIndex;
            return;
        }

        SyncSidebarToTab(MainTabs.SelectedIndex);

        var fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        MainTabs.BeginAnimation(OpacityProperty, fade);

        // Chargement paresseux des vues de module autonomes : la premiere
        // ouverture de leur onglet declenche leur LoadAsync, les suivantes non.
        await EnsureModuleTabLoadedAsync(MainTabs.SelectedIndex);
    }

    // Declenche le premier chargement de la vue hebergee par l'onglet demande.
    // Les vues sortent d'elles-memes si le contexte n'a pas ete fourni ou si la
    // session est fermee : rien a garder ici en plus de l'index deja charge.
    private async Task EnsureModuleTabLoadedAsync(int tabIndex)
    {
        if (!apiClient.IsAuthenticated || !loadedModuleTabs.Add(tabIndex))
        {
            return;
        }

        switch (tabIndex)
        {
            case 5:
                await ClosingView.LoadAsync();
                break;
            case 6:
                await TreasuryView.LoadAsync();
                break;
            case 7:
                await CustomersView.LoadAsync();
                break;
            case 8:
                await InvoicesView.LoadAsync();
                break;
            case 9:
                await SettingsView.LoadAsync();
                break;
            case 10:
                await UsersView.LoadAsync();
                break;
            case 11:
                await AccountingView.LoadAsync();
                break;
            case 12:
                await BudgetView.LoadAsync();
                break;
            case 13:
                await ReceivablesView.LoadAsync();
                break;
            case 14:
                await TariffsView.LoadAsync();
                break;
            case 15:
                await LodgingView.LoadAsync();
                break;
            case 16:
                await ApprovalsView.LoadAsync();
                break;
            case 17:
                await ReportsView.LoadAsync();
                break;
            case 18:
                await BackupView.LoadAsync();
                break;
            case 19:
                await GroupDashboardView.LoadAsync();
                break;
            case 20:
                await DecCockpitView.LoadAsync();
                break;
            case 21:
                await HousekeepingView.LoadAsync();
                break;
            case 22:
                await HumanResourcesView.LoadAsync();
                break;
            case 23:
                await CrmView.LoadAsync();
                break;
            case 24:
                await InventoryView.LoadAsync();
                break;
            case 25:
                await PurchasingView.LoadAsync();
                break;
            case 26:
                await KitchenView.LoadAsync();
                break;
            case 27:
                await SyncView.LoadAsync();
                break;
            case 28:
                await MiceView.LoadAsync();
                break;
            case 29:
                await KpiView.LoadAsync();
                break;
            case 30:
                await PmsView.LoadAsync();
                break;
            default:
                // Les onglets 0 a 4 vivent dans MainWindow et sont charges a la
                // connexion : rien a faire, et rien a retenir non plus.
                loadedModuleTabs.Remove(tabIndex);
                break;
        }
    }

    // Navigation demandee par le cockpit DEC (recettes=2, cloture=5, tresorerie=6). Le module
    // cible reste garde par sa propre permission de lecture : un bouton de sidebar desactive
    // signifie que le profil n'a pas le droit, et la demande est alors ignoree plutot que de
    // renvoyer l'utilisateur sur l'accueil sans explication.
    private void DecCockpitView_NavigateRequested(int tabIndex)
    {
        if (!CanOpenModule(tabIndex))
        {
            return;
        }

        NavigateToModule(tabIndex);
    }

    // Ouverture demandee depuis l'onglet 0 (carte du catalogue, resultat de recherche,
    // plus tard carte de file de travail). Meme garde que partout ailleurs : la vue
    // demande, la fenetre decide.
    private void HomeNavigateRequested(int tabIndex)
    {
        if (!CanOpenModule(tabIndex))
        {
            return;
        }

        // La recherche de la barre laterale a rempli son office : elle revient a son
        // etat normal, ou le module qui vient de s'ouvrir se voit dans sa famille.
        ModuleSearchTextBox.Clear();
        NavigateToModule(tabIndex);
    }

    private void ShowHomeButton_Click(object sender, RoutedEventArgs e)
    {
        ModuleSearchTextBox.Clear();
        NavigateToModule(HomeTabIndex);
    }

    // Cartes de l'accueil ET boutons de la barre laterale : un seul handler, donc un
    // seul comportement. Une carte ou un bouton sans ecran ou sans permission est
    // desactive (IsClickable=False) et ne peut donc pas declencher ce handler ; le
    // test reste une securite si un chemin futur y arrivait autrement.
    private void ModuleTileNavigate_Click(object sender, RoutedEventArgs e)
    {
        var tabIndex = (sender as FrameworkElement)?.DataContext switch
        {
            ModuleTile tile => tile.TabIndex,
            ModuleNavigationScreen screen => screen.TabIndex,
            _ => null
        };

        if (tabIndex is not { } target || !CanOpenModule(target))
        {
            return;
        }

        // La recherche a rempli son office : la barre laterale revient a son etat
        // normal, ou le module qui vient de s'ouvrir se voit dans sa famille.
        ModuleSearchTextBox.Clear();
        NavigateToModule(target);
    }

    // ==================== Permissions ====================

    // Aligne l'ecran d'accueil et la sidebar sur les permissions de lecture du
    // profil connecte : carte verrouillee (cadenas + info-bulle explicite) quand
    // la permission manque, onglet desactive pour les modules qui ont un ecran, et
    // barre laterale reduite a ce qui s'ouvre. Appele au demarrage, apres connexion
    // et apres deconnexion (ou currentUserPermissions est null : tout revient a
    // l'etat par defaut).
    private void ApplyModulePermissions()
    {
        // Un module sans cle de permission n'est jamais verrouille : son statut
        // d'avancement suffit a dire ce que l'utilisateur peut en faire.
        foreach (var tile in moduleTiles)
        {
            tile.IsLocked = tile.PermissionKey is { } permission && !HasModulePermission(permission);
        }

        ApplyModuleAccess(PermissionCatalog.UnitsRead, UnitsTabItem);
        ApplyModuleAccess(PermissionCatalog.RevenueRead, RevenueTabItem);
        ApplyModuleAccess(PermissionCatalog.DashboardRead, DashboardTabItem);
        ApplyModuleAccess(PermissionCatalog.AuditRead, AuditTabItem);
        ApplyModuleAccess(PermissionCatalog.ClosingRead, ClosingTabItem);
        ApplyModuleAccess(PermissionCatalog.TreasuryRead, TreasuryTabItem);
        ApplyModuleAccess(PermissionCatalog.CustomersRead, CustomersTabItem);
        ApplyModuleAccess(PermissionCatalog.InvoicesRead, InvoicesTabItem);
        ApplyModuleAccess(PermissionCatalog.SettingsRead, SettingsTabItem);
        ApplyModuleAccess(PermissionCatalog.UsersRead, UsersTabItem);
        ApplyModuleAccess(PermissionCatalog.AccountingRead, AccountingTabItem);
        ApplyModuleAccess(PermissionCatalog.BudgetRead, BudgetTabItem);
        ApplyModuleAccess(PermissionCatalog.ReceivablesRead, ReceivablesTabItem);
        ApplyModuleAccess(PermissionCatalog.TariffsRead, TariffsTabItem);
        ApplyModuleAccess(PermissionCatalog.LodgingRead, LodgingTabItem);
        ApplyModuleAccess(PermissionCatalog.ApprovalsRead, ApprovalsTabItem);
        ApplyModuleAccess(PermissionCatalog.ReportsRead, ReportsTabItem);
        ApplyModuleAccess(PermissionCatalog.MaintenanceRead, BackupTabItem);

        // Les deux ecrans de pilotage sont de l'agregation pure des modules existants : ils
        // reutilisent la cle dashboard.read deja semee, sans creer de permission a eux.
        ApplyModuleAccess(PermissionCatalog.DashboardRead, GroupDashboardTabItem);
        ApplyModuleAccess(PermissionCatalog.DashboardRead, DecCockpitTabItem);
        ApplyModuleAccess(PermissionCatalog.HousekeepingRead, HousekeepingTabItem);
        ApplyModuleAccess(PermissionCatalog.InventoryRead, InventoryTabItem);
        ApplyModuleAccess(PermissionCatalog.PurchasingRead, PurchasingTabItem);
        ApplyModuleAccess(PermissionCatalog.KitchenRead, KitchenTabItem);
        ApplyModuleAccess(PermissionCatalog.CrmRead, CrmTabItem);
        // Sans cette ligne l'onglet RH restait le SEUL module ouvert a tout profil
        // connecte, y compris par le cycle clavier Ctrl+Tab : les donnees de paie
        // sont parmi les plus sensibles du produit.
        ApplyModuleAccess(PermissionCatalog.HrRead, HumanResourcesTabItem);
        ApplyModuleAccess(PermissionCatalog.SyncRead, SyncTabItem);
        ApplyModuleAccess(PermissionCatalog.MiceRead, MiceTabItem);

        ApplyWriteActionStates();
        RefreshSidebar();

        // Les cadenas des cartes sont poses sur les tuiles partagees ; la liste des
        // resultats de recherche de la vue, elle, doit etre recalculee.
        ModuleCatalogView.RefreshPermissions();
    }

    // Le bouton de la barre laterale n'existe que si le profil peut ouvrir l'ecran
    // (RefreshSidebar) : il ne reste ici que l'onglet, dont la desactivation ferme le
    // chemin clavier Ctrl+Tab / Ctrl+Shift+Tab, qui cycle les onglets meme quand leurs
    // en-tetes ne sont pas affiches - un onglet desactive est saute par ce cycle.
    private void ApplyModuleAccess(string permission, TabItem tabItem)
    {
        tabItem.IsEnabled = HasModulePermission(permission);
    }

    // Les cles du profil, telles que l'elagage les compare (sans casse, comme
    // HasModulePermission). Hors session, toutes : l'accueil est alors dans son etat
    // par defaut, et la barre laterale avec lui.
    //
    // Le jeton porte les cles que le serveur y a mises ; l'arbre de navigation, lui, est
    // ecrit avec les cles CIBLES (domaine.ressource.action). Comparer les deux
    // litteralement masquerait a un role personnalise des ecrans que l'API lui ouvre :
    // l'ensemble est donc etendu de toute cle cible dont une claim acceptee est detenue,
    // exactement la regle que les politiques appliquent cote serveur.
    private IReadOnlySet<string> GrantedPermissionKeys()
    {
        if (currentUserPermissions is null)
        {
            return AllPermissionKeys;
        }

        // Les claims du jeton sont figees avant l'extension : une cle ajoutee ici ne doit
        // jamais en justifier une autre.
        var claims = currentUserPermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var granted = new HashSet<string>(claims, StringComparer.OrdinalIgnoreCase);

        foreach (var key in AllPermissionKeys)
        {
            if (PermissionRegistry.AcceptedClaims(key).Any(claims.Contains))
            {
                granted.Add(key);
            }
        }

        return granted;
    }

    // ==================== Barre laterale ====================

    private void ModuleSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshSidebar();
    }

    // Echap efface la recherche sans quitter le champ : le raccourci attendu d'un
    // champ de recherche, et le seul moyen au clavier de retrouver la liste complete.
    private void ModuleSearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || ModuleSearchTextBox.Text.Length == 0)
        {
            return;
        }

        ModuleSearchTextBox.Clear();
        e.Handled = true;
    }

    private void ClearModuleSearchButton_Click(object sender, RoutedEventArgs e)
    {
        ModuleSearchTextBox.Clear();
        ModuleSearchTextBox.Focus();
    }

    // Rejoue l'arbre elague sur la barre laterale : permissions du profil, puis la
    // recherche. Les sections qui ont un resultat s'ouvrent, les autres disparaissent.
    // Effacer la recherche rend les sections a l'etat ou l'utilisateur les avait
    // laissees, plus celle du module affiche.
    private void RefreshSidebar()
    {
        var query = ModuleSearchTextBox.Text;
        var isSearching = !string.IsNullOrWhiteSpace(query);
        var granted = GrantedPermissionKeys();

        // L'arbre « ouvrable » ne depend pas de la recherche : c'est lui que suivent les
        // raccourcis module precedent / suivant. Une recherche aide a trouver, elle ne
        // change pas l'espace de travail.
        navigableTree = NavigationTreeBuilder.Build(FunctionalArchitectureCatalog.Tree, granted, NavigationFilter.Sidebar);

        var shown = isSearching
            ? NavigationTreeBuilder.Build(
                FunctionalArchitectureCatalog.Tree,
                granted,
                NavigationFilter.Sidebar with { SearchText = query })
            : navigableTree;

        if (isSearching && !isSidebarSearchActive)
        {
            groupsExpandedBeforeSearch.Clear();

            foreach (var group in sidebarGroups.Where(group => group.IsExpanded))
            {
                groupsExpandedBeforeSearch.Add(group);
            }
        }

        var matches = 0;

        foreach (var group in sidebarGroups)
        {
            matches += group.Apply(shown.FindDomain(group.Id), tab => tilesByTab.GetValueOrDefault(tab));
            group.IsExpanded = isSearching ? group.HasMatches : groupsExpandedBeforeSearch.Contains(group);
        }

        if (!isSearching)
        {
            groupsExpandedBeforeSearch.Clear();
            SyncSidebarToTab(MainTabs.SelectedIndex);
        }

        isSidebarSearchActive = isSearching;

        // Le filet de separation du pied ne doit pas rester seul quand la recherche ne
        // retient rien dans l'administration systeme.
        SidebarPinnedPanel.Visibility = sidebarGroups.Any(group => group.IsPinned && group.HasMatches)
            ? Visibility.Visible
            : Visibility.Collapsed;

        ClearModuleSearchButton.Visibility = query.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        SidebarSearchEmptyTextBlock.Visibility = isSearching && matches == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

}
