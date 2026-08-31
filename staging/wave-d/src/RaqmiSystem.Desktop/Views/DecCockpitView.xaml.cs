using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using RaqmiSystem.Application.Pilotage;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Cockpit DEC : le poste de controle quotidien de la Direction de l'Exploitation
/// et du Controle. Files de travail du jour (recettes soumises a valider, retard
/// de cloture, recettes rejetees, ordres de paiement en attente), sante du jour
/// par unite, indicateurs de charge. Lecture pure : tous les chiffres viennent du
/// serveur (GetDecCockpitAsync), la vue ne fait que du formatage d'affichage.
///
/// Vue de module autonome : elle ne connait ni MainWindow ni les autres vues,
/// tout passe par le <see cref="ModuleViewContext"/> recu dans Initialize().
/// La navigation vers les modules concernes est DEMANDEE via l'evenement
/// <see cref="NavigateRequested"/> ; c'est la fenetre qui s'y abonne et navigue.
/// </summary>
public partial class DecCockpitView : UserControl
{
    // Index des onglets cibles dans MainTabs (voir MainWindow.xaml.cs :
    // NavigateToModule / SidebarButtonForTab). La vue ne navigue pas elle-meme :
    // elle transmet cet index via NavigateRequested.
    private const int RevenueTabIndex = 2;
    private const int ClosingTabIndex = 5;
    private const int TreasuryTabIndex = 6;

    private ModuleViewContext? context;

    /// <summary>
    /// Demande de navigation vers l'onglet de module donne (index de MainTabs).
    /// Cablage attendu cote fenetre, dans InitializeModuleViews :
    /// DecCockpitView.NavigateRequested += tabIndex =&gt;
    ///     NavigateToModule(tabIndex, SidebarButtonForTab(tabIndex) ?? ShowHomeButton);
    /// </summary>
    public event Action<int>? NavigateRequested;

    public DecCockpitView()
    {
        InitializeComponent();

        // Les StringFormat XAML (N2, dd/MM/yyyy) suivent la meme culture que le
        // code : sans cela, WPF formate en en-US quelle que soit la culture du poste.
        Language = XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);

