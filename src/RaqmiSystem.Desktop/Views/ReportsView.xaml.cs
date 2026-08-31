using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using Microsoft.Win32;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Application.Reporting;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module Rapports automatiques : catalogue de rapports defini en code cote
/// serveur, execution parametree rendue par une grille dynamique unique
/// (colonnes construites depuis la reponse), export CSV local et journal des
/// executions. Vue autonome : elle ne connait que le ModuleViewContext que la
/// fenetre lui prete, jamais MainWindow ni une autre vue.
///
/// Les exports PDF et Excel sont hors perimetre (aucune bibliotheque dans le
/// depot) : l'ecran ne promet que ce que le serveur sait produire.
/// </summary>
public partial class ReportsView : UserControl
{
    // NOTE INTEGRATEUR : remplacer par PermissionCatalog.ReportsRead une fois la
    // constante ajoutee au catalogue des permissions.
    private const string ReadPermission = "reports.read";

    private const string ReadPermissionHint = "Permission reports.read requise : votre profil ne peut pas consulter les rapports.";

    // Types de colonne renvoyes par le serveur (contrat ReportColumnResponse).
    private const string MoneyColumnType = "money";
    private const string NumberColumnType = "number";
    private const string DateColumnType = "date";

    // Types de parametre renvoyes par le serveur (contrat ReportParameterResponse).
    private const string DateParameterType = "date";
    private const string UnitParameterType = "unit";

    private ModuleViewContext? context;

    private bool canReadReports = true;

    // Info-bulles d'origine des boutons, capturees avant toute substitution par le
    // message de permission : l'affectation doit rester symetrique (les vues
    // survivent a la deconnexion et resservent au profil suivant).
    private readonly Dictionary<Button, object?> originalToolTips = [];

    // Controles de saisie des parametres du rapport selectionne, par cle de
    // parametre. Reconstruits a chaque changement de selection.
    private readonly Dictionary<string, FrameworkElement> parameterInputs = [];

    private IReadOnlyCollection<HotelUnitResponse> hotelUnits = [];

    // Dernier resultat BRUT renvoye par le serveur : c'est lui (valeurs machine
    // invariantes) qui alimente l'export CSV, jamais les cellules formatees pour
    // l'affichage.
    private ReportResultResponse? lastResult;

    public ReportsView()
    {
        InitializeComponent();

        // Les StringFormat XAML doivent suivre la meme culture que le code.
        Language = XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);

