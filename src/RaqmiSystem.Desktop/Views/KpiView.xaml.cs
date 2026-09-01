using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using RaqmiSystem.Application.Kpi;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Bibliotheque KPI (modules 24 / 25.4) : tableau de bord d'indicateurs, comparatif
/// inter-unites, alertes et parametrage.
///
/// Ecran pilote par le serveur : la vue ne recalcule RIEN - valeurs, ecarts, tendances,
/// verdicts et classements arrivent tout faits de /api/v1/kpis, et cette classe ne fait que
/// les mettre en forme. Les seules ecritures (seuils, rattachement de comptes, instantanes)
/// sont derriere kpi.admin ; le volet Parametrage est masque sans ce droit, par confort -
/// le refus fait autorite cote serveur.
///
/// Vue de module autonome : elle ne connait ni MainWindow ni les autres vues, tout passe par
/// le <see cref="ModuleViewContext"/> recu dans Initialize().
/// </summary>
public partial class KpiView : UserControl
{
    /// <summary>Valeur sentinelle du perimetre "groupe entier" dans les combos d'unite.</summary>
    private const string GroupScope = "";

    private ModuleViewContext? context;

    public KpiView()
    {
        InitializeComponent();

        Language = XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);

        InitializeDefaults();
    }

    public void Initialize(ModuleViewContext context)
    {
        this.context = context;

        // Confort d'interface uniquement : les routes de parametrage exigent kpi.admin cote
        // serveur, quoi que la vue affiche.
        AdminTabItem.Visibility = context.HasPermission(PermissionCatalog.KpiAdmin)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// (Re)charge l'ecran entier sur la periode et le perimetre saisis. Sort silencieusement
    /// tant qu'aucun contexte n'est fourni ou que l'utilisateur n'est pas connecte.
    /// </summary>
    public async Task LoadAsync()
    {
        var current = context;

        if (current is null || !current.ApiClient.IsAuthenticated)
        {
            return;
        }

        await current.RunAsync(async () => await LoadAllAsync(current));
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    private void InitializeDefaults()
    {
        var today = DateTime.Today;

        FromDatePicker.SelectedDate = new DateTime(today.Year, today.Month, 1);
        ToDatePicker.SelectedDate = today;
    }

    private async Task LoadAllAsync(ModuleViewContext current)
    {
        if (!TryReadPeriod(current, out var from, out var to))
        {
            return;
        }

        var unitCode = UnitComboBox.SelectedValue as string;
        var scopedUnit = string.IsNullOrEmpty(unitCode) ? null : unitCode;

        var dashboard = await current.ApiClient.GetKpiDashboardAsync(
            current.ApiBaseUrl, from, to, scopedUnit);

        PopulateHeadline(dashboard);
        PopulateSections(dashboard);
        PopulateAlerts(dashboard);
        PopulateUnitCombos(dashboard, scopedUnit);

        // Le comparatif est une lecture GROUPE par nature : il reste charge sur le groupe
        // entier meme quand le tableau de bord est restreint a une unite.
        var comparison = await current.ApiClient.GetKpiComparisonAsync(current.ApiBaseUrl, from, to);
        PopulateComparison(comparison);

        if (AdminTabItem.Visibility == Visibility.Visible)
        {
            await LoadAdministrationAsync(current);
        }

        var culture = CultureInfo.CurrentCulture;

        PeriodSummaryTextBlock.Text = string.Format(
            culture,
            "Du {0:d} au {1:d} — comparé à la période équivalente du {2:d} au {3:d}. Calculé le {4:g}.",
            dashboard.From.ToDateTime(TimeOnly.MinValue),
            dashboard.To.ToDateTime(TimeOnly.MinValue),
            dashboard.PreviousFrom.ToDateTime(TimeOnly.MinValue),
            dashboard.PreviousTo.ToDateTime(TimeOnly.MinValue),
            dashboard.CalculatedAt.ToLocalTime());

        current.SetStatus("Bibliothèque KPI actualisée.");
    }

    private bool TryReadPeriod(ModuleViewContext current, out DateOnly from, out DateOnly to)
    {
        from = default;
        to = default;

        if (FromDatePicker.SelectedDate is not { } fromDate || ToDatePicker.SelectedDate is not { } toDate)
        {
            current.SetStatus("Sélectionnez une période complète (dates « du » et « au »).", isError: true);
            return false;
        }

        if (toDate < fromDate)
        {
            current.SetStatus("La date « du » doit précéder la date « au ».", isError: true);
            return false;
        }

        from = DateOnly.FromDateTime(fromDate);
        to = DateOnly.FromDateTime(toDate);
        return true;
    }

    // ------------------------------------------------------------------ Tuiles de tete

    private void PopulateHeadline(KpiDashboardResponse dashboard)
    {
        var culture = CultureInfo.CurrentCulture;

        HeadlineItemsControl.ItemsSource = dashboard.Headline
            .Select(measure => new HeadlineCard(
                measure.ShortName,
                FormatValue(measure.Value, measure.Unit, culture),
                BuildTrendText(measure, culture),
                ResolveTrendBrush(measure),
                BuildHealthLabel(measure.Health),
                ResolveHealthBrush(measure.Health),
                BuildMeasureTooltip(measure)))
            .ToArray();
    }

    // ------------------------------------------------------------------ Bibliotheque

    private void PopulateSections(KpiDashboardResponse dashboard)
    {
        var culture = CultureInfo.CurrentCulture;

        SectionsItemsControl.ItemsSource = dashboard.Sections
            .Select(section => new SectionItem(
                section.Label,
                section.Measures
                    .Select(measure => new MeasureRow(
                        measure.Name,
                        FormatValue(measure.Value, measure.Unit, culture),
                        FormatValue(measure.PreviousValue, measure.Unit, culture),
                        FormatSignedPercent(measure.PreviousVariancePercent, culture),
                        FormatValue(measure.BudgetValue, measure.Unit, culture),
                        FormatValue(measure.TargetValue, measure.Unit, culture),
                        BuildHealthLabel(measure.Health),
                        ResolveHealthBrush(measure.Health),
                        BuildQualityLabel(measure.Quality),
                        string.Join(Environment.NewLine, measure.MissingData),
                        BuildMeasureTooltip(measure)))
                    .ToArray()))
            .ToArray();

        if (dashboard.HiddenByPermission > 0)
        {
            HiddenByPermissionTextBlock.Text = string.Format(
                culture,
                "{0} indicateur(s) ne sont pas affichés : votre profil ne détient pas les permissions des modules dont ils lisent les données.",
                dashboard.HiddenByPermission);
            HiddenByPermissionTextBlock.Visibility = Visibility.Visible;
        }
        else
        {
            HiddenByPermissionTextBlock.Visibility = Visibility.Collapsed;
        }
    }

    // ------------------------------------------------------------------ Comparatif

    private void PopulateComparison(KpiComparisonResponse comparison)
    {
        var culture = CultureInfo.CurrentCulture;

        // Colonnes construites dynamiquement : la premiere porte l'unite, puis une colonne par
        // indicateur du comparatif, dans l'ordre renvoye par le serveur. Les valeurs sont
        // pre-formatees dans un tableau et liees par index - la grille n'a aucune idee de ce
        // qu'est un KPI, et c'est voulu.
        ComparisonDataGrid.Columns.Clear();
        ComparisonDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Unité",
            Binding = new System.Windows.Data.Binding(nameof(ComparisonRow.UnitName)),
            Width = new DataGridLength(1.6, DataGridLengthUnitType.Star)
        });

        var referenceRow = comparison.Rows.FirstOrDefault();

        var headers = referenceRow is null
            ? []
            : comparison.Codes
                .Select(code => referenceRow.Measures.FirstOrDefault(m => m.Code == code)?.ShortName ?? code)
                .ToArray();

        for (var index = 0; index < headers.Length; index++)
        {
            ComparisonDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = headers[index],
                Binding = new System.Windows.Data.Binding($"{nameof(ComparisonRow.Values)}[{index}]"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
        }

        ComparisonDataGrid.ItemsSource = comparison.Rows
            .Select(row => new ComparisonRow(
                row.HotelUnitCode is null ? "Groupe" : row.HotelUnitName,
                comparison.Codes
                    .Select(code =>
                    {
                        var measure = row.Measures.FirstOrDefault(m => m.Code == code);
                        return measure is null ? "—" : FormatValue(measure.Value, measure.Unit, culture);
                    })
                    .ToArray()))
            .ToArray();

        RankingsDataGrid.ItemsSource = comparison.Rankings
            .Select(ranking => new RankingRow(
                BuildRankingLabel(ranking.Kind),
                ranking.KpiName,
                ranking.HotelUnitName,
                ranking.Value is null
                    ? "—"
                    : FormatValue(ranking.Value, KpiCatalog.Find(ranking.KpiCode)?.Unit ?? KpiUnit.Ratio, culture),
                ranking.Explanation))
            .ToArray();
    }

    // ------------------------------------------------------------------ Alertes

    private void PopulateAlerts(KpiDashboardResponse dashboard)
    {
        var culture = CultureInfo.CurrentCulture;

        AlertsDataGrid.ItemsSource = dashboard.Alerts
            .Select(alert => new AlertRow(
                alert.Severity == KpiAlertSeverity.Critical ? "Critique" : "Vigilance",
                ResolveHealthBrush(alert.Severity == KpiAlertSeverity.Critical
                    ? KpiHealth.Critical
                    : KpiHealth.Watch),
                alert.KpiName,
                alert.HotelUnitName ?? alert.HotelUnitCode ?? "Groupe",
                FormatValue(alert.Value, alert.Unit, culture),
                FormatValue(alert.BreachedThreshold, alert.Unit, culture),
                alert.OwnerRole ?? "—",
                alert.Message))
            .ToArray();

        NoAlertTextBlock.Visibility = dashboard.Alerts.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    // ------------------------------------------------------------------ Parametrage

    private async Task LoadAdministrationAsync(ModuleViewContext current)
    {
        var culture = CultureInfo.CurrentCulture;

        var thresholds = await current.ApiClient.GetKpiThresholdsAsync(current.ApiBaseUrl);

        ThresholdsDataGrid.ItemsSource = thresholds
            .Select(threshold => new ThresholdRow(
                threshold.KpiName,
                threshold.HotelUnitCode ?? "Groupe",
                FormatValue(threshold.FavorableThreshold, threshold.Unit, culture),
                FormatValue(threshold.CriticalThreshold, threshold.Unit, culture),
                FormatValue(threshold.TargetValue, threshold.Unit, culture),
                threshold.OwnerRole ?? "—",
                threshold.IsActive ? "Oui" : "Non"))
            .ToArray();

        var mappings = await current.ApiClient.GetKpiAccountMappingsAsync(current.ApiBaseUrl);

        MappingsDataGrid.ItemsSource = mappings
            .Select(mapping => new MappingRow(
                mapping.AccountPrefix,
                BuildAccountGroupLabel(mapping.Group),
                mapping.Label,
                mapping.IsActive ? "Oui" : "Non"))
            .ToArray();

        if (ThresholdKpiComboBox.Items.Count == 0)
        {
            ThresholdKpiComboBox.ItemsSource = KpiCatalog.All
                .Select(definition => new ChoiceItem(definition.Name, definition.Code))
                .ToArray();
            ThresholdKpiComboBox.DisplayMemberPath = nameof(ChoiceItem.Label);
            ThresholdKpiComboBox.SelectedValuePath = nameof(ChoiceItem.Value);
        }

        if (MappingGroupComboBox.Items.Count == 0)
        {
            MappingGroupComboBox.ItemsSource = Enum.GetValues<KpiAccountGroup>()
                .Select(group => new ChoiceItem(BuildAccountGroupLabel(group), group.ToString()))
                .ToArray();
            MappingGroupComboBox.DisplayMemberPath = nameof(ChoiceItem.Label);
            MappingGroupComboBox.SelectedValuePath = nameof(ChoiceItem.Value);
        }
    }

    private async void SaveThresholdButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        if (ThresholdKpiComboBox.SelectedValue is not string kpiCode || string.IsNullOrWhiteSpace(kpiCode))
        {
            current.SetStatus("Sélectionnez l'indicateur à paramétrer.", isError: true);
            return;
        }

        if (!TryParseOptionalDecimal(ThresholdFavorableTextBox.Text, out var favorable)
            || !TryParseOptionalDecimal(ThresholdCriticalTextBox.Text, out var critical)
            || !TryParseOptionalDecimal(ThresholdTargetTextBox.Text, out var target))
        {
            current.SetStatus("Les bornes et l'objectif doivent être des nombres.", isError: true);
            return;
        }

        var unitCode = ThresholdUnitComboBox.SelectedValue as string;

        await current.RunAsync(async () =>
        {
            await current.ApiClient.SaveKpiThresholdAsync(
                current.ApiBaseUrl,
                new SaveKpiThresholdRequest(
                    kpiCode,
                    string.IsNullOrEmpty(unitCode) ? null : unitCode,
                    favorable,
                    critical,
                    target,
                    string.IsNullOrWhiteSpace(ThresholdOwnerTextBox.Text) ? null : ThresholdOwnerTextBox.Text.Trim(),
                    null));

            await LoadAdministrationAsync(current);
            current.SetStatus("Règle de seuils enregistrée.");
        });
    }

    private async void SaveMappingButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        if (MappingGroupComboBox.SelectedValue is not string groupValue
            || !Enum.TryParse<KpiAccountGroup>(groupValue, out var group))
        {
            current.SetStatus("Sélectionnez le groupe de gestion.", isError: true);
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.SaveKpiAccountMappingAsync(
                current.ApiBaseUrl,
                new SaveKpiAccountMappingRequest(
                    MappingPrefixTextBox.Text,
                    group,
                    MappingLabelTextBox.Text));

            await LoadAdministrationAsync(current);
            current.SetStatus("Rattachement de comptes enregistré.");
        });
    }

    private async void CaptureSnapshotsButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || !TryReadPeriod(current, out var from, out var to))
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            var result = await current.ApiClient.CaptureKpiSnapshotsAsync(
                current.ApiBaseUrl,
                new CaptureKpiSnapshotsRequest(from, to));

            current.SetStatus(string.Format(
                CultureInfo.CurrentCulture,
                "Instantanés posés : {0} créé(s), {1} rafraîchi(s), {2} clôturé(s) intouché(s){3}",
                result.Created,
                result.Refreshed,
                result.SkippedBecauseClosed,
                result.Divergences.Count > 0
                    ? $" — {result.Divergences.Count} divergence(s) signalée(s)."
                    : "."));
        });
    }

    private async void CloseSnapshotsButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || !TryReadPeriod(current, out var from, out var to))
        {
            return;
        }

        // Confirmation explicite : la cloture est IRREVERSIBLE par construction - un chiffre
        // fige ne sera plus jamais reecrit par un recalcul.
        var confirmation = MessageBox.Show(
            "Clôturer fige définitivement les valeurs de la période : aucun recalcul ne les réécrira, "
            + "et une divergence ultérieure sera signalée sans être corrigée.\n\nClôturer les instantanés ?",
            "Clôture des instantanés KPI",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            var result = await current.ApiClient.CloseKpiSnapshotsAsync(
                current.ApiBaseUrl,
                new CloseKpiSnapshotsRequest(from, to));

            current.SetStatus(string.Format(
                CultureInfo.CurrentCulture,
                "Instantanés clôturés : {0} figé(s), {1} déjà clôturé(s).",
                result.Closed,
                result.SkippedBecauseClosed));
        });
    }

    // ------------------------------------------------------------------ Combos

    private void PopulateUnitCombos(KpiDashboardResponse dashboard, string? selectedUnit)
    {
        // La liste des perimetres vient du tableau de bord lui-meme (les unites du groupe) ;
        // elle n'est reconstruite que lorsqu'elle est vide ou que le serveur renvoie d'autres
        // unites, pour ne pas faire sauter la selection de l'utilisateur a chaque actualisation.
        var choices = new List<ChoiceItem> { new("Groupe entier", GroupScope) };

        choices.AddRange(dashboard.Units
            .Select(unit => new ChoiceItem(unit.HotelUnitName, unit.HotelUnitCode)));

        if (UnitComboBox.Items.Count != choices.Count && dashboard.HotelUnitCode is null)
        {
            UnitComboBox.ItemsSource = choices;
            UnitComboBox.DisplayMemberPath = nameof(ChoiceItem.Label);
            UnitComboBox.SelectedValuePath = nameof(ChoiceItem.Value);
            UnitComboBox.SelectedValue = selectedUnit ?? GroupScope;

            if (ThresholdUnitComboBox.Items.Count == 0)
            {
                ThresholdUnitComboBox.ItemsSource = choices.ToArray();
                ThresholdUnitComboBox.DisplayMemberPath = nameof(ChoiceItem.Label);
                ThresholdUnitComboBox.SelectedValuePath = nameof(ChoiceItem.Value);
                ThresholdUnitComboBox.SelectedValue = GroupScope;
            }
        }
    }

    // ------------------------------------------------------------------ Mise en forme

    /// <summary>
    /// Le formatage suit l'unite declaree par le catalogue - et une valeur absente est TOUJOURS
    /// un tiret, jamais un zero : zero signifie "mesuré et nul", le tiret "la question ne se
    /// pose pas ou il manque une donnée", et les confondre est exactement ce que le moteur
    /// s'interdit.
    /// </summary>
    private static string FormatValue(decimal? value, KpiUnit unit, CultureInfo culture)
    {
        if (value is null)
        {
            return "—";
        }

        return unit switch
        {
            KpiUnit.Currency => value.Value.ToString("N2", culture),
            KpiUnit.Percentage => string.Format(culture, "{0:N1} %", value.Value),
            KpiUnit.Count or KpiUnit.Nights => value.Value.ToString("N0", culture),
            KpiUnit.Days or KpiUnit.Hours or KpiUnit.Score => value.Value.ToString("N1", culture),
            _ => value.Value.ToString("N2", culture)
        };
    }

    private static string FormatSignedPercent(decimal? value, CultureInfo culture)
    {
        return value is null
            ? "—"
            : string.Format(culture, "{0}{1:N1} %", value.Value > 0 ? "+" : string.Empty, value.Value);
    }

    private static string BuildTrendText(KpiMeasureResponse measure, CultureInfo culture)
    {
        var glyph = measure.Trend switch
        {
            KpiTrend.Up => "▲",
            KpiTrend.Down => "▼",
            KpiTrend.Flat => "▬",
            _ => string.Empty
        };

        return measure.PreviousVariancePercent is null
            ? glyph
            : $"{glyph} {FormatSignedPercent(measure.PreviousVariancePercent, culture)} vs N-1";
    }

    private static string BuildHealthLabel(KpiHealth health)
    {
        return health switch
        {
            KpiHealth.Favorable => "Favorable",
            KpiHealth.Watch => "Vigilance",
            KpiHealth.Critical => "Critique",
            _ => string.Empty
        };
    }

    private static string BuildQualityLabel(KpiQuality quality)
    {
        return quality switch
        {
            KpiQuality.Valid => "Complète",
            KpiQuality.Partial => "Partielle",
            KpiQuality.MissingData => "Donnée manquante",
            _ => "Sans objet"
        };
    }

    private static string BuildRankingLabel(KpiRankingKind kind)
    {
        return kind switch
        {
            KpiRankingKind.BestPerformance => "Meilleure performance",
            KpiRankingKind.StrongestProgress => "Plus forte progression",
            KpiRankingKind.LargestBudgetGap => "Plus fort écart budget",
            _ => "Indicateur le plus faible"
        };
    }

    private static string BuildAccountGroupLabel(KpiAccountGroup group)
    {
        return group switch
        {
            KpiAccountGroup.Revenue => "Produits d'exploitation",
            KpiAccountGroup.DepartmentalExpense => "Charges départementales",
            KpiAccountGroup.UndistributedExpense => "Charges non réparties",
            KpiAccountGroup.FixedCharge => "Charges fixes de propriété",
            KpiAccountGroup.DepreciationAndProvision => "Dotations et provisions",
            KpiAccountGroup.FinancialResult => "Résultat financier",
            _ => "Impôts sur le résultat"
        };
    }

    /// <summary>
    /// L'infobulle qui rend un chiffre discutable : la formule, ce qui est compte exactement,
    /// et - quand la valeur manque - la raison, telle que le serveur l'a donnee.
    /// </summary>
    private static string BuildMeasureTooltip(KpiMeasureResponse measure)
    {
        var parts = new List<string>
        {
            measure.Name,
            "Formule : " + measure.Formula,
            "Compté : " + measure.SourceDetail
        };

        if (measure.MissingData.Count > 0)
        {
            parts.Add(string.Join(Environment.NewLine, measure.MissingData));
        }

        return string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

    /// <summary>
    /// La couleur d'une tendance depend du SENS DE LECTURE de l'indicateur : un food cost qui
    /// monte est une fleche rouge, une occupation qui monte une fleche verte. La combinaison
    /// est faite ici, sur la polarite renvoyee par le serveur - jamais devinee du nom.
    /// </summary>
    private Brush ResolveTrendBrush(KpiMeasureResponse measure)
    {
        if (measure.Trend is KpiTrend.Unknown or KpiTrend.Flat
            || measure.Polarity == KpiPolarity.Neutral)
        {
            return FindBrush("TextMutedBrush", Brushes.Gray);
        }

        var improving = (measure.Trend == KpiTrend.Up) == (measure.Polarity == KpiPolarity.HigherIsBetter);

        return improving
            ? FindBrush("StatusValidatedForegroundBrush", Brushes.Green)
            : FindBrush("StatusRejectedForegroundBrush", Brushes.Firebrick);
    }

    private Brush ResolveHealthBrush(KpiHealth health)
    {
        return health switch
        {
            KpiHealth.Favorable => FindBrush("StatusValidatedForegroundBrush", Brushes.Green),
            KpiHealth.Watch => FindBrush("StatusSubmittedForegroundBrush", Brushes.DarkOrange),
            KpiHealth.Critical => FindBrush("StatusRejectedForegroundBrush", Brushes.Firebrick),
            _ => FindBrush("TextMutedBrush", Brushes.Gray)
        };
    }

    private Brush FindBrush(string resourceKey, Brush fallback)
    {
        return TryFindResource(resourceKey) as Brush ?? fallback;
    }

    private static bool TryParseOptionalDecimal(string? text, out decimal? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed)
            || decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    // -------------------------------------------------------------- Lignes d'affichage

    private sealed record HeadlineCard(
        string Label,
        string ValueText,
        string TrendText,
        Brush TrendBrush,
        string HealthText,
        Brush HealthBrush,
        string Tooltip);

    private sealed record SectionItem(string Label, IReadOnlyList<MeasureRow> Rows);

    private sealed record MeasureRow(
        string Name,
        string ValueText,
        string PreviousText,
        string VariationText,
        string BudgetText,
        string TargetText,
        string HealthText,
        Brush HealthBrush,
        string QualityText,
        string QualityTooltip,
        string Tooltip);

    private sealed record ComparisonRow(string UnitName, IReadOnlyList<string> Values);

    private sealed record RankingRow(
        string KindLabel,
        string KpiName,
        string UnitName,
        string ValueText,
        string Explanation);

    private sealed record AlertRow(
        string SeverityLabel,
        Brush SeverityBrush,
        string KpiName,
        string ScopeLabel,
        string ValueText,
        string ThresholdText,
        string OwnerRole,
        string Message);

    private sealed record ThresholdRow(
        string KpiName,
        string ScopeLabel,
        string FavorableText,
        string CriticalText,
        string TargetText,
        string OwnerRole,
        string ActiveLabel);

    private sealed record MappingRow(
        string AccountPrefix,
        string GroupLabel,
        string Label,
        string ActiveLabel);

    private sealed record ChoiceItem(string Label, string Value);
}
