using System.ComponentModel;
using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Desktop.Api;
using RaqmiSystem.Desktop.Views;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Desktop;

public partial class MainWindow : Window
{
    // Info-bulles expliquant pourquoi une action d'ecriture est grisee quand le
    // droit manque. Le serveur reste la seule autorite : ceci n'est qu'un confort
    // d'interface, jamais une mesure de securite.
    private const string UnitsWritePermissionHint =
        "Permission requise : units.write. Votre profil ne peut que consulter les unités.";

    private const string RevenueWritePermissionHint =
        "Permission requise : revenue.write. Votre profil ne peut que consulter les recettes.";

    private const string RevenueValidatePermissionHint =
        "Permission requise : revenue.validate. Votre profil ne peut pas valider ni rejeter une recette.";

    // Info-bulles d'origine des boutons d'ecriture, capturees avant toute
    // substitution : le message "permission requise" pose pour un profil restreint
    // doit disparaitre a la reconnexion d'un profil qui a le droit (meme motif
    // ApplyPermissionHint que TreasuryView).
    private readonly Dictionary<Button, object?> originalToolTips = [];

    // Appel API en vol : combine avec les permissions dans ApplyWriteActionStates,
    // pour que la fin d'un appel ne reactive jamais un bouton sans droit.
    private bool isBusy;

    private readonly RaqmiApiClient apiClient = new(new HttpClient());
    private IReadOnlyCollection<HotelUnitResponse> hotelUnits = Array.Empty<HotelUnitResponse>();
    private string? editingUnitCode;

    // Ecran d'accueil : les 49 modules du catalogue, exposes une seule fois et
    // filtres via une vue (pas de reconstruction de collection, donc pas de
    // clignotement quand on change de filtre ou de profil).
    private readonly IReadOnlyList<ModuleTile> moduleTiles =
        ModuleCatalog.Entries.Select(entry => new ModuleTile(entry)).ToList();

    private ICollectionView? moduleCatalogView;
    private ModuleStatus? moduleStatusFilter;
    private string? modulePriorityFilter;

    // Permissions de l'utilisateur connecte (cles "units.read", "revenue.read", ...).
    // Null tant que personne n'est connecte : l'ecran d'accueil est alors dans son
    // etat par defaut (tout accessible), retabli a chaque deconnexion.
    private IReadOnlyCollection<string>? currentUserPermissions;

    // Identifiant du compte connecte, prete aux vues de module via le
    // ModuleViewContext. L'administration des utilisateurs s'en sert pour
    // reconnaitre l'utilisateur courant dans sa propre liste et ne pas lui
    // proposer de desactiver son compte - garde-fou dont le serveur reste
    // l'autorite. Null hors session.
    private Guid? currentUserId;

    // Onglets des vues de module (ClosingView, TreasuryView, CustomersView,
    // InvoicesView, SettingsView, UsersView, AccountingView, BudgetView,
    // ReceivablesView, TariffsView, LodgingView) deja charges depuis la connexion
    // en cours. Ces onze vues sont chargees paresseusement - a la premiere
    // ouverture de leur onglet - pour ne pas declencher autant de series d'appels
    // reseau inutiles a chaque connexion. Vide a la deconnexion : la session
    // suivante repart de donnees fraiches.
    private readonly HashSet<int> loadedModuleTabs = [];

    public MainWindow()
    {
        InitializeComponent();

        // Aligne les StringFormat du XAML (montants N2, dates) sur la culture du
        // poste : sans cela WPF formate en en-US pendant que le code-behind formate
        // en culture courante, et deux formats coexistent sur le meme ecran.
        Language = XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);

        ApiBaseUrlTextBox.Text = DesktopSettings.Load();
        PrefillRememberedCredentials();
        InitializeDefaults();
        RefreshAuthState();

