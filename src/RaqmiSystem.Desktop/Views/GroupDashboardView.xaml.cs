using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using RaqmiSystem.Application.Pilotage;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Dashboard PDG (module 24.2) : la vision groupe de la direction. Toutes les unites d'un
/// coup, sur une periode, avec comparaison N-1, tableau des unites classees par chiffre
/// d'affaires et alertes factuelles.
///
/// Ecran de LECTURE PURE : aucune action d'ecriture, donc aucune permission a griser - la
/// lecture est gardee par dashboard.read cote serveur, et la carte du catalogue est
/// verrouillee sans ce droit. Tous les chiffres affiches viennent du serveur ; la vue ne
/// recalcule rien (la barre de proportion et les fleches de tendance ne sont que la mise en
/// forme de pourcentages renvoyes par l'API).
///
/// Vue de module autonome : elle ne connait ni MainWindow ni les autres vues, tout passe par
/// le <see cref="ModuleViewContext"/> recu dans Initialize().
/// </summary>
public partial class GroupDashboardView : UserControl
{
    private ModuleViewContext? context;

    public GroupDashboardView()
    {
        InitializeComponent();

        // Les StringFormat XAML (montants N2) suivent la culture du poste, comme le code -
        // meme motif que la vue Factures.
        Language = XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);