        InitializeDefaults();
    }

    /// <summary>
    /// Memorise le contexte prete par la fenetre. Aucun appel reseau ici : le
    /// premier chargement est declenche par LoadAsync(). Module en lecture pure
    /// sous dashboard.read : aucun bouton d'ecriture a conditionner.
    /// </summary>
    public void Initialize(ModuleViewContext context)
    {
        this.context = context;
    }

    /// <summary>
    /// (Re)charge le cockpit pour la date selectionnee. Sort silencieusement tant
    /// qu'aucun contexte n'est fourni ou que l'utilisateur n'est pas connecte.
    /// </summary>
    public async Task LoadAsync()
    {
        var current = context;

        if (current is null || !current.ApiClient.IsAuthenticated)
        {
            return;
        }

        await current.RunAsync(() => LoadCockpitAsync(current));
    }

    /// <summary>
    /// Vide grilles et indicateurs : appele a la deconnexion pour ne jamais
    /// laisser les donnees d'un utilisateur a l'ecran du suivant.
    /// </summary>
    public void ResetState()
    {
        PendingValidationDataGrid.ItemsSource = null;
        ClosingBacklogDataGrid.ItemsSource = null;
        RejectedDataGrid.ItemsSource = null;
        PaymentOrdersDataGrid.ItemsSource = null;
        HealthDataGrid.ItemsSource = null;

        PendingValidationCountText.Text = "0";
        ClosingBacklogCountText.Text = "0";
        RejectedCountText.Text = "0";
        PaymentOrdersCountText.Text = "0";

        IndicatorPendingCountText.Text = "—";
        IndicatorPendingAmountText.Text = "—";
        IndicatorOldestClosingText.Text = "—";

        InitializeDefaults();
    }

    // Date par defaut : aujourd'hui, en heure locale du poste (DateTime.Today,
    // jamais l'horloge UTC - convention constante du depot pour les calendriers).
    private void InitializeDefaults()
    {
        CockpitDatePicker.SelectedDate = DateTime.Today;
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || !current.ApiClient.IsAuthenticated)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await LoadCockpitAsync(current);
            current.SetStatus("Cockpit DEC actualisé.");
        });
    }

    private async Task LoadCockpitAsync(ModuleViewContext current)
    {
        var date = CockpitDatePicker.SelectedDate is DateTime selected
            ? DateOnly.FromDateTime(selected)
            : DateOnly.FromDateTime(DateTime.Today);

        var cockpit = await current.ApiClient.GetDecCockpitAsync(current.ApiBaseUrl, date);
        var culture = CultureInfo.CurrentCulture;

        // Files de travail : listes deja triees par age decroissant par le serveur.
        PendingValidationDataGrid.ItemsSource = cockpit.PendingValidations;
        ClosingBacklogDataGrid.ItemsSource = cockpit.ClosingBacklog
            .Select(unit => BacklogRow.From(unit, culture))
            .ToArray();
        RejectedDataGrid.ItemsSource = cockpit.RejectedRevenues;
        PaymentOrdersDataGrid.ItemsSource = cockpit.PendingPaymentOrders;
        HealthDataGrid.ItemsSource = cockpit.UnitHealth
            .Select(row => HealthRow.From(row, culture))
            .ToArray();

        // Les gros nombres des cartes : ce sont les compteurs renvoyes par l'API,
        // jamais un Items.Count recalcule localement.
        PendingValidationCountText.Text = cockpit.PendingValidationCount.ToString(culture);
        ClosingBacklogCountText.Text = cockpit.ClosingBacklogDayCount.ToString(culture);
        RejectedCountText.Text = cockpit.RejectedCount.ToString(culture);
        PaymentOrdersCountText.Text = cockpit.PendingPaymentOrderCount.ToString(culture);

        IndicatorPendingCountText.Text = cockpit.PendingValidationCount.ToString(culture);
        IndicatorPendingAmountText.Text = cockpit.PendingValidationAmount.ToString("N2", culture);
        IndicatorOldestClosingText.Text = cockpit.OldestClosingDelay is { } delay
            ? string.Format(
                culture,
                "{0} — {1} ({2} j)",
                delay.HotelUnitCode,
                delay.BusinessDate.ToString("dd/MM/yyyy", culture),
                delay.AgeDays)
            : "Aucun";

        HealthSubtitleTextBlock.Text = string.Format(
            culture,
            "Recette du {0}, clôture du {0} et occupation du {1}. Une unité sans recette d'hier et sans clôture est surlignée.",
            cockpit.Yesterday.ToString("dd/MM/yyyy", culture),
            cockpit.Date.ToString("dd/MM/yyyy", culture));
    }

    // Boutons "Ouvrir" des files de travail : la vue demande la navigation, la
    // fenetre l'execute (aucune reference a MainWindow ici). Le module cible
    // applique ses propres permissions de lecture.
    private void OpenRevenueRow_Click(object sender, RoutedEventArgs e)
    {
        NavigateRequested?.Invoke(RevenueTabIndex);
    }

    private void OpenClosingRow_Click(object sender, RoutedEventArgs e)
    {
        NavigateRequested?.Invoke(ClosingTabIndex);
    }

    private void OpenTreasuryRow_Click(object sender, RoutedEventArgs e)
    {
        NavigateRequested?.Invoke(TreasuryTabIndex);
    }

    /// <summary>
    /// Ligne d'affichage du retard de cloture : formatage pur (les dates
    /// manquantes jointes en jj/MM), aucun chiffre recalcule.
    /// </summary>
    private sealed record BacklogRow(
        string HotelUnitCode,
        string? HotelUnitName,
        int MissingCount,
        string MissingDatesDisplay,
        DateOnly OldestMissingDate,
        int OldestAgeDays)
    {
        public static BacklogRow From(DecClosingBacklogUnit unit, CultureInfo culture)
        {
            return new BacklogRow(
                unit.HotelUnitCode,
                unit.HotelUnitName,
                unit.MissingDates.Count,
                string.Join(", ", unit.MissingDates.Select(day => day.ToString("dd/MM", culture))),
                unit.OldestMissingDate,
                unit.OldestAgeDays);
        }
    }

    /// <summary>
    /// Ligne d'affichage de la sante d'une unite : formatage pur des chiffres
    /// serveur. La recette d'hier est la validee, ou a defaut la soumise - le
    /// badge "Soumise" la marque alors comme provisoire ; un brouillon ou une
    /// rejetee n'est pas un chiffre exploitable et s'affiche en tiret.
    /// </summary>
    private sealed record HealthRow(
        string HotelUnitCode,
        string HotelUnitName,
        DailyRevenueStatus? RevenueStatus,
        string RevenueDisplay,
        bool YesterdayClosed,
        string OccupancyDisplay,
        bool NeedsAttention)
    {
        public static HealthRow From(DecUnitHealthRow row, CultureInfo culture)
        {
            var revenueDisplay = row.YesterdayRevenueTotal is { } total
                ? total.ToString("N2", culture)
                : "—";

            var occupancyDisplay = row.OccupancyRatePercent is { } rate
                ? string.Format(culture, "{0}/{1} · {2} %", row.OccupiedRooms, row.ActiveRooms, rate.ToString("N0", culture))
                : "—";

            return new HealthRow(
                row.HotelUnitCode,
                row.HotelUnitName,
                row.YesterdayRevenueStatus,
                revenueDisplay,
                row.YesterdayClosed,
                occupancyDisplay,
                row.NeedsAttention);
        }
    }
}