        // Focus initial : premier champ vide de l'ecran de connexion, ou directement
        // le bouton quand les identifiants memorises ont tout pre-rempli.
        Loaded += (_, _) =>
        {
            if (!apiClient.IsAuthenticated)
            {
                if (string.IsNullOrWhiteSpace(UserNameTextBox.Text))
                {
                    UserNameTextBox.Focus();
                }
                else if (string.IsNullOrEmpty(PasswordBox.Password))
                {
                    PasswordBox.Focus();
                }
                else
                {
                    LoginButton.Focus();
                }
            }
        };
    }

    private void PrefillRememberedCredentials()
    {
        if (DesktopSettings.TryLoadCredentials(out var rememberedUser, out var rememberedPassword))
        {
            UserNameTextBox.Text = rememberedUser;
            PasswordBox.Password = rememberedPassword;
            RememberMeCheckBox.IsChecked = true;
        }
    }

    private void InitializeDefaults()
    {
        BusinessDatePicker.SelectedDate = DateTime.Today;
        DashboardDatePicker.SelectedDate = DateTime.Today;
        AccommodationTextBox.Text = "0";
        FoodTextBox.Text = "0";
        BeverageTextBox.Text = "0";
        OtherTextBox.Text = "0";
        UnitTypeComboBox.ItemsSource = Enum.GetValues<HotelUnitType>();
        ResetUnitForm();
        RefreshHomeDate();
        InitializeModuleCatalog();
        ApplyModulePermissions();

        // L'application demarre sur l'accueil, meme avant la premiere connexion :
        // le TabControl n'a pas d'en-tetes, sa selection doit donc etre explicite.
        NavigateToModule(0, ShowHomeButton);
        SetStatus("Connectez-vous pour charger les données de l'API.");
    }

    // Swaps between the login card and the sidebar+modules content based on whether the
    // API client currently holds a session token. Called at startup, after a successful
    // login, and after sign-out - never left to the individual handlers to remember.
    private void RefreshAuthState()
    {
        var isAuthenticated = apiClient.IsAuthenticated;

        LoginCardBorder.Visibility = isAuthenticated ? Visibility.Collapsed : Visibility.Visible;
        MainContentGrid.Visibility = isAuthenticated ? Visibility.Visible : Visibility.Collapsed;
        HeaderSessionPanel.Visibility = isAuthenticated ? Visibility.Visible : Visibility.Collapsed;
    }

    // Highlights the sidebar button for the module currently shown in MainTabs, so the
    // "Modules" panel - now the only navigation surface - always shows where you are.
    private void SetActiveModuleButton(Button active)
    {
        // Le style ModuleNavButton (Themes/RaqmiTheme.xaml) reagit a Tag="Active" :
        // barre laterale accent de 3px, teinte douce et texte en semi-gras.
        foreach (var button in new[]
                 {
                     ShowHomeButton, ShowUnitsButton, ShowRevenueButton, ShowDashboardButton, ShowAuditButton,
                     ShowClosingButton, ShowTreasuryButton, ShowCustomersButton, ShowInvoicesButton,
                     ShowAccountingButton, ShowBudgetButton, ShowReceivablesButton,
                     ShowTariffsButton, ShowLodgingButton,
                     ShowSettingsButton, ShowUsersButton
                 })
        {
            button.Tag = ReferenceEquals(button, active) ? "Active" : null;
        }
    }

    // Unique chemin de navigation entre modules : selection de l'onglet + mise en
    // surbrillance du bouton de la sidebar. Utilise par la sidebar ET par les
    // cartes de l'ecran d'accueil, pour ne jamais desynchroniser les deux.
    private void NavigateToModule(int tabIndex, Button moduleButton)
    {
        MainTabs.SelectedIndex = tabIndex;
        SetActiveModuleButton(moduleButton);
    }

    // Date du jour en toutes lettres ("vendredi 29 août 2026") sur l'accueil.
    private void RefreshHomeDate()
    {
        HomeDateTextBlock.Text = DateTime.Today.ToString(
            "dddd d MMMM yyyy",
            CultureInfo.GetCultureInfo("fr-FR"));
    }

    private bool HasModulePermission(string permission)
    {
        return currentUserPermissions is null
            || currentUserPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    // Aligne l'ecran d'accueil et la sidebar sur les permissions de lecture du
    // profil connecte : carte verrouillee (cadenas + info-bulle explicite) quand
    // la permission manque, bouton et onglet desactives pour les modules qui ont
    // un ecran. Appele au demarrage, apres connexion et apres deconnexion (ou
    // currentUserPermissions est null : tout revient a l'etat par defaut).
    private void ApplyModulePermissions()
    {
        // Un module sans cle de permission n'est jamais verrouille : son statut
        // d'avancement suffit a dire ce que l'utilisateur peut en faire.
        foreach (var tile in moduleTiles)
        {
            tile.IsLocked = tile.PermissionKey is { } permission && !HasModulePermission(permission);
        }

        ApplyModuleAccess(PermissionCatalog.UnitsRead, ShowUnitsButton, UnitsTabItem);
        ApplyModuleAccess(PermissionCatalog.RevenueRead, ShowRevenueButton, RevenueTabItem);
        ApplyModuleAccess(PermissionCatalog.DashboardRead, ShowDashboardButton, DashboardTabItem);
        ApplyModuleAccess(PermissionCatalog.AuditRead, ShowAuditButton, AuditTabItem);
        ApplyModuleAccess(PermissionCatalog.ClosingRead, ShowClosingButton, ClosingTabItem);
        ApplyModuleAccess(PermissionCatalog.TreasuryRead, ShowTreasuryButton, TreasuryTabItem);
        ApplyModuleAccess(PermissionCatalog.CustomersRead, ShowCustomersButton, CustomersTabItem);
        ApplyModuleAccess(PermissionCatalog.InvoicesRead, ShowInvoicesButton, InvoicesTabItem);
        ApplyModuleAccess(PermissionCatalog.SettingsRead, ShowSettingsButton, SettingsTabItem);
        ApplyModuleAccess(PermissionCatalog.UsersRead, ShowUsersButton, UsersTabItem);
        ApplyModuleAccess(PermissionCatalog.AccountingRead, ShowAccountingButton, AccountingTabItem);
        ApplyModuleAccess(PermissionCatalog.BudgetRead, ShowBudgetButton, BudgetTabItem);
        ApplyModuleAccess(PermissionCatalog.ReceivablesRead, ShowReceivablesButton, ReceivablesTabItem);
        ApplyModuleAccess(PermissionCatalog.TariffsRead, ShowTariffsButton, TariffsTabItem);
        ApplyModuleAccess(PermissionCatalog.LodgingRead, ShowLodgingButton, LodgingTabItem);

        ApplyWriteActionStates();
    }

    // Source unique de verite de l'etat des boutons d'ecriture des onglets Unites
    // et Recettes : croise le droit du profil avec l'appel API en vol. Appelee a
    // chaque changement de session (ApplyModulePermissions) ET a chaque bascule de
    // SetBusy, pour qu'un retour de SetBusy(false) ne reactive jamais un bouton
    // sans droit.
    private void ApplyWriteActionStates()
    {
        var canWriteUnits = HasModulePermission(PermissionCatalog.UnitsWrite);
        var canWriteRevenue = HasModulePermission(PermissionCatalog.RevenueWrite);
        var canValidateRevenue = HasModulePermission(PermissionCatalog.RevenueValidate);

        SetActionState(SaveUnitButton, canWriteUnits, UnitsWritePermissionHint);
        SetActionState(ActivateUnitButton, canWriteUnits, UnitsWritePermissionHint);
        SetActionState(DeactivateUnitButton, canWriteUnits, UnitsWritePermissionHint);
        SetActionState(CreateRevenueButton, canWriteRevenue, RevenueWritePermissionHint);
        SetActionState(CreateAndSubmitRevenueButton, canWriteRevenue, RevenueWritePermissionHint);
        SetActionState(ValidateRevenueButton, canValidateRevenue, RevenueValidatePermissionHint);
        SetActionState(RejectRevenueButton, canValidateRevenue, RevenueValidatePermissionHint);
    }

    // Grise le bouton quand le droit manque (ou pendant un appel en vol) et pose
    // l'info-bulle explicative, RESTAUREE quand le droit est present : l'affectation
    // doit etre symetrique, sinon un message pose pour un profil restreint survit a
    // la reconnexion d'un profil qui, lui, a le droit (motif ApplyPermissionHint,
    // TreasuryView.xaml.cs).
    private void SetActionState(Button button, bool allowed, string hint)
    {
        button.IsEnabled = allowed && !isBusy;

        if (!originalToolTips.ContainsKey(button))
        {
            originalToolTips[button] = button.ToolTip;
        }

        button.ToolTip = allowed ? originalToolTips[button] : hint;
    }

    private void ApplyModuleAccess(string permission, Button navButton, TabItem tabItem)
    {
        var allowed = HasModulePermission(permission);

        navButton.IsEnabled = allowed;

        // Desactiver aussi le TabItem ferme le chemin clavier Ctrl+Tab / Ctrl+Shift+Tab,
        // qui cycle les onglets meme quand leurs en-tetes ne sont pas affiches - un
        // onglet desactive est saute par ce cycle.
        tabItem.IsEnabled = allowed;
    }

    // ==================== Accueil : carte d'avancement des modules ====================

    // Prepare la liste groupee des 49 modules (regroupement par famille via la
    // vue, filtres statut/priorite appliques par MatchesModuleFilters) et le
    // bandeau d'avancement.
    private void InitializeModuleCatalog()
    {
        var source = new CollectionViewSource { Source = moduleTiles };
        source.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ModuleTile.Group)));

        moduleCatalogView = source.View;
        moduleCatalogView.Filter = MatchesModuleFilters;
        ModuleCatalogItemsControl.ItemsSource = moduleCatalogView;

        RefreshModuleProgress();
        RefreshModuleCatalogEmptyState();
    }

    // Compteurs par statut, largeurs proportionnelles de la barre segmentee et
    // libelles de synthese : la reponse directe a "ou en est le produit ?".
    private void RefreshModuleProgress()
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
        // ("livres cote serveur") surestimerait le travail fait, car "Partiel"
        // recouvre des situations tres differentes (API seule, fonction absorbee
        // par un autre ecran, outillage serveur hors application).
        ModuleProgressCaptionTextBlock.Text =
            $"{available} modules disponibles sur {total}  ·  {apiReady} avec API livrée, écran à venir  ·  {partial} partiellement couverts  ·  {planned} planifiés";
    }

    // Filtre courant de la vue : statut, puis priorite.
    private bool MatchesModuleFilters(object item)
    {
        if (item is not ModuleTile tile)
        {
            return false;
        }

        if (moduleStatusFilter is { } status && tile.Status != status)
        {
            return false;
        }

        return modulePriorityFilter is null
            || string.Equals(tile.Priority, modulePriorityFilter, StringComparison.Ordinal);
    }

    private void ModuleStatusFilterChip_Checked(object sender, RoutedEventArgs e)
    {
        // Les puces par defaut sont cochees pendant InitializeComponent, donc
        // avant la creation de la vue : ces premiers evenements sont sans objet.
        if (moduleCatalogView is null)
        {
            return;
        }

        var tag = (sender as RadioButton)?.Tag as string;
        moduleStatusFilter = Enum.TryParse<ModuleStatus>(tag, out var status) ? status : null;
        ApplyModuleCatalogFilters();
    }

    private void ModulePriorityFilterChip_Checked(object sender, RoutedEventArgs e)
    {
        if (moduleCatalogView is null)
        {
            return;
        }

        var tag = (sender as RadioButton)?.Tag as string;
        modulePriorityFilter = string.IsNullOrEmpty(tag) ? null : tag;
        ApplyModuleCatalogFilters();
    }

    private void ApplyModuleCatalogFilters()
    {
        moduleCatalogView?.Refresh();
        RefreshModuleCatalogEmptyState();
    }

    private void RefreshModuleCatalogEmptyState()
    {
        var isEmpty = moduleCatalogView is null || moduleCatalogView.IsEmpty;
        ModuleCatalogEmptyTextBlock.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    // Carte de l'accueil : meme chemin de navigation que la barre laterale. Une
    // carte sans ecran ou sans permission est desactivee (IsClickable=False) et
    // ne peut donc pas declencher ce handler ; le test du bouton reste une
    // securite si un chemin futur y arrivait autrement.
    private void ModuleCatalogCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ModuleTile tile } || tile.TabIndex is not { } tabIndex)
        {
            return;
        }

        var moduleButton = SidebarButtonForTab(tabIndex);
        if (moduleButton is null || !moduleButton.IsEnabled)
        {
            return;
        }

        NavigateToModule(tabIndex, moduleButton);
    }

    // Ordre des onglets de MainTabs : 0=Accueil, 1=Unités hôtelières,
    // 2=Recettes journalières, 3=Tableau de bord, 4=Journal d'audit,
    // 5=Clôture journalière, 6=Trésorerie, 7=Clients, 8=Facturation,
    // 9=Paramétrage global, 10=Administration & utilisateurs,
    // 11=Comptabilité SCF, 12=Budget & prévisions, 13=Créances & recouvrement,
    // 14=Tarifs & conventions, 15=Hébergement & occupation.
    //
    // Cet ordre est celui des TabItem, pas celui de la barre latérale : l'index d'un
    // onglet est l'identité d'un module dans tout le code (ModuleCatalog.TabIndex y
    // compris), donc les nouveaux modules s'ajoutent à la fin. La barre latérale, elle,
    // les présente à leur place métier, parmi les modules d'exploitation.
    private Button? SidebarButtonForTab(int tabIndex) => tabIndex switch
    {
        0 => ShowHomeButton,
        1 => ShowUnitsButton,
        2 => ShowRevenueButton,
        3 => ShowDashboardButton,
        4 => ShowAuditButton,
        5 => ShowClosingButton,
        6 => ShowTreasuryButton,
        7 => ShowCustomersButton,
        8 => ShowInvoicesButton,
        9 => ShowSettingsButton,
        10 => ShowUsersButton,
        11 => ShowAccountingButton,
        12 => ShowBudgetButton,
        13 => ShowReceivablesButton,
        14 => ShowTariffsButton,
        15 => ShowLodgingButton,
        _ => null
    };

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

        var moduleButton = SidebarButtonForTab(MainTabs.SelectedIndex);

        if (moduleButton is not null)
        {
            // Ceinture et bretelles en plus des TabItem desactives : si un chemin
            // de navigation futur atteint un module non autorise, retour a l'accueil.
            if (!moduleButton.IsEnabled)
            {
                MainTabs.SelectedIndex = 0;
                moduleButton = ShowHomeButton;
            }

            SetActiveModuleButton(moduleButton);
        }

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
            default:
                // Les onglets 0 a 4 vivent dans MainWindow et sont charges a la
                // connexion : rien a faire, et rien a retenir non plus.
                loadedModuleTabs.Remove(tabIndex);
                break;
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        // Releve pendant l'appel, exploite apres : ouvrir une fenetre modale depuis
        // l'interieur de RunApiActionAsync le ferait pendant que le curseur d'attente
        // est encore pose et que la fenetre principale n'a pas fini de basculer.
        var mustChangePassword = false;

        await RunApiActionAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(UserNameTextBox.Text) || string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                SetStatus("Utilisateur et mot de passe requis.", isError: true);
                return;
            }

            var userName = UserNameTextBox.Text.Trim();
            var password = PasswordBox.Password;

            var login = await apiClient.LoginAsync(
                ApiBaseUrlTextBox.Text,
                new LoginRequest(userName, password));

            CurrentUserTextBlock.Text = $"{login.User.DisplayName} - {login.User.UserName}";
            mustChangePassword = login.User.MustChangePassword;
            PasswordBox.Password = string.Empty;
            DesktopSettings.Save(ApiBaseUrlTextBox.Text.Trim());

            // Personnalisation de l'ecran d'accueil + application des permissions
            // de lecture du profil sur les cartes et la sidebar.
            currentUserPermissions = login.User.Permissions;
            currentUserId = login.User.Id;
            HomeGreetingTextBlock.Text = $"Bonjour, {login.User.DisplayName}";
            RefreshHomeDate();
            ApplyModulePermissions();

            // Memorisation uniquement apres une connexion REUSSIE, pour ne jamais
            // enregistrer des identifiants invalides ; decocher la case efface
            // ce qui avait ete memorise precedemment.
            if (RememberMeCheckBox.IsChecked == true)
            {
                DesktopSettings.SaveCredentials(userName, password);
            }
            else
            {
                DesktopSettings.ClearCredentials();
            }
            RefreshAuthState();

            // Les vues de module autonomes recoivent le contexte de la
            // session qui vient de s'ouvrir. Initialize ne fait que memoriser :
            // aucun appel reseau tant que l'onglet correspondant n'est pas ouvert.
            InitializeModuleViews();

            // La session s'ouvre toujours sur l'ecran d'accueil.
            NavigateToModule(0, ShowHomeButton);

            SetStatus("Connexion réussie. Chargement des données...");

            // Ne precharge que les modules que le profil est autorise a lire,
            // pour ne pas echouer sur un 403 previsible.
            if (HasModulePermission(PermissionCatalog.UnitsRead))
            {
                await LoadHotelUnitsAsync();
            }

            if (HasModulePermission(PermissionCatalog.RevenueRead))
            {
                await LoadDailyRevenueAsync();
            }

            if (HasModulePermission(PermissionCatalog.DashboardRead))
            {
                await LoadUnitDashboardAsync();
            }

            SetStatus("Données chargées.");
        });

        // C'est ici que le drapeau MustChangePassword devient utile. Un administrateur
        // le pose en remettant un mot de passe temporaire - qu'il a donc pu lire - et
        // rien d'autre dans l'application ne vient jamais le lever : sans cette
        // ouverture automatique, le compte resterait indefiniment sur un mot de passe
        // connu d'un tiers.
        if (mustChangePassword && apiClient.IsAuthenticated)
        {
            ShowChangePasswordDialog(isMandatory: true);
        }
    }

    private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
    {
        if (!apiClient.IsAuthenticated)
        {
            SetStatus("Connectez-vous avant de changer votre mot de passe.", isError: true);
            return;
        }

        ShowChangePasswordDialog(isMandatory: false);
    }

    /// <summary>
    /// Ouvre la fenetre de changement de mot de passe et tire les consequences de son
    /// resultat sur ce poste.
    /// </summary>
    /// <param name="isMandatory">
    /// Vrai quand la connexion a signale que le mot de passe doit etre change : la
    /// fenetre explique alors pourquoi, et un abandon laisse un rappel dans le bandeau
    /// de session plutot que de passer sous silence un compte a regulariser.
    /// </param>
    private void ShowChangePasswordDialog(bool isMandatory)
    {
        var dialog = new ChangePasswordWindow(apiClient, ApiBaseUrlTextBox.Text, isMandatory)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            if (isMandatory)
            {
                SetStatus(
                    "Mot de passe temporaire toujours en place. Utilisez « Changer mon mot de passe » dans l'en-tête dès que possible.",
                    isError: true);
            }

            return;
        }

        // Les identifiants memorises sur ce poste viennent de devenir faux : les
        // garder pre-remplirait la prochaine connexion avec un mot de passe que le
        // serveur refuse desormais, et quelques essais suffisent a verrouiller le
        // compte. On les efface plutot que de laisser l'utilisateur decouvrir cela.
        DesktopSettings.ClearCredentials();
        RememberMeCheckBox.IsChecked = false;

        // Le serveur revoque TOUTES les sessions du compte, y compris le jeton de la
        // session courante (reemis dans la foulee) : RevokedSessionCount ne compte
        // donc pas que les "autres" appareils. Plutot que d'afficher un chiffre qui
        // mentirait d'une unite, le message dit honnetement la consequence utile.
        SetStatus(
            "Mot de passe changé. Vos autres appareils éventuellement connectés devront se reconnecter avec le nouveau mot de passe.");
    }

    // Un seul contexte partage par les vues de module : le client API de la
    // fenetre, l'URL saisie sur l'ecran de connexion, et les deux services
    // transverses (bandeau de session, execution d'un appel avec curseur d'attente).
    private void InitializeModuleViews()
    {
        var context = new ModuleViewContext(
            apiClient,
            () => ApiBaseUrlTextBox.Text,
            SetStatus,
            RunApiActionAsync,
            HasModulePermission,
            () => currentUserId);

        ClosingView.Initialize(context);
        TreasuryView.Initialize(context);
        CustomersView.Initialize(context);
        InvoicesView.Initialize(context);
        SettingsView.Initialize(context);
        UsersView.Initialize(context);
        AccountingView.Initialize(context);
        BudgetView.Initialize(context);
        ReceivablesView.Initialize(context);
        TariffsView.Initialize(context);
        LodgingView.Initialize(context);

        // Nouvelle session : aucune vue n'a encore charge ses donnees.
        loadedModuleTabs.Clear();
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        apiClient.Logout();
        CurrentUserTextBlock.Text = "Non connecté";
        PasswordBox.Password = string.Empty;

        // Reconnexion facilitee : re-pre-remplit l'ecran de connexion depuis les
        // identifiants memorises (s'il y en a).
        PrefillRememberedCredentials();

        // L'URL peut avoir ete changee depuis l'ecran Parametrage pendant la
        // session : la relire ici evite que la connexion suivante ne reenregistre
        // l'ancienne valeur encore affichee dans ce champ, ecrasant silencieusement
        // ce que l'utilisateur venait d'enregistrer.
        ApiBaseUrlTextBox.Text = DesktopSettings.Load();

        // Clear every grid/summary/form surface so the previous user's data never
        // stays visible after a subsequent login (own account or another one) -
        // this matters most for the audit log, which is never auto-reloaded on login.
        hotelUnits = Array.Empty<HotelUnitResponse>();
        UnitsDataGrid.ItemsSource = null;
        RevenueUnitComboBox.ItemsSource = null;
        DailyRevenueDataGrid.ItemsSource = null;
        UnitDashboardDataGrid.ItemsSource = null;
        AuditDataGrid.ItemsSource = null;

        SummaryTotalTextBlock.Text = "0";
        SummaryDraftTextBlock.Text = "0";
        SummarySubmittedTextBlock.Text = "0";
        SummaryValidatedTextBlock.Text = "0";
        SummaryRejectedTextBlock.Text = "0";

        DashboardTotalUnitsTextBlock.Text = "0";
        DashboardWithEntryTextBlock.Text = "0";
        DashboardMissingTextBlock.Text = "0";
        DashboardPendingValidationTextBlock.Text = "0";
        DashboardGrandTotalTextBlock.Text = "0";

        AuditResultCountTextBlock.Text = string.Empty;

        // Meme regle pour les vues de module autonomes : elles vident
        // leurs grilles et formulaires, et oublier les onglets deja charges
        // garantit des donnees fraiches a la prochaine connexion.
        ClosingView.ResetState();
        TreasuryView.ResetState();
        CustomersView.ResetState();
        InvoicesView.ResetState();
        SettingsView.ResetState();
        UsersView.ResetState();
        AccountingView.ResetState();
        BudgetView.ResetState();
        ReceivablesView.ResetState();
        TariffsView.ResetState();
        LodgingView.ResetState();
        loadedModuleTabs.Clear();

        ResetUnitForm();
        ResetAmounts();

        // L'ecran d'accueil revient a son etat par defaut : salutation neutre,
        // toutes les cartes et boutons de modules de nouveau accessibles.
        currentUserPermissions = null;
        currentUserId = null;
        HomeGreetingTextBlock.Text = "Bonjour";
        RefreshHomeDate();
        ApplyModulePermissions();

        // A la reconnexion, la session reprendra sur l'ecran d'accueil.
        NavigateToModule(0, ShowHomeButton);

        RefreshAuthState();
        SetStatus("Déconnecté. Reconnectez-vous pour continuer.");
    }

    private async void RefreshUnitsButton_Click(object sender, RoutedEventArgs e)
    {
        await RunApiActionAsync(async () =>
        {
            await LoadHotelUnitsAsync();
            SetStatus("Liste des unités actualisée.");
        });
    }

    private async void RefreshRevenueButton_Click(object sender, RoutedEventArgs e)
    {
        await RunApiActionAsync(async () =>
        {
            await LoadDailyRevenueAsync();
            SetStatus("Liste des recettes actualisée.");
        });
    }

    private async void RefreshDashboardButton_Click(object sender, RoutedEventArgs e)
    {
        await RunApiActionAsync(async () =>
        {
            await LoadUnitDashboardAsync();
            SetStatus("Tableau de bord actualisé.");
        });
    }

    private void ShowHomeButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToModule(0, ShowHomeButton);
    }

    private void ShowUnitsButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToModule(1, ShowUnitsButton);
    }

    private void ShowRevenueButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToModule(2, ShowRevenueButton);
    }

    private void ShowDashboardButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToModule(3, ShowDashboardButton);
    }

    private void ShowAuditButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToModule(4, ShowAuditButton);
    }

    private void ShowClosingButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToModule(5, ShowClosingButton);
    }

    private void ShowTreasuryButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToModule(6, ShowTreasuryButton);
    }

    private void ShowCustomersButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToModule(7, ShowCustomersButton);
    }

    private void ShowInvoicesButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToModule(8, ShowInvoicesButton);
    }

    private void ShowSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToModule(9, ShowSettingsButton);
    }

    private void ShowUsersButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToModule(10, ShowUsersButton);
    }

    private void ShowAccountingButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToModule(11, ShowAccountingButton);
    }

    private void ShowBudgetButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToModule(12, ShowBudgetButton);
    }

    private void ShowReceivablesButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToModule(13, ShowReceivablesButton);
    }

    private void ShowTariffsButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToModule(14, ShowTariffsButton);
    }

    private void ShowLodgingButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToModule(15, ShowLodgingButton);
    }

    private async void CreateRevenueButton_Click(object sender, RoutedEventArgs e)
    {
        await CreateRevenueAsync(submitAfterCreate: false);
    }

    private async void CreateAndSubmitRevenueButton_Click(object sender, RoutedEventArgs e)
    {
        await CreateRevenueAsync(submitAfterCreate: true);
    }

    private async Task CreateRevenueAsync(bool submitAfterCreate)
    {
        await RunApiActionAsync(async () =>
        {
            var request = BuildRevenueRequest();
            if (request is null)
            {
                return;
            }

            var created = await apiClient.CreateDailyRevenueAsync(ApiBaseUrlTextBox.Text, request);

            if (submitAfterCreate)
            {
                created = await apiClient.SubmitDailyRevenueAsync(ApiBaseUrlTextBox.Text, created.Id);
            }

            await LoadDailyRevenueAsync();
            ResetAmounts();

            SetStatus(submitAfterCreate
                ? "Recette créée et soumise au contrôle."
                : "Recette créée en brouillon.");
        });
    }

    private async Task LoadHotelUnitsAsync()
    {
        hotelUnits = await apiClient.GetHotelUnitsAsync(
            ApiBaseUrlTextBox.Text,
            IncludeInactiveCheckBox.IsChecked == true);

        UnitsDataGrid.ItemsSource = hotelUnits;

        var activeUnits = hotelUnits
            .Where(unit => unit.IsActive)
            .OrderBy(unit => unit.DisplayOrder)
            .ThenBy(unit => unit.Name)
            .ToArray();

        RevenueUnitComboBox.ItemsSource = activeUnits;

        if (RevenueUnitComboBox.SelectedItem is null && activeUnits.Length > 0)
        {
            RevenueUnitComboBox.SelectedIndex = 0;
        }
    }

    private async Task LoadDailyRevenueAsync()
    {
        var businessDate = GetSelectedBusinessDate();
        var rows = await apiClient.GetDailyRevenueAsync(
            ApiBaseUrlTextBox.Text,
            businessDate,
            businessDate,
            null);

        DailyRevenueDataGrid.ItemsSource = rows
            .OrderBy(row => row.HotelUnitCode)
            .ToArray();

        await LoadDailyRevenueSummaryAsync(businessDate);
    }

    private async Task LoadDailyRevenueSummaryAsync(DateOnly businessDate)
    {
        var summary = await apiClient.GetDailyRevenueSummaryAsync(
            ApiBaseUrlTextBox.Text,
            businessDate,
            businessDate,
            null);

        SummaryTotalTextBlock.Text = summary.Total.ToString("N2", CultureInfo.CurrentCulture);
        SummaryDraftTextBlock.Text = summary.DraftCount.ToString(CultureInfo.CurrentCulture);
        SummarySubmittedTextBlock.Text = summary.SubmittedCount.ToString(CultureInfo.CurrentCulture);
        SummaryValidatedTextBlock.Text = summary.ValidatedCount.ToString(CultureInfo.CurrentCulture);
        SummaryRejectedTextBlock.Text = summary.RejectedCount.ToString(CultureInfo.CurrentCulture);
    }

    private async Task LoadUnitDashboardAsync()
    {
        var businessDate = DashboardDatePicker.SelectedDate.HasValue
            ? DateOnly.FromDateTime(DashboardDatePicker.SelectedDate.Value)
            : DateOnly.FromDateTime(DateTime.Today);

        var dashboard = await apiClient.GetUnitDashboardAsync(ApiBaseUrlTextBox.Text, businessDate);

        UnitDashboardDataGrid.ItemsSource = dashboard.Units
            .Select(ToDashboardRowView)
            .ToArray();

        DashboardTotalUnitsTextBlock.Text = dashboard.TotalUnits.ToString(CultureInfo.CurrentCulture);
        DashboardWithEntryTextBlock.Text = dashboard.UnitsWithEntry.ToString(CultureInfo.CurrentCulture);
        DashboardMissingTextBlock.Text = dashboard.UnitsMissing.ToString(CultureInfo.CurrentCulture);
        DashboardPendingValidationTextBlock.Text = dashboard.UnitsPendingValidation.ToString(CultureInfo.CurrentCulture);
        DashboardGrandTotalTextBlock.Text = dashboard.GrandTotal.ToString("N2", CultureInfo.CurrentCulture);
    }

    // Projects the API row into a display-friendly shape so a missing entry renders as an
    // explicit "Non saisi" label in the "A saisi" column rather than an ambiguous empty cell.
    private static UnitDashboardRowView ToDashboardRowView(UnitDashboardRow row)
    {
        // Les libelles de statut viennent de DailyRevenueStatusDisplay, source
        // unique de la traduction (grille des recettes, impression et CSV rendent
        // le meme mot, accords au feminin compris). Seule la mention "en attente
        // de validation" est un complement propre au tableau de bord.
        var entryStatusText = !row.HasEntry
            ? "Non saisi"
            : row.Status is not { } status
                ? "Saisi"
                : status == DailyRevenueStatus.Submitted
                    ? $"{DailyRevenueStatusDisplay.ToFrench(status)} — en attente de validation"
                    : DailyRevenueStatusDisplay.ToFrench(status);

        return new UnitDashboardRowView(
            row.HotelUnitCode,
            row.HotelUnitName,
            entryStatusText,
            row.Total,
            row.SubmittedAt,
            row.ValidatedAt);
    }

    private sealed record UnitDashboardRowView(
        string HotelUnitCode,
        string HotelUnitName,
        string EntryStatusText,
        decimal? Total,
        DateTimeOffset? SubmittedAt,
        DateTimeOffset? ValidatedAt);

    private async void RefreshAuditButton_Click(object sender, RoutedEventArgs e)
    {
        await RunApiActionAsync(async () =>
        {
            await LoadAuditLogAsync();
            SetStatus("Journal d'audit actualisé.");
        });
    }

    private async Task LoadAuditLogAsync()
    {
        var from = AuditFromDatePicker.SelectedDate.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(AuditFromDatePicker.SelectedDate.Value.Date, DateTimeKind.Local))
            : (DateTimeOffset?)null;

        var to = AuditToDatePicker.SelectedDate.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(AuditToDatePicker.SelectedDate.Value.Date, DateTimeKind.Local))
                .AddDays(1).AddTicks(-1)
            : (DateTimeOffset?)null;

        var action = string.IsNullOrWhiteSpace(AuditActionTextBox.Text) ? null : AuditActionTextBox.Text.Trim();

        // First page only for now; broader pagination is out of scope for this batch.
        var result = await apiClient.GetAuditLogAsync(ApiBaseUrlTextBox.Text, from, to, action, page: 1, pageSize: 100);

        AuditDataGrid.ItemsSource = result.Items;

        // Accord du pluriel selon le compte, comme ClosingView : jamais de "(s)".
        var totalCountText = result.TotalCount.ToString(CultureInfo.CurrentCulture);

        AuditResultCountTextBlock.Text = result.TotalCount > result.Items.Count
            ? $"Affichage de {result.Items.Count.ToString(CultureInfo.CurrentCulture)} sur {totalCountText} entrées. Affinez les filtres pour voir les entrées plus anciennes."
            : result.TotalCount > 1
                ? $"{totalCountText} entrées."
                : $"{totalCountText} entrée.";
    }

    private async void ValidateRevenueButton_Click(object sender, RoutedEventArgs e)
    {
        await RunApiActionAsync(async () =>
        {
            if (DailyRevenueDataGrid.SelectedItem is not DailyRevenueResponse selected)
            {
                SetStatus("Sélectionnez une recette à valider.", isError: true);
                return;
            }

            await apiClient.ValidateDailyRevenueAsync(ApiBaseUrlTextBox.Text, selected.Id);
            await LoadDailyRevenueAsync();
            SetStatus("Recette validée.");
        });
    }

    private async void RejectRevenueButton_Click(object sender, RoutedEventArgs e)
    {
        await RunApiActionAsync(async () =>
        {
            if (DailyRevenueDataGrid.SelectedItem is not DailyRevenueResponse selected)
            {
                SetStatus("Sélectionnez une recette à rejeter.", isError: true);
                return;
            }

            var reason = RejectReasonTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(reason))
            {
                SetStatus("Le motif de rejet est requis.", isError: true);
                return;
            }

            await apiClient.RejectDailyRevenueAsync(ApiBaseUrlTextBox.Text, selected.Id, new RejectDailyRevenueRequest(reason));
            RejectReasonTextBox.Text = string.Empty;
            await LoadDailyRevenueAsync();
            SetStatus("Recette rejetée.");
        });
    }

    private void ExportRevenueCsvButton_Click(object sender, RoutedEventArgs e)
    {
        // DataGrid.Items (not ItemsSource) reflects the grid's live sort order if the user
        // has clicked a column header, so the export matches what is actually on screen.
        IReadOnlyCollection<DailyRevenueResponse> rows = DailyRevenueDataGrid.Items.Cast<DailyRevenueResponse>().ToArray();

        if (rows.Count == 0)
        {
            SetStatus("Aucune recette à exporter.", isError: true);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Fichiers CSV (*.csv)|*.csv",
            FileName = $"recettes-journalieres-{DateTime.Today:yyyy-MM-dd}.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var csv = CsvExportHelper.BuildDailyRevenueCsv(rows);
            CsvExportHelper.WriteCsvFile(dialog.FileName, csv);
            SetStatus("Export CSV terminé.");
        }
        catch (Exception ex)
        {
            SetStatus($"Échec de l'export CSV : {ex.Message}", isError: true);
        }
    }

    private void PrintRevenueButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var printDialog = new PrintDialog();

            if (printDialog.ShowDialog() != true)
            {
                return;
            }

            IReadOnlyCollection<DailyRevenueResponse> rows = DailyRevenueDataGrid.Items.Cast<DailyRevenueResponse>().ToArray();
            var document = BuildDailyRevenuePrintDocument(rows);

            printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Raqmi System — Recettes journalières");
            SetStatus("Document envoyé à l'imprimante.");
        }
        catch (Exception ex)
        {
            SetStatus($"Échec de l'impression : {ex.Message}", isError: true);
        }
    }

    private FlowDocument BuildDailyRevenuePrintDocument(IReadOnlyCollection<DailyRevenueResponse> rows)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(40),
            // Police de marque du theme (Manrope, Noto Kufi Arabic, repli Segoe UI) :
            // le document imprime porte la meme identite que l'ecran.
            FontFamily = (FontFamily)FindResource("AppFontFamily")
        };

        document.Blocks.Add(new Paragraph(new Run("Raqmi System — Recettes journalières"))
        {
            FontSize = 20,
            FontWeight = FontWeights.SemiBold
        });

        document.Blocks.Add(new Paragraph(new Run(
            $"Date d'exploitation : {GetSelectedBusinessDate().ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)}")));

        document.Blocks.Add(new Paragraph(new Run(
            $"Total : {SummaryTotalTextBlock.Text}  |  Brouillons : {SummaryDraftTextBlock.Text}  |  " +
            $"Soumises : {SummarySubmittedTextBlock.Text}  |  Validées : {SummaryValidatedTextBlock.Text}  |  " +
            $"Rejetées : {SummaryRejectedTextBlock.Text}"))
        {
            Margin = new Thickness(0, 4, 0, 16)
        });

        var table = new Table();

        for (var i = 0; i < 9; i++)
        {
            table.Columns.Add(new TableColumn());
        }

        var rowGroup = new TableRowGroup();
        table.RowGroups.Add(rowGroup);

        var headerRow = new TableRow { FontWeight = FontWeights.SemiBold };

        foreach (var header in new[] { "Date", "Unité", "Hébergement", "Restauration", "Boissons", "Autres", "Total", "Statut", "Saisi par" })
        {
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run(header))) { Padding = new Thickness(4) });
        }

        rowGroup.Rows.Add(headerRow);

        foreach (var row in rows)
        {
            var tableRow = new TableRow();

            foreach (var value in new[]
            {
                row.BusinessDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture),
                row.HotelUnitCode,
                row.Accommodation.ToString("N2", CultureInfo.CurrentCulture),
                row.Food.ToString("N2", CultureInfo.CurrentCulture),
                row.Beverage.ToString("N2", CultureInfo.CurrentCulture),
                row.Other.ToString("N2", CultureInfo.CurrentCulture),
                row.Total.ToString("N2", CultureInfo.CurrentCulture),
                DailyRevenueStatusDisplay.ToFrench(row.Status),
                row.CreatedBy
            })
            {
                tableRow.Cells.Add(new TableCell(new Paragraph(new Run(value))) { Padding = new Thickness(4) });
            }

            rowGroup.Rows.Add(tableRow);
        }

        document.Blocks.Add(table);

        return document;
    }

    private void ExportAuditCsvButton_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyCollection<AuditLogSummary> rows = AuditDataGrid.Items.Cast<AuditLogSummary>().ToArray();

        if (rows.Count == 0)
        {
            SetStatus("Aucune entrée d'audit à exporter.", isError: true);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Fichiers CSV (*.csv)|*.csv",
            FileName = $"journal-audit-{DateTime.Today:yyyy-MM-dd}.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var csv = CsvExportHelper.BuildAuditLogCsv(rows);
            CsvExportHelper.WriteCsvFile(dialog.FileName, csv);
            SetStatus("Export CSV terminé.");
        }
        catch (Exception ex)
        {
            SetStatus($"Échec de l'export CSV : {ex.Message}", isError: true);
        }
    }

    private void UnitsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (UnitsDataGrid.SelectedItem is not HotelUnitResponse selected)
        {
            return;
        }

        editingUnitCode = selected.Code;
        UnitFormTitleTextBlock.Text = $"Modifier {selected.Code}";
        UnitCodeTextBox.Text = selected.Code;
        UnitCodeTextBox.IsEnabled = false;
        UnitNameTextBox.Text = selected.Name;
        UnitTypeComboBox.SelectedItem = selected.UnitType;
        UnitDisplayOrderTextBox.Text = selected.DisplayOrder.ToString(CultureInfo.CurrentCulture);
        SaveUnitButton.Content = "Modifier";
    }

    private void NewUnitButton_Click(object sender, RoutedEventArgs e)
    {
        ResetUnitForm();
    }

    private void ResetUnitForm()
    {
        editingUnitCode = null;
        UnitFormTitleTextBlock.Text = "Nouvelle unité";
        UnitCodeTextBox.Text = string.Empty;
        UnitCodeTextBox.IsEnabled = true;
        UnitNameTextBox.Text = string.Empty;
        UnitTypeComboBox.SelectedItem = HotelUnitType.Hotel;
        UnitDisplayOrderTextBox.Text = "0";
        SaveUnitButton.Content = "Créer";
        UnitsDataGrid.SelectedItem = null;
    }

    private async void SaveUnitButton_Click(object sender, RoutedEventArgs e)
    {
        await RunApiActionAsync(async () =>
        {
            var code = UnitCodeTextBox.Text.Trim();
            var name = UnitNameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            {
                SetStatus("Code et nom sont requis.", isError: true);
                return;
            }

            if (UnitTypeComboBox.SelectedItem is not HotelUnitType unitType)
            {
                SetStatus("Sélectionnez un type d'unité.", isError: true);
                return;
            }

            if (!int.TryParse(UnitDisplayOrderTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var displayOrder))
            {
                SetStatus("L'ordre d'affichage doit être un nombre entier.", isError: true);
                return;
            }

            if (editingUnitCode is null)
            {
                await apiClient.CreateHotelUnitAsync(
                    ApiBaseUrlTextBox.Text,
                    new CreateHotelUnitRequest(code, name, unitType, displayOrder));
                SetStatus("Unité créée.");
            }
            else
            {
                await apiClient.UpdateHotelUnitAsync(
                    ApiBaseUrlTextBox.Text,
                    editingUnitCode,
                    new UpdateHotelUnitRequest(name, unitType, displayOrder));
                SetStatus("Unité mise à jour.");
            }

            ResetUnitForm();
            await LoadHotelUnitsAsync();
        });
    }

    private async void ActivateUnitButton_Click(object sender, RoutedEventArgs e)
    {
        await SetUnitActiveAsync(isActive: true);
    }

    private async void DeactivateUnitButton_Click(object sender, RoutedEventArgs e)
    {
        await SetUnitActiveAsync(isActive: false);
    }

    private async Task SetUnitActiveAsync(bool isActive)
    {
        if (UnitsDataGrid.SelectedItem is not HotelUnitResponse selected)
        {
            SetStatus("Sélectionnez une unité.", isError: true);
            return;
        }

        // Acte engageant sur le referentiel : la desactivation est confirmee avec
        // le gabarit de la charte (fenetre proprietaire, icone Warning, defaut Non),
        // comme la desactivation d'un client ou d'un compte bancaire. L'activation,
        // elle, reste sans confirmation (meme choix que CustomersView).
        if (!isActive)
        {
            var confirmation = MessageBox.Show(
                this,
                $"Désactiver l'unité {selected.Code} — {selected.Name} ?\n\n" +
                "Elle ne sera plus proposée à la saisie des recettes journalières tant qu'elle n'aura pas été réactivée.",
                "Désactivation d'une unité",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }
        }

        await RunApiActionAsync(async () =>
        {
            await apiClient.SetHotelUnitActiveAsync(ApiBaseUrlTextBox.Text, selected.Code, isActive);
            await LoadHotelUnitsAsync();
            SetStatus(isActive ? "Unité activée." : "Unité désactivée.");
        });
    }

    private CreateDailyRevenueRequest? BuildRevenueRequest()
    {
        if (RevenueUnitComboBox.SelectedItem is not HotelUnitResponse selectedUnit)
        {
            SetStatus("Sélectionnez une unité hôtelière.", isError: true);
            return null;
        }

        if (!TryReadMoney(AccommodationTextBox, "Hébergement", out var accommodation) ||
            !TryReadMoney(FoodTextBox, "Restauration", out var food) ||
            !TryReadMoney(BeverageTextBox, "Boissons", out var beverage) ||
            !TryReadMoney(OtherTextBox, "Autres", out var other))
        {
            return null;
        }

        return new CreateDailyRevenueRequest(
            GetSelectedBusinessDate(),
            selectedUnit.Code,
            accommodation,
            food,
            beverage,
            other,
            string.IsNullOrWhiteSpace(NotesTextBox.Text) ? null : NotesTextBox.Text.Trim());
    }

    private bool TryReadMoney(TextBox textBox, string label, out decimal value)
    {
        var text = textBox.Text.Trim();

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value) ||
            decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            if (value < 0)
            {
                SetStatus($"{label} ne peut pas être négatif.", isError: true);
                return false;
            }

            return true;
        }

        SetStatus($"{label} doit être un montant valide.", isError: true);
        return false;
    }

    private DateOnly GetSelectedBusinessDate()
    {
        var date = BusinessDatePicker.SelectedDate ?? DateTime.Today;
        return DateOnly.FromDateTime(date);
    }

    private void ResetAmounts()
    {
        AccommodationTextBox.Text = "0";
        FoodTextBox.Text = "0";
        BeverageTextBox.Text = "0";
        OtherTextBox.Text = "0";
        NotesTextBox.Text = string.Empty;
    }

    private async Task RunApiActionAsync(Func<Task> action)
    {
        SetBusy(true);

        try
        {
            await action();
        }
        catch (ApiRequestFailedException ex)
        {
            SetStatus($"API {(int)ex.StatusCode}: {ex.Message}", isError: true);
        }
        catch (HttpRequestException ex)
        {
            SetStatus($"API indisponible : {ex.Message}", isError: true);
        }
        catch (InvalidOperationException ex)
        {
            SetStatus(ex.Message, isError: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        this.isBusy = isBusy;

        Mouse.OverrideCursor = isBusy ? Cursors.Wait : null;
        BusyProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        LoginBusyProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        LoginButton.IsEnabled = !isBusy;
        ChangePasswordButton.IsEnabled = !isBusy;
        RefreshUnitsButton.IsEnabled = !isBusy;
        RefreshRevenueButton.IsEnabled = !isBusy;
        RefreshDashboardButton.IsEnabled = !isBusy;
        RefreshAuditButton.IsEnabled = !isBusy;
        NewUnitButton.IsEnabled = !isBusy;

        // Les actions d'ecriture (unites et recettes) croisent l'etat busy avec les
        // permissions du profil : une seule methode porte cette verite, pour que le
        // retour de SetBusy(false) ne reactive pas un bouton sans droit.
        ApplyWriteActionStates();

        // Les vues de module (Cloture, Tresorerie, Clients, Facturation,
        // Parametrage global, Administration et utilisateurs, Comptabilite, Budget,
        // Creances, Tarifs, Hebergement) portent leurs propres boutons, que cette
        // methode ne peut pas enumerer un par un.
        // Neutraliser tout le conteneur d'onglets pendant un appel en vol empeche
        // la double soumission (double clic = deux factures, deux encaissements,
        // deux cloctures) sans avoir a maintenir une liste de boutons par vue.
        // La barre laterale reste active : changer d'onglet pendant un appel est
        // sans danger, les vues rechargent leurs donnees a l'ouverture.
        MainTabs.IsEnabled = !isBusy;
    }

    // Mirrors the message onto both status surfaces: the sidebar's (visible once signed in)
    // and the login card's (visible while signed out, e.g. for a wrong-password message) -
    // exactly one of the two is ever on screen at a time, so this never reads as duplicated.
    private void SetStatus(string message, bool isError = false)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = (Brush)FindResource(isError ? "DangerBrush" : "TextSecondaryBrush");

        // Sur la carte de connexion, le message est presente dans un encart stylise
        // dont l'apparence (info/erreur) est pilotee par le Tag (voir MainWindow.xaml).
        LoginStatusTextBlock.Text = message;
        LoginStatusBorder.Tag = isError ? "Error" : "Info";
        LoginStatusBorder.Visibility = string.IsNullOrWhiteSpace(message)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
