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
using RaqmiSystem.Application.Navigation;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Sync;
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

    // Onglets des vues de module autonomes (onglets 5 a 20 : ClosingView jusqu'a
    // DecCockpitView) deja charges depuis la connexion en cours. Ces vues sont
    // chargees paresseusement - a la premiere ouverture de leur onglet - pour ne
    // pas declencher autant de series d'appels reseau inutiles a chaque connexion.
    // Vide a la deconnexion : la session suivante repart de donnees fraiches.
    private readonly HashSet<int> loadedModuleTabs = [];

    public MainWindow()
    {
        // Construits AVANT InitializeComponent : le TabControl choisit son premier
        // onglet pendant l'analyse du XAML, ce qui declenche MainTabs_SelectionChanged,
        // qui lit deja ces collections (voir MainWindow.Navigation.cs).
        sidebarGroups = ModuleNavigationGroup.Build(FunctionalArchitectureCatalog.Tree);
        tilesByTab = BuildTilesByTab(moduleTiles);

        InitializeComponent();

        // Aligne les StringFormat du XAML (montants N2, dates) sur la culture du
        // poste : sans cela WPF formate en en-US pendant que le code-behind formate
        // en culture courante, et deux formats coexistent sur le meme ecran.
        Language = XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);

        // L'icone de bascule doit annoncer la bonne destination des la premiere image :
        // le theme a deja ete applique par App.OnStartup, il ne reste qu'a s'y accorder.
        SyncThemeToggle();

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
        InitializeNavigation();
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

    // « Ma sécurité » de Mon Espace : la meme boite que le bouton de l'en-tete, jamais
    // une seconde implementation du changement de mot de passe.
    private void HomeView_ChangePasswordRequested()
    {
        ShowChangePasswordDialog(isMandatory: false);
    }

    // Une cle est detenue des qu'une des claims que le SERVEUR accepte pour elle figure
    // dans le jeton (PermissionRegistry.AcceptedClaims) : c'est la regle exacte des
    // politiques de l'API. La comparer litteralement verrouillerait chez un role
    // personnalise porteur de cles cibles des ecrans que l'API lui ouvre - un masquage
    // qui ment, alors que le masquage n'est de toute facon jamais une securite.
    private bool HasModulePermission(string permission)
    {
        return currentUserPermissions is null
            || PermissionRegistry.AcceptedClaims(permission)
                .Any(claim => currentUserPermissions.Contains(claim, StringComparer.OrdinalIgnoreCase));
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
            HomeView.OpenSession(login.User);
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
            NavigateToModule(HomeTabIndex);

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

        // Les files de travail de Mon Espace en dernier, et HORS du RunApiActionAsync de
        // la connexion, pour la meme raison que la boite ci-dessus : LoadAsync fait UNE
        // RunAsync PAR SOURCE, et les imbriquer rallumerait MainTabs a chaque source
        // terminee alors que la connexion est encore en cours. L'ecran est deja affiche :
        // ses cartes se remplissent au fil des reponses.
        if (apiClient.IsAuthenticated)
        {
            await HomeView.LoadAsync();
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
        ApprovalsView.Initialize(context);
        ReportsView.Initialize(context);
        BackupView.Initialize(context);
        SyncView.Initialize(context);
        MiceView.Initialize(context);
        GroupDashboardView.Initialize(context);
        KpiView.Initialize(context);
        PmsView.Initialize(context);
        DecCockpitView.Initialize(context);
        HousekeepingView.Initialize(context);
        HumanResourcesView.Initialize(context);
        InventoryView.Initialize(context);
        PurchasingView.Initialize(context);
        KitchenView.Initialize(context);
        CrmView.Initialize(context);

        // Le cockpit DEC ne connait pas MainWindow : ses files de travail DEMANDENT
        // l'ouverture du module concerne via NavigateRequested, et c'est la fenetre - seule a
        // connaitre les onglets et la barre laterale - qui execute la navigation.
        // Desabonnement systematique avant abonnement : InitializeModuleViews est rappelee a
        // CHAQUE connexion, et un simple "+=" empilerait un handler de plus par session, donc
        // autant de navigations pour un seul clic.
        DecCockpitView.NavigateRequested -= DecCockpitView_NavigateRequested;
        DecCockpitView.NavigateRequested += DecCockpitView_NavigateRequested;

        // Mon Espace suit la meme regle : il compose ses files des seules cles du jeton,
        // demande la navigation, et delegue « Ma sécurité » a la boite deja existante.
        HomeView.Initialize(context);
        HomeView.ChangePasswordRequested -= HomeView_ChangePasswordRequested;
        HomeView.ChangePasswordRequested += HomeView_ChangePasswordRequested;

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
        ApprovalsView.ResetState();
        ReportsView.ResetState();
        BackupView.ResetState();
        SyncView.ResetState();
        MiceView.ResetState();
        PmsView.ResetState();

        // Remis a zero pour que la prochaine session batte immediatement : le registre doit
        // refleter le NOUVEL utilisateur du poste sans attendre cinq minutes.
        lastHeartbeatUtc = DateTimeOffset.MinValue;
        GroupDashboardView.ResetState();
        DecCockpitView.ResetState();
        HousekeepingView.ResetState();
        HumanResourcesView.ResetState();
        InventoryView.ResetState();
        PurchasingView.ResetState();
        KitchenView.ResetState();
        CrmView.ResetState();
        loadedModuleTabs.Clear();

        ResetUnitForm();
        ResetAmounts();

        // L'ecran d'accueil revient a son etat par defaut : salutation neutre, files de
        // travail videes, toutes les cartes et boutons de modules de nouveau accessibles.
        // Les reglages du POSTE (unite, derniers ecrans) survivent : ils ne sont pas ceux
        // de la personne qui part.
        currentUserPermissions = null;
        currentUserId = null;
        HomeView.ResetState();
        ApplyModulePermissions();

        // A la reconnexion, la session reprendra sur l'ecran d'accueil.
        NavigateToModule(HomeTabIndex);

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
        catch (OperationCanceledException)
        {
            // Depuis .NET 5, un depassement de HttpClient.Timeout (100 s par defaut, aucun
            // Timeout n'etant configure ici) leve TaskCanceledException, qui derive
            // d'OperationCanceledException et NON de HttpRequestException. Sans ce catch elle
            // n'etait attrapee par personne : comme les gestionnaires WPF sont async void, elle
            // remontait au Dispatcher et FERMAIT l'application, sans message, environ 100
            // secondes apres une coupure reseau silencieuse (cable, switch, serveur fige).
            // Aucune annulation volontaire n'existe dans ce client (aucun CancellationTokenSource,
            // aucun appel a Cancel), donc ce cas ne peut etre qu'un delai depasse.
            SetStatus(
                "Le serveur n'a pas repondu a temps. Verifiez le reseau puis reessayez.",
                isError: true);
        }
        catch (InvalidOperationException ex)
        {
            SetStatus(ex.Message, isError: true);
        }
        finally
        {
            SetBusy(false);
        }

        // Hors du try/finally : le battement ne doit ni retarder la remise a l'etat normal de
        // l'interface, ni pouvoir en perturber le deroulement.
        await SendHeartbeatIfDueAsync();
    }

    // ==================== Module 29 : battement de ce poste ====================

    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(5);

    private DateTimeOffset lastHeartbeatUtc = DateTimeOffset.MinValue;

    /// <summary>
    /// Signale au serveur que ce poste est actif, au plus une fois toutes les cinq minutes, en
    /// profitant d'un appel metier deja effectue (aucun Timer, aucun thread : le jeton du client
    /// API n'est pas synchronise et tout ce client est monothread - cette hypothese ne doit pas
    /// etre cassee pour une fonction de confort).
    ///
    /// Rien ici ne peut faire echouer le travail de l'operateur : toute exception est avalee, et
    /// aucun message n'est affiche. Un registre incomplet est un desagrement ; une saisie perdue
    /// parce qu'un battement a echoue serait une faute.
    /// </summary>
    private async Task SendHeartbeatIfDueAsync()
    {
        if (!apiClient.IsAuthenticated)
        {
            return;
        }

        var nowUtc = DateTimeOffset.UtcNow;

        if (nowUtc - lastHeartbeatUtc < HeartbeatInterval)
        {
            return;
        }

        // Marque AVANT l'appel : si le reseau est coupe, l'appel prend du temps et echoue, et on
        // ne veut pas qu'un second battement parte a chaque action pendant toute la panne.
        lastHeartbeatUtc = nowUtc;

        try
        {
            var apiBaseUrl = ApiBaseUrlTextBox.Text;

            var unitCode = RevenueUnitComboBox.SelectedItem is HotelUnitResponse selectedUnit
                ? selectedUnit.Code
                : null;

            await apiClient.SendHeartbeatAsync(
                apiBaseUrl,
                new WorkstationHeartbeatRequest(
                    StationIdentity.StationId,
                    StationIdentity.Label,
                    StationIdentity.AppVersion,
                    unitCode));

            var pending = apiClient.Failures.DrainUpTo(ClientFailureBuffer.Capacity);

            if (pending.Count > 0)
            {
                await apiClient.ReportWorkstationFailuresAsync(
                    apiBaseUrl,
                    new ReportWorkstationFailuresRequest(
                        StationIdentity.StationId,
                        pending
                            .Select(entry => new WorkstationFailureItem(
                                entry.EventId,
                                entry.Method,
                                entry.Path,
                                entry.StatusCode,
                                entry.Kind,
                                entry.Message,
                                entry.ClaimedAtUtc))
                            .ToList()));

                apiClient.Failures.ResetLostCount();
            }
        }
        catch
        {
            // Silence delibere : voir le resume de la methode.
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
        // La barre laterale reste active : changer de module pendant un appel est
        // sans danger, les vues rechargent leurs donnees a l'ouverture.
        MainTabs.IsEnabled = !isBusy;
    }

    // Mirrors the message onto both status surfaces: the session strip at the foot of the
    // module area (visible once signed in, on every screen including the home one) and the
    // login card's (visible while signed out, e.g. for a wrong-password message) - exactly
    // one of the two is ever on screen at a time, so this never reads as duplicated.
    private void SetStatus(string message, bool isError = false)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = (Brush)FindResource(isError ? "DangerBrush" : "TextSecondaryBrush");
        SessionStatusDot.Fill = (Brush)FindResource(isError ? "DangerBrush" : "AccentBrush");
        FlashSessionStrip(isError);

        // Sur la carte de connexion, le message est presente dans un encart stylise
        // dont l'apparence (info/erreur) est pilotee par le Tag (voir MainWindow.xaml).
        LoginStatusTextBlock.Text = message;
        LoginStatusBorder.Tag = isError ? "Error" : "Info";
        LoginStatusBorder.Visibility = string.IsNullOrWhiteSpace(message)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    // Surlignage bref du bandeau de session a chaque nouveau message.
    //
    // Pourquoi : le bandeau est en pied de fenetre, toujours visible mais loin du geste.
    // Apres un clic sur un bouton place en haut d'un ecran defilant, un texte qui change
    // sans bouger passe inapercu - et l'utilisateur reclique, ou croit l'action perdue.
    // Un fond qui s'allume puis s'efface attire l'oeil sans rien deplacer.
    //
    // Une erreur tient plus longtemps (1,6 s contre 0,9 s) et part d'un fond plus dense :
    // elle demande une lecture, la ou un succes ne demande qu'une confirmation du coin de
    // l'oeil. La couleur est relue dans les ressources a chaque appel, donc elle suit le
    // theme clair ou sombre sans code particulier.
    private void FlashSessionStrip(bool isError)
    {
        var depart = ((SolidColorBrush)FindResource(isError ? "DangerSoftBrush" : "AccentSoftBrush")).Color;

        // Un brush par animation : partage, il serait fige par la premiere et le
        // second message ne s'allumerait plus.
        var fond = new SolidColorBrush(depart);
        SessionStatusBorder.Background = fond;

        var extinction = new ColorAnimation
        {
            From = depart,
            To = Colors.Transparent,
            Duration = TimeSpan.FromMilliseconds(isError ? 1600 : 900),
            BeginTime = TimeSpan.FromMilliseconds(isError ? 500 : 200),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
            FillBehavior = FillBehavior.Stop
        };

        // Le fond revient a transparent une fois l'animation retiree : sans cela, le
        // FillBehavior.Stop rendrait au brush sa couleur de depart, et le bandeau
        // resterait allume.
        extinction.Completed += (_, _) => fond.Color = Colors.Transparent;

        fond.BeginAnimation(SolidColorBrush.ColorProperty, extinction);
    }
}