        UpdateActionButtons();
    }

    /// <summary>Memorise le contexte fourni par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext context)
    {
        this.context = context;
        canReadReports = context.HasPermission(ReadPermission);

        UpdateActionButtons();
    }

    /// <summary>
    /// (Re)charge le catalogue, les unites et le journal. Sort silencieusement tant
    /// qu'aucun contexte n'est disponible ou qu'aucune session n'est ouverte.
    /// </summary>
    public async Task LoadAsync()
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await ReloadCatalogAsync();
            await ReloadExecutionsAsync();
        });
    }

    /// <summary>Vide catalogue, parametres, resultat et journal (appelee a la deconnexion).</summary>
    public void ResetState()
    {
        ReportsListBox.ItemsSource = null;
        ParametersPanel.Children.Clear();
        parameterInputs.Clear();
        NoReportSelectedTextBlock.Visibility = Visibility.Visible;
        hotelUnits = [];
        lastResult = null;
        ResultDataGrid.Columns.Clear();
        ResultDataGrid.ItemsSource = null;
        ResultTitleTextBlock.Text = "Résultat";
        ResultInfoTextBlock.Text = string.Empty;
        ExecutionsDataGrid.ItemsSource = null;
        ExecutionCountTextBlock.Text = string.Empty;
        UpdateActionButtons();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await ReloadCatalogAsync();
            await ReloadExecutionsAsync();
            moduleContext.SetStatus("Catalogue et journal des exécutions actualisés.");
        });
    }

    private async Task ReloadCatalogAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        // Le code du rapport est la cle stable d'une ligne a l'autre : la
        // selection est restauree apres rechargement.
        var selectedCode = (ReportsListBox.SelectedItem as ReportDefinitionResponse)?.Code;

        var catalog = await moduleContext.ApiClient.GetReportCatalogAsync(moduleContext.ApiBaseUrl);
        hotelUnits = await moduleContext.ApiClient.GetHotelUnitsAsync(moduleContext.ApiBaseUrl, includeInactive: false);

        ReportsListBox.ItemsSource = catalog;

        if (selectedCode is not null)
        {
            var restored = catalog.FirstOrDefault(report =>
                string.Equals(report.Code, selectedCode, StringComparison.OrdinalIgnoreCase));

            if (restored is not null)
            {
                ReportsListBox.SelectedItem = restored;
                ReportsListBox.ScrollIntoView(restored);
            }
        }

        UpdateActionButtons();
    }

    private async Task ReloadExecutionsAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var executions = await moduleContext.ApiClient.GetReportExecutionsAsync(moduleContext.ApiBaseUrl);

        ExecutionsDataGrid.ItemsSource = executions.Select(ToExecutionRow).ToArray();

        ExecutionCountTextBlock.Text = executions.Count == 1
            ? "1 exécution"
            : $"{executions.Count.ToString(CultureInfo.CurrentCulture)} exécutions";
    }

    // ------------------------------------------------------------- parametres dynamiques

    private void ReportsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        BuildParameterInputs(ReportsListBox.SelectedItem as ReportDefinitionResponse);
        UpdateActionButtons();
    }

    /// <summary>
    /// Construit les champs de saisie depuis la definition du rapport renvoyee par
    /// le serveur : selecteur de date pour un parametre "date", liste des unites
    /// pour un parametre "unit". Aucun parametre n'est recopie en dur ici.
    /// </summary>
    private void BuildParameterInputs(ReportDefinitionResponse? report)
    {
        ParametersPanel.Children.Clear();
        parameterInputs.Clear();

        if (report is null)
        {
            NoReportSelectedTextBlock.Visibility = Visibility.Visible;
            return;
        }

        NoReportSelectedTextBlock.Visibility = Visibility.Collapsed;

        var today = DateTime.Today;

        foreach (var parameter in report.Parameters)
        {
            var field = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            field.Children.Add(new TextBlock
            {
                Text = parameter.Label,
                Style = (Style)FindResource("LabelText")
            });

            FrameworkElement input;

            if (string.Equals(parameter.Type, UnitParameterType, StringComparison.OrdinalIgnoreCase))
            {
                var comboBox = new ComboBox { DisplayMemberPath = "Label" };
                var options = new List<UnitOption>();

                // Un parametre d'unite facultatif propose "toutes les unites" ;
                // un parametre requis force un choix explicite.
                if (!parameter.Required)
                {
                    options.Add(new UnitOption(null, "(Toutes les unités)"));
                }

                options.AddRange(hotelUnits.Select(unit => new UnitOption(unit.Code, $"{unit.Code} — {unit.Name}")));

                comboBox.ItemsSource = options;
                comboBox.SelectedIndex = options.Count > 0 ? 0 : -1;
                input = comboBox;
            }
            else
            {
                // Bornes par defaut raisonnees en heure LOCALE du poste : le mois
                // en cours pour un debut de periode, aujourd'hui pour le reste.
                var defaultDate = string.Equals(parameter.Key, "from", StringComparison.OrdinalIgnoreCase)
                    ? new DateTime(today.Year, today.Month, 1)
                    : today;

                input = new DatePicker { SelectedDate = defaultDate };
            }

            field.Children.Add(input);
            ParametersPanel.Children.Add(field);
            parameterInputs[parameter.Key] = input;
        }
    }

    // ------------------------------------------------------------------- execution

    private async void RunReportButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        if (ReportsListBox.SelectedItem is not ReportDefinitionResponse report)
        {
            moduleContext.SetStatus("Sélectionnez un rapport dans le catalogue.", isError: true);
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var parameters = new Dictionary<string, string?>();

            // Verification miroir de la regle serveur (parametre requis manquant) :
            // un message clair ici evite un aller-retour previsible, le serveur
            // restant seul juge de la validite.
            foreach (var parameter in report.Parameters)
            {
                var value = ReadParameterValue(parameter.Key);

                if (value is null)
                {
                    if (parameter.Required)
                    {
                        moduleContext.SetStatus($"Le paramètre « {parameter.Label} » est requis.", isError: true);
                        return;
                    }

                    continue;
                }

                parameters[parameter.Key] = value;
            }

            var result = await moduleContext.ApiClient.RunReportAsync(
                moduleContext.ApiBaseUrl,
                new RunReportRequest(report.Code, parameters));

            DisplayResult(result);
            await ReloadExecutionsAsync();

            moduleContext.SetStatus(result.RowCount == 1
                ? $"Rapport « {result.Title} » exécuté : 1 ligne."
                : $"Rapport « {result.Title} » exécuté : {result.RowCount.ToString(CultureInfo.CurrentCulture)} lignes.");
        });
    }

    private string? ReadParameterValue(string parameterKey)
    {
        if (!parameterInputs.TryGetValue(parameterKey, out var input))
        {
            return null;
        }

        return input switch
        {
            DatePicker datePicker => datePicker.SelectedDate is { } date
                ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : null,
            ComboBox comboBox => (comboBox.SelectedItem as UnitOption)?.Code,
            _ => null
        };
    }

    /// <summary>
    /// Rend le resultat structurel dans la grille : les colonnes sont construites
    /// depuis la reponse (montants N2 alignes a droite, dates en dd/MM/yyyy), la
    /// ligne de total renvoyee par le serveur est ajoutee en derniere position et
    /// mise en evidence par le style de ligne. Les totaux viennent du serveur,
    /// jamais d'un recalcul local.
    /// </summary>
    private void DisplayResult(ReportResultResponse result)
    {
        lastResult = result;

        ResultDataGrid.Columns.Clear();

        for (var index = 0; index < result.Columns.Count; index++)
        {
            var column = result.Columns[index];
            var isAmountLike = IsAmountLike(column.Type);

            var gridColumn = new DataGridTextColumn
            {
                Header = column.Label,
                Binding = new Binding($"Cells[{index.ToString(CultureInfo.InvariantCulture)}]"),
                Width = index == 0
                    ? new DataGridLength(1, DataGridLengthUnitType.Star)
                    : DataGridLength.Auto,
                MinWidth = 90
            };

            if (isAmountLike)
            {
                gridColumn.ElementStyle = (Style)FindResource("AmountCellText");
                gridColumn.HeaderStyle = (Style)FindResource("RightAlignedColumnHeader");
            }

            ResultDataGrid.Columns.Add(gridColumn);
        }

        var rows = result.Rows
            .Select(row => new ResultRowView(FormatRowForDisplay(row, result.Columns), IsTotal: false))
            .ToList();

        if (result.TotalRow is not null)
        {
            rows.Add(new ResultRowView(FormatRowForDisplay(result.TotalRow, result.Columns), IsTotal: true));
        }

        ResultDataGrid.ItemsSource = rows;

        ResultTitleTextBlock.Text = result.Title;
        ResultInfoTextBlock.Text = result.RowCount == 1
            ? $"1 ligne — générée le {result.GeneratedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture)}"
            : $"{result.RowCount.ToString(CultureInfo.CurrentCulture)} lignes — générées le {result.GeneratedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture)}";

        UpdateActionButtons();
    }

    private static bool IsAmountLike(string columnType)
    {
        return string.Equals(columnType, MoneyColumnType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(columnType, NumberColumnType, StringComparison.OrdinalIgnoreCase);
    }

    // Les valeurs brutes du serveur sont invariantes (dates yyyy-MM-dd, decimaux a
    // point) ; l'affichage suit la culture courante : montants en N2, dates en
    // dd/MM/yyyy. Une cellule non numerique d'une colonne numerique (le libelle
    // "Total" de la ligne de total) est rendue telle quelle.
    private static IReadOnlyList<string> FormatRowForDisplay(
        IReadOnlyList<string?> rawCells,
        IReadOnlyList<ReportColumnResponse> columns)
    {
        var cells = new string[columns.Count];

        for (var index = 0; index < columns.Count; index++)
        {
            var raw = index < rawCells.Count ? rawCells[index] : null;
            cells[index] = FormatCellForDisplay(raw, columns[index].Type);
        }

        return cells;
    }

    private static string FormatCellForDisplay(string? raw, string columnType)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        if (string.Equals(columnType, MoneyColumnType, StringComparison.OrdinalIgnoreCase) &&
            decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return amount.ToString("N2", CultureInfo.CurrentCulture);
        }

        if (string.Equals(columnType, NumberColumnType, StringComparison.OrdinalIgnoreCase) &&
            decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            return number.ToString("#,0.##", CultureInfo.CurrentCulture);
        }

        if (string.Equals(columnType, DateColumnType, StringComparison.OrdinalIgnoreCase) &&
            DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);
        }

        return raw;
    }

    // --------------------------------------------------------------------- export CSV

    private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;
        var result = lastResult;

        if (moduleContext is null)
        {
            return;
        }

        if (result is null || result.Rows.Count == 0)
        {
            moduleContext.SetStatus("Aucun résultat à exporter : exécutez d'abord un rapport.", isError: true);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Fichiers CSV (*.csv)|*.csv",
            FileName = $"{result.ReportCode}-{DateTime.Today:yyyy-MM-dd}.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            // L'export part du resultat BRUT (valeurs machine invariantes), pas
            // des cellules formatees pour l'ecran : meme convention que les autres
            // exports du produit (CsvExportHelper, UTF-8 avec BOM, anti-injection).
            var csv = CsvExportHelper.BuildReportCsv(result);
            CsvExportHelper.WriteCsvFile(dialog.FileName, csv);
            moduleContext.SetStatus("Export CSV terminé.");
        }
        catch (Exception ex)
        {
            moduleContext.SetStatus($"Échec de l'export CSV : {ex.Message}", isError: true);
        }
    }

    // ------------------------------------------------------------------------ etat UI

    private void UpdateActionButtons()
    {
        var hasReportSelected = ReportsListBox.SelectedItem is ReportDefinitionResponse;
        var hasResult = lastResult is not null && lastResult.Rows.Count > 0;

        RunReportButton.IsEnabled = canReadReports && hasReportSelected;
        ExportCsvButton.IsEnabled = canReadReports && hasResult;

        ApplyPermissionHint(RunReportButton, canReadReports, ReadPermissionHint);
        ApplyPermissionHint(ExportCsvButton, canReadReports, ReadPermissionHint);
    }

    // Pose le message d'explication quand le droit manque, et RESTAURE l'info-bulle
    // d'origine du bouton quand il est present : l'affectation doit etre symetrique,
    // sinon un message pose pour un profil restreint survit a la reconnexion d'un
    // profil qui, lui, a le droit (les vues survivent a la deconnexion).
    private void ApplyPermissionHint(Button button, bool allowed, string hint)
    {
        if (!originalToolTips.ContainsKey(button))
        {
            originalToolTips[button] = button.ToolTip;
        }

        button.ToolTip = allowed ? originalToolTips[button] : hint;
    }

    // ------------------------------------------------------------------- projections

    private static ExecutionRowView ToExecutionRow(ReportExecutionResponse execution)
    {
        return new ExecutionRowView(
            execution.ExecutedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture),
            execution.ReportTitle ?? execution.ReportCode,
            execution.ExecutedBy,
            DescribeParameters(execution.ParametersJson),
            execution.RowCount.ToString("#,0", CultureInfo.CurrentCulture),
            execution.DurationMilliseconds.ToString("#,0", CultureInfo.CurrentCulture));
    }

    // Le journal stocke les parametres normalises en JSON ; l'ecran les rend plus
    // lisibles ("from=2026-08-01, unitCode=HTL-01") sans pretendre les traduire.
    private static string DescribeParameters(string parametersJson)
    {
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(parametersJson);

            if (values is null || values.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(", ", values.Select(pair => $"{pair.Key}={pair.Value}"));
        }
        catch (JsonException)
        {
            return parametersJson;
        }
    }

    private sealed record UnitOption(string? Code, string Label);

    private sealed record ResultRowView(IReadOnlyList<string> Cells, bool IsTotal);

    private sealed record ExecutionRowView(
        string ExecutedAtLabel,
        string ReportTitle,
        string ExecutedBy,
        string ParametersLabel,
        string RowCountLabel,
        string DurationLabel);
}