        InitializeDefaults();
    }

    /// <summary>
    /// Memorise le contexte prete par la fenetre. Aucun appel reseau ici : le premier
    /// chargement est declenche par LoadAsync(). Aucune permission a relever non plus :
    /// l'ecran n'expose aucune ecriture.
    /// </summary>
    public void Initialize(ModuleViewContext context)
    {
        this.context = context;
    }

    /// <summary>
    /// (Re)charge le tableau de bord groupe sur la periode saisie. Sort silencieusement tant
    /// qu'aucun contexte n'est fourni ou que l'utilisateur n'est pas connecte.
    /// </summary>
    public async Task LoadAsync()
    {
        var current = context;

        if (current is null || !current.ApiClient.IsAuthenticated)
        {
            return;
        }

        await current.RunAsync(async () => await LoadDashboardAsync(current));
    }

    /// <summary>
    /// Vide tuiles, grilles et periode : appele a la deconnexion pour ne jamais laisser les
    /// chiffres d'un utilisateur a l'ecran du suivant.
    /// </summary>
    public void ResetState()
    {
        UnitsDataGrid.ItemsSource = null;
        AlertsDataGrid.ItemsSource = null;
        PeriodSummaryTextBlock.Text = string.Empty;
        BasisCardBorder.ToolTip = null;
        InitializeDefaults();
    }

    // Periode par defaut : le mois courant, en heure locale du poste (DateTime.Today, jamais
    // l'horloge UTC - meme motif que ClosingView).
    private void InitializeDefaults()
    {
        var today = DateTime.Today;

        FromDatePicker.SelectedDate = new DateTime(today.Year, today.Month, 1);
        ToDatePicker.SelectedDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

        ClearKpis();
    }

    private void ClearKpis()
    {
        RevenueValueText.Text = "—";
        ReceiptsValueText.Text = "—";
        ReceivablesValueText.Text = "—";
        OccupancyValueText.Text = "—";
        UnitsValueText.Text = "—";
        OccupancyNightsText.Text = string.Empty;
        UnitsTrendText.Text = string.Empty;

        ClearTrend(RevenueTrendPath, RevenueTrendText);
        ClearTrend(ReceiptsTrendPath, ReceiptsTrendText);
        ClearTrend(ReceivablesTrendPath, ReceivablesTrendText);
        ClearTrend(OccupancyTrendPath, OccupancyTrendText);
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    private async Task LoadDashboardAsync(ModuleViewContext current)
    {
        if (FromDatePicker.SelectedDate is not DateTime fromDate ||
            ToDatePicker.SelectedDate is not DateTime toDate)
        {
            current.SetStatus("Sélectionnez une période complète (dates « du » et « au »).", isError: true);
            return;
        }

        // Simple confort de saisie : la meme regle est appliquee (et fait autorite) cote
        // serveur, qui repond en validation si les bornes sont inversees.
        if (toDate < fromDate)
        {
            current.SetStatus("La date « du » doit précéder la date « au ».", isError: true);
            return;
        }

        var dashboard = await current.ApiClient.GetGroupDashboardAsync(
            current.ApiBaseUrl,
            DateOnly.FromDateTime(fromDate),
            DateOnly.FromDateTime(toDate));

        ApplyKpis(dashboard);

        UnitsDataGrid.ItemsSource = dashboard.Units.Select(BuildUnitRow).ToArray();
        AlertsDataGrid.ItemsSource = dashboard.Alerts.Select(BuildAlertRow).ToArray();

        PeriodSummaryTextBlock.Text = string.Format(
            CultureInfo.CurrentCulture,
            "Période du {0:dd/MM/yyyy} au {1:dd/MM/yyyy}, comparée du {2:dd/MM/yyyy} au {3:dd/MM/yyyy} (N-1).",
            dashboard.From.ToDateTime(TimeOnly.MinValue),
            dashboard.To.ToDateTime(TimeOnly.MinValue),
            dashboard.PreviousFrom.ToDateTime(TimeOnly.MinValue),
            dashboard.PreviousTo.ToDateTime(TimeOnly.MinValue));

        // Le texte EXACT du serveur (GroupDashboardBasis) en info-bulle de la carte « Ce que
        // ces chiffres comptent » : si la lecture francaise et la regle du serveur venaient a
        // diverger, l'ecart serait visible ici.
        BasisCardBorder.ToolTip = string.Join(
            Environment.NewLine,
            dashboard.Basis.Revenue,
            dashboard.Basis.Receipts,
            dashboard.Basis.Receivables,
            dashboard.Basis.Occupancy,
            dashboard.Basis.Closing);

        current.SetStatus("Tableau de bord groupe actualisé.");
    }

    private void ApplyKpis(GroupDashboardResponse dashboard)
    {
        var culture = CultureInfo.CurrentCulture;

        RevenueValueText.Text = dashboard.Kpis.ValidatedRevenue.ToString("N2", culture);
        ReceiptsValueText.Text = dashboard.Kpis.ConfirmedReceipts.ToString("N2", culture);
        ReceivablesValueText.Text = dashboard.Kpis.OutstandingReceivables.ToString("N2", culture);
        OccupancyValueText.Text = FormatRate(dashboard.Kpis.OccupancyRatePercent);
        UnitsValueText.Text = dashboard.Kpis.ActiveUnitCount.ToString("N0", culture);
        UnitsTrendText.Text = "Sans comparaison N-1";

        OccupancyNightsText.Text = string.Format(
            culture,
            "{0:N0} nuits occupées / {1:N0} disponibles",
            dashboard.Kpis.OccupiedNights,
            dashboard.Kpis.AvailableNights);

        ApplyTrend(RevenueTrendPath, RevenueTrendText, dashboard.Variations.RevenuePercent, increaseIsGood: true);
        ApplyTrend(ReceiptsTrendPath, ReceiptsTrendText, dashboard.Variations.ReceiptsPercent, increaseIsGood: true);

        // Un encours clients qui MONTE est une degradation : la teinte semantique s'inverse.
        ApplyTrend(ReceivablesTrendPath, ReceivablesTrendText, dashboard.Variations.ReceivablesPercent, increaseIsGood: false);
        ApplyTrend(OccupancyTrendPath, OccupancyTrendText, dashboard.Variations.OccupancyPercent, increaseIsGood: true);
    }

    /// <summary>
    /// Affiche la variation N-1 sous une tuile : fleche + pourcentage en teinte semantique du
    /// theme (vert « accompli » quand l'evolution est favorable, rouge « refuse » sinon -
    /// jamais de couleur en dur). Une variation nulle cote serveur (reference N-1 a zero) est
    /// affichee « — », conformement a la regle de division par zero du calcul d'ecart budget.
    /// </summary>
    private void ApplyTrend(Path trendPath, TextBlock trendText, decimal? percent, bool increaseIsGood)
    {
        if (percent is null)
        {
            trendPath.Visibility = Visibility.Collapsed;
            trendText.Text = "— vs N-1";
            trendText.Foreground = (Brush)FindResource("TextMutedBrush");
            trendText.ToolTip = "Variation indisponible : la valeur de référence N-1 est nulle.";
            return;
        }

        var value = percent.Value;

        trendText.ToolTip = null;
        trendText.Text = string.Format(
            CultureInfo.CurrentCulture,
            "{0}{1:N1} % vs N-1",
            value > 0m ? "+" : string.Empty,
            value);

        if (value == 0m)
        {
            trendPath.Visibility = Visibility.Collapsed;
            trendText.Foreground = (Brush)FindResource("TextSecondaryBrush");
            return;
        }

        var isFavourable = value > 0m == increaseIsGood;
        var brush = (Brush)FindResource(isFavourable ? "StatusValidatedForegroundBrush" : "StatusRejectedForegroundBrush");

        trendPath.Data = (Geometry)FindResource(value > 0m ? "TrendUpGeometry" : "TrendDownGeometry");
        trendPath.Stroke = brush;
        trendPath.Visibility = Visibility.Visible;
        trendText.Foreground = brush;
    }

    private void ClearTrend(Path trendPath, TextBlock trendText)
    {
        trendPath.Visibility = Visibility.Collapsed;
        trendText.Text = string.Empty;
        trendText.ToolTip = null;
        trendText.Foreground = (Brush)FindResource("TextMutedBrush");
    }

    private static string FormatRate(decimal? ratePercent)
    {
        return ratePercent is null
            ? "—"
            : string.Format(CultureInfo.CurrentCulture, "{0:N1} %", ratePercent.Value);
    }

    private static UnitRowDisplay BuildUnitRow(GroupUnitRow row)
    {
        var culture = CultureInfo.CurrentCulture;
        var share = row.GroupSharePercent;

        return new UnitRowDisplay
        {
            HotelUnitCode = row.HotelUnitCode,
            HotelUnitName = row.IsActive ? row.HotelUnitName : row.HotelUnitName + " (désactivée)",
            ValidatedRevenue = row.ValidatedRevenue,
            ConfirmedReceipts = row.ConfirmedReceipts,
            GroupSharePercent = share ?? 0m,
            GroupShareText = share is null
                ? "—"
                : string.Format(culture, "{0:N1} %", share.Value),

            // Largeur de la barre de proportion : simple mise a l'echelle du pourcentage
            // renvoye par le serveur (70 px = 100 %) - aucune valeur metier recalculee ici.
            GroupShareBarWidth = share is null
                ? 0d
                : Math.Max(0d, Math.Min(70d, (double)share.Value * 0.7d)),

            OccupancySortValue = row.OccupancyRatePercent ?? -1m,
            OccupancyText = FormatRate(row.OccupancyRatePercent),
            OccupancyTooltip = row.AvailableNights == 0
                ? "Taux indisponible : cette unité n'a aucune chambre active (le taux d'une capacité nulle n'existe pas)."
                : string.Format(
                    culture,
                    "{0:N0} nuits occupées / {1:N0} disponibles",
                    row.OccupiedNights,
                    row.AvailableNights),

            UnclosedDayCount = row.UnclosedDayCount,
            BudgetVarianceSortValue = row.BudgetVarianceAmount ?? decimal.MinValue,
            BudgetVarianceText = BuildBudgetVarianceText(row, culture),
            BudgetTooltip = row.BudgetTarget is null
                ? "Aucun plan budgétaire approuvé ne couvre la période pour cette unité."
                : string.Format(
                    culture,
                    "Objectif budgété sur les mois couverts par la période : {0:N2}",
                    row.BudgetTarget.Value)
        };
    }

    private static string BuildBudgetVarianceText(GroupUnitRow row, CultureInfo culture)
    {
        if (row.BudgetTarget is null || row.BudgetVarianceAmount is null)
        {
            return "—";
        }

        var amount = row.BudgetVarianceAmount.Value;
        var amountText = string.Format(
            culture,
            "{0}{1:N2}",
            amount > 0m ? "+" : string.Empty,
            amount);

        // Le pourcentage d'ecart peut etre indefini (objectif a zero sur les mois couverts) :
        // il est alors omis, la valeur reste affichee - meme regle que le module Budget.
        return row.BudgetVariancePercent is null
            ? amountText
            : string.Format(
                culture,
                "{0} ({1}{2:N1} %)",
                amountText,
                row.BudgetVariancePercent.Value > 0m ? "+" : string.Empty,
                row.BudgetVariancePercent.Value);
    }

    private static AlertRowDisplay BuildAlertRow(GroupAlert alert)
    {
        return new AlertRowDisplay
        {
            Severity = alert.Severity.ToString(),
            SeverityLabel = GroupAlertDisplay.ToFrench(alert.Severity),
            TypeLabel = GroupAlertDisplay.ToFrench(alert.Type),

            // Lecture francaise de la regle (source unique GroupAlertDisplay), suivie du texte
            // EXACT renvoye par le serveur : miroir des regles, jamais leur remplacement.
            RuleTooltip = GroupAlertDisplay.RuleToFrench(alert.Type)
                + Environment.NewLine + Environment.NewLine
                + "Règle du serveur : " + alert.Rule,

            HotelUnitCode = alert.HotelUnitCode,
            Count = alert.Count
        };
    }

    /// <summary>
    /// Ligne d'affichage du tableau des unites : les montants restent des decimaux (tri et
    /// StringFormat N2 de la grille), les pourcentages sont pre-formates une seule fois avec
    /// la culture du poste, et chaque valeur speciale (« — ») garde son explication en
    /// info-bulle.
    /// </summary>
    private sealed class UnitRowDisplay
    {
        public string HotelUnitCode { get; init; } = string.Empty;

        public string HotelUnitName { get; init; } = string.Empty;

        public decimal ValidatedRevenue { get; init; }

        public decimal ConfirmedReceipts { get; init; }

        public decimal GroupSharePercent { get; init; }

        public string GroupShareText { get; init; } = string.Empty;

        public double GroupShareBarWidth { get; init; }

        public decimal OccupancySortValue { get; init; }

        public string OccupancyText { get; init; } = string.Empty;

        public string OccupancyTooltip { get; init; } = string.Empty;

        public int UnclosedDayCount { get; init; }

        public decimal BudgetVarianceSortValue { get; init; }

        public string BudgetVarianceText { get; init; } = string.Empty;

        public string BudgetTooltip { get; init; } = string.Empty;
    }

    private sealed class AlertRowDisplay
    {
        public string Severity { get; init; } = string.Empty;

        public string SeverityLabel { get; init; } = string.Empty;

        public string TypeLabel { get; init; } = string.Empty;

        public string RuleTooltip { get; init; } = string.Empty;

        public string HotelUnitCode { get; init; } = string.Empty;

        public int Count { get; init; }
    }
}
