using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Desktop.Api;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Desktop;

public partial class MainWindow : Window
{
    private readonly RaqmiApiClient apiClient = new(new HttpClient());
    private IReadOnlyCollection<HotelUnitResponse> hotelUnits = Array.Empty<HotelUnitResponse>();
    private string? editingUnitCode;

    public MainWindow()
    {
        InitializeComponent();
        ApiBaseUrlTextBox.Text = DesktopSettings.Load();
        InitializeDefaults();
        RefreshAuthState();

        // Focus initial sur le champ Utilisateur de l'ecran de connexion.
        Loaded += (_, _) =>
        {
            if (!apiClient.IsAuthenticated)
            {
                UserNameTextBox.Focus();
            }
        };
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
        SetActiveModuleButton(ShowUnitsButton);
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
        foreach (var button in new[] { ShowUnitsButton, ShowRevenueButton, ShowDashboardButton, ShowAuditButton })
        {
            button.Tag = ReferenceEquals(button, active) ? "Active" : null;
        }
    }

    // Fondu discret (150 ms) du contenu a chaque changement de module.
    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged est un evenement routé : ignorer ceux qui remontent des
        // DataGrid/ComboBox internes.
        if (!ReferenceEquals(e.OriginalSource, MainTabs))
        {
            return;
        }

        var fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        MainTabs.BeginAnimation(OpacityProperty, fade);
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        await RunApiActionAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(UserNameTextBox.Text) || string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                SetStatus("Utilisateur et mot de passe requis.", isError: true);
                return;
            }

            var login = await apiClient.LoginAsync(
                ApiBaseUrlTextBox.Text,
                new LoginRequest(UserNameTextBox.Text.Trim(), PasswordBox.Password));

            CurrentUserTextBlock.Text = $"{login.User.DisplayName} - {login.User.UserName}";
            PasswordBox.Password = string.Empty;
            DesktopSettings.Save(ApiBaseUrlTextBox.Text.Trim());
            RefreshAuthState();
            SetStatus("Connexion réussie. Chargement des données...");
            await LoadHotelUnitsAsync();
            await LoadDailyRevenueAsync();
            await LoadUnitDashboardAsync();
            SetStatus("Données chargées.");
        });
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        apiClient.Logout();
        CurrentUserTextBlock.Text = "Non connecté";
        PasswordBox.Password = string.Empty;

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

        ResetUnitForm();
        ResetAmounts();

        MainTabs.SelectedIndex = 0;
        SetActiveModuleButton(ShowUnitsButton);

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

    private void ShowUnitsButton_Click(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 0;
        SetActiveModuleButton(ShowUnitsButton);
    }

    private void ShowRevenueButton_Click(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 1;
        SetActiveModuleButton(ShowRevenueButton);
    }

    private void ShowDashboardButton_Click(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 2;
        SetActiveModuleButton(ShowDashboardButton);
    }

    private void ShowAuditButton_Click(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 3;
        SetActiveModuleButton(ShowAuditButton);
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
        var entryStatusText = !row.HasEntry
            ? "Non saisi"
            : row.Status switch
            {
                DailyRevenueStatus.Draft => "Brouillon",
                DailyRevenueStatus.Submitted => "Soumis - en attente de validation",
                DailyRevenueStatus.Validated => "Validé",
                DailyRevenueStatus.Rejected => "Rejeté",
                _ => "Saisi"
            };

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

        AuditResultCountTextBlock.Text = result.TotalCount > result.Items.Count
            ? $"Affichage de {result.Items.Count} sur {result.TotalCount} entrées. Affinez les filtres pour voir les entrées plus anciennes."
            : $"{result.TotalCount} entrée(s).";
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

            printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Raqmi System - Recettes journalieres");
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
            FontFamily = new FontFamily("Segoe UI")
        };

        document.Blocks.Add(new Paragraph(new Run("Raqmi System - Recettes journalieres"))
        {
            FontSize = 20,
            FontWeight = FontWeights.SemiBold
        });

        document.Blocks.Add(new Paragraph(new Run($"Date d'exploitation : {GetSelectedBusinessDate():yyyy-MM-dd}")));

        document.Blocks.Add(new Paragraph(new Run(
            $"Total: {SummaryTotalTextBlock.Text}  |  Brouillons: {SummaryDraftTextBlock.Text}  |  " +
            $"Soumises: {SummarySubmittedTextBlock.Text}  |  Validées: {SummaryValidatedTextBlock.Text}  |  " +
            $"Rejetées: {SummaryRejectedTextBlock.Text}"))
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
                row.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
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
        await RunApiActionAsync(async () =>
        {
            if (UnitsDataGrid.SelectedItem is not HotelUnitResponse selected)
            {
                SetStatus("Sélectionnez une unité.", isError: true);
                return;
            }

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
        Mouse.OverrideCursor = isBusy ? Cursors.Wait : null;
        BusyProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        LoginBusyProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        LoginButton.IsEnabled = !isBusy;
        RefreshUnitsButton.IsEnabled = !isBusy;
        RefreshRevenueButton.IsEnabled = !isBusy;
        RefreshDashboardButton.IsEnabled = !isBusy;
        RefreshAuditButton.IsEnabled = !isBusy;
        CreateRevenueButton.IsEnabled = !isBusy;
        CreateAndSubmitRevenueButton.IsEnabled = !isBusy;
        NewUnitButton.IsEnabled = !isBusy;
        SaveUnitButton.IsEnabled = !isBusy;
        ActivateUnitButton.IsEnabled = !isBusy;
        DeactivateUnitButton.IsEnabled = !isBusy;
        ValidateRevenueButton.IsEnabled = !isBusy;
        RejectRevenueButton.IsEnabled = !isBusy;
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
