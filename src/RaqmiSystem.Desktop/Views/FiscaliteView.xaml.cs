using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using RaqmiSystem.Application.Fiscalite;
using RaqmiSystem.Domain.Fiscalite;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module 5.4 - Fiscalité DGI &amp; SIFEC : registres TVA ventes/achats, déclaration mensuelle,
/// télédéclaration G50, retenue à la source, liasse fiscale et connecteur SIFEC (mode sandbox
/// uniquement - la production n'a pas d'intégration DGI réelle dans ce dépôt, voir
/// <see cref="SendSifecInvoiceButton_Click"/> et <see cref="TestSifecConnectionButton_Click"/>).
///
/// Deux permissions gèrent l'écriture : finance.fiscal.declare (déclaration, G50, retenue,
/// liasse, transmission SIFEC) et finance.fiscal.sifec.manage (configuration SIFEC seule,
/// restreinte comme dans l'ancien produit). Vue autonome : elle ne connaît que le
/// ModuleViewContext que la fenêtre lui prête.
/// </summary>
public partial class FiscaliteView : UserControl
{
    private ModuleViewContext? context;
    private bool canDeclare;
    private bool canManageSifec;
    private readonly Dictionary<Button, object?> originalToolTips = [];

    private const string DeclareHint = "Permission finance.fiscal.declare requise.";
    private const string SifecManageHint = "Permission finance.fiscal.sifec.manage requise.";

    public FiscaliteView()
    {
        InitializeComponent();
        SalesRegisterPeriodDatePicker.SelectedDate = DateTime.Today;
        PurchaseRegisterPeriodDatePicker.SelectedDate = DateTime.Today;
        DeclarationPeriodDatePicker.SelectedDate = DateTime.Today;
        TeleDeclarationPeriodDatePicker.SelectedDate = DateTime.Today;
        NewPurchaseEntryDateDatePicker.SelectedDate = DateTime.Today;
        WithholdingDateDatePicker.SelectedDate = DateTime.Today;
        FiscalReturnYearTextBox.Text = DateTime.Today.Year.ToString(CultureInfo.InvariantCulture);
        UpdateActionButtons();
    }

    public void Initialize(ModuleViewContext context)
    {
        this.context = context;
        canDeclare = context.HasPermission(PermissionCatalog.FinanceFiscalDeclare);
        canManageSifec = context.HasPermission(PermissionCatalog.FinanceFiscalSifecManage);
        UpdateActionButtons();
    }

    public async Task LoadAsync()
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await ReloadSalesRegisterAsync();
            await ReloadPurchaseRegisterAsync();
            await ReloadDeclarationHistoryAsync();
            await ReloadTeleDeclarationsAsync();
            await ReloadWithholdingAsync();
            await ReloadFiscalReturnsAsync();
            await ReloadSifecAsync();
        });
    }

    public void ResetState()
    {
        SalesRegisterDataGrid.ItemsSource = null;
        PurchaseRegisterDataGrid.ItemsSource = null;
        DeclarationHistoryDataGrid.ItemsSource = null;
        TeleDeclarationsDataGrid.ItemsSource = null;
        WithholdingDataGrid.ItemsSource = null;
        FiscalReturnDataGrid.ItemsSource = null;
        SifecTransmissionsDataGrid.ItemsSource = null;
        DeclarationSummaryPanel.Visibility = Visibility.Collapsed;
        SifecHubEnAttenteTextBlock.Text = "—";
        SifecHubSoumisTextBlock.Text = "—";
        SifecHubAcceptesTextBlock.Text = "—";
        SifecHubRejetesTextBlock.Text = "—";
        SifecHubErreursTextBlock.Text = "—";
        SifecHubModeTextBlock.Text = "—";
        SifecConfigLastTestTextBlock.Text = string.Empty;
        UpdateActionButtons();
    }

    // ============================== Registre TVA ventes ==============================

    private async void RefreshSalesRegisterButton_Click(object sender, RoutedEventArgs e)
    {
        await RunIfReady(async moduleContext =>
        {
            await ReloadSalesRegisterAsync();
            moduleContext.SetStatus("Registre TVA ventes actualisé.");
        });
    }

    private async Task ReloadSalesRegisterAsync()
    {
        var moduleContext = context;
        if (moduleContext is null) return;

        var (year, month) = ReadPeriod(SalesRegisterPeriodDatePicker);
        var entries = await moduleContext.ApiClient.GetVatSalesRegisterAsync(moduleContext.ApiBaseUrl, year, month);
        SalesRegisterDataGrid.ItemsSource = entries.Select(entry => new SalesRegisterRow(entry)).ToList();
    }

    // ============================== Registre TVA achats ==============================

    private async void RefreshPurchaseRegisterButton_Click(object sender, RoutedEventArgs e)
    {
        await RunIfReady(async moduleContext =>
        {
            await ReloadPurchaseRegisterAsync();
            moduleContext.SetStatus("Registre TVA achats actualisé.");
        });
    }

    private async void ImportPurchaseOrdersButton_Click(object sender, RoutedEventArgs e)
    {
        await RunIfReady(async moduleContext =>
        {
            var (year, month) = ReadPeriod(PurchaseRegisterPeriodDatePicker);
            var imported = await moduleContext.ApiClient.ImportApprovedPurchaseOrdersAsync(moduleContext.ApiBaseUrl, year, month);
            await ReloadPurchaseRegisterAsync();
            moduleContext.SetStatus(imported == 0
                ? "Aucun bon de commande à importer pour cette période."
                : $"{imported} bon(s) de commande importé(s) dans le registre.");
        });
    }

    private async void AddPurchaseEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseDecimal(NewPurchaseEntryBaseHtTextBox.Text, out var baseHt) ||
            !TryParseDecimal(NewPurchaseEntryVatAmountTextBox.Text, out var vatAmount))
        {
            context?.SetStatus("Base HT et TVA doivent être des montants valides.", isError: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPurchaseEntrySupplierTextBox.Text) || NewPurchaseEntryDateDatePicker.SelectedDate is null)
        {
            context?.SetStatus("Fournisseur et date sont obligatoires.", isError: true);
            return;
        }

        await RunIfReady(async moduleContext =>
        {
            var request = new CreateVatPurchaseEntryRequest(
                $"MANUEL-{DateTime.UtcNow:yyyyMMddHHmmss}",
                DateOnly.FromDateTime(NewPurchaseEntryDateDatePicker.SelectedDate!.Value),
                NewPurchaseEntrySupplierTextBox.Text.Trim(),
                string.IsNullOrWhiteSpace(NewPurchaseEntrySupplierNifTextBox.Text) ? null : NewPurchaseEntrySupplierNifTextBox.Text.Trim(),
                baseHt,
                vatAmount);

            await moduleContext.ApiClient.CreateVatPurchaseEntryAsync(moduleContext.ApiBaseUrl, request);
            await ReloadPurchaseRegisterAsync();

            NewPurchaseEntrySupplierTextBox.Text = string.Empty;
            NewPurchaseEntrySupplierNifTextBox.Text = string.Empty;
            NewPurchaseEntryBaseHtTextBox.Text = string.Empty;
            NewPurchaseEntryVatAmountTextBox.Text = string.Empty;

            moduleContext.SetStatus("Écriture manuelle ajoutée au registre TVA achats.");
        });
    }

    private async Task ReloadPurchaseRegisterAsync()
    {
        var moduleContext = context;
        if (moduleContext is null) return;

        var (year, month) = ReadPeriod(PurchaseRegisterPeriodDatePicker);
        var entries = await moduleContext.ApiClient.GetVatPurchasesRegisterAsync(moduleContext.ApiBaseUrl, year, month);
        PurchaseRegisterDataGrid.ItemsSource = entries.Select(entry => new PurchaseRegisterRow(entry)).ToList();
    }

    // ============================== Déclaration TVA ==============================

    private async void CalculateDeclarationButton_Click(object sender, RoutedEventArgs e)
    {
        await RunIfReady(async moduleContext =>
        {
            var (year, month) = ReadPeriod(DeclarationPeriodDatePicker);
            var declaration = await moduleContext.ApiClient.CalculateVatDeclarationAsync(moduleContext.ApiBaseUrl, year, month);
            RenderDeclarationSummary(declaration);
            await ReloadDeclarationHistoryAsync();
            moduleContext.SetStatus($"Déclaration TVA {month:00}/{year} calculée : solde {declaration.Solde:N2} DZD.");
        });
    }

    private async Task ReloadDeclarationHistoryAsync()
    {
        var moduleContext = context;
        if (moduleContext is null) return;

        var history = await moduleContext.ApiClient.GetVatDeclarationHistoryAsync(moduleContext.ApiBaseUrl);
        DeclarationHistoryDataGrid.ItemsSource = history.Select(declaration => new DeclarationRow(declaration)).ToList();
    }

    private void RenderDeclarationSummary(VatDeclarationResponse declaration)
    {
        DeclarationSummaryPanel.Visibility = Visibility.Visible;
        DeclarationBaseHtVentesTextBlock.Text = declaration.BaseHtVentes.ToString("N2", CultureInfo.CurrentCulture);
        DeclarationTvaCollecteeTextBlock.Text = declaration.TvaCollectee.ToString("N2", CultureInfo.CurrentCulture);
        DeclarationTvaDeductibleTextBlock.Text = declaration.TvaDeductible.ToString("N2", CultureInfo.CurrentCulture);
        DeclarationCreditAnterieurTextBlock.Text = declaration.CreditAnterieur.ToString("N2", CultureInfo.CurrentCulture);
        DeclarationSoldeTextBlock.Text = declaration.Solde.ToString("N2", CultureInfo.CurrentCulture);
        DeclarationStatusTextBlock.Text = StatusLabel(declaration.Status);
    }

    // ============================== Télédéclarations (G50) ==============================

    private async void ExportG50Button_Click(object sender, RoutedEventArgs e)
    {
        await RunIfReady(async moduleContext =>
        {
            var (year, month) = ReadPeriod(TeleDeclarationPeriodDatePicker);
            var result = await moduleContext.ApiClient.ExportG50Async(moduleContext.ApiBaseUrl, year, month);
            await ReloadTeleDeclarationsAsync();
            await ReloadDeclarationHistoryAsync();
            moduleContext.SetStatus($"G50 {month:00}/{year} exporté ({result.TeleDeclaration.Amount:N2} DZD). Dépôt sur le portail DGI à faire hors application.");
        });
    }

    private async void MarkTeleDeclarationDeclaredButton_Click(object sender, RoutedEventArgs e)
    {
        if (TeleDeclarationsDataGrid.SelectedItem is not TeleDeclarationRow row)
        {
            context?.SetStatus("Sélectionnez une télédéclaration dans la liste.", isError: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(DgiReferenceTextBox.Text))
        {
            context?.SetStatus("Saisissez la référence DGI reçue après dépôt.", isError: true);
            return;
        }

        await RunIfReady(async moduleContext =>
        {
            await moduleContext.ApiClient.MarkTeleDeclarationDeclaredAsync(moduleContext.ApiBaseUrl, row.Id, DgiReferenceTextBox.Text.Trim());
            await ReloadTeleDeclarationsAsync();
            await ReloadDeclarationHistoryAsync();
            DgiReferenceTextBox.Text = string.Empty;
            moduleContext.SetStatus("Télédéclaration marquée déclarée.");
        });
    }

    private async Task ReloadTeleDeclarationsAsync()
    {
        var moduleContext = context;
        if (moduleContext is null) return;

        var teleDeclarations = await moduleContext.ApiClient.GetTeleDeclarationsAsync(moduleContext.ApiBaseUrl);
        TeleDeclarationsDataGrid.ItemsSource = teleDeclarations.Select(t => new TeleDeclarationRow(t)).ToList();
    }

    // ============================== Retenue à la source ==============================

    private async void AddWithholdingButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(WithholdingSupplierTextBox.Text) || WithholdingDateDatePicker.SelectedDate is null ||
            !TryParseDecimal(WithholdingBaseHtTextBox.Text, out var baseHt))
        {
            context?.SetStatus("Fournisseur, date et base HT sont obligatoires.", isError: true);
            return;
        }

        decimal? rate = null;
        if (!string.IsNullOrWhiteSpace(WithholdingRateTextBox.Text))
        {
            if (!TryParseDecimal(WithholdingRateTextBox.Text, out var parsedRate))
            {
                context?.SetStatus("Le taux doit être un nombre valide.", isError: true);
                return;
            }
            rate = parsedRate;
        }

        await RunIfReady(async moduleContext =>
        {
            var request = new CreateWithholdingTaxEntryRequest(
                WithholdingSupplierTextBox.Text.Trim(), baseHt, rate, DateOnly.FromDateTime(WithholdingDateDatePicker.SelectedDate!.Value));

            await moduleContext.ApiClient.CreateWithholdingTaxEntryAsync(moduleContext.ApiBaseUrl, request);
            await ReloadWithholdingAsync();

            WithholdingSupplierTextBox.Text = string.Empty;
            WithholdingBaseHtTextBox.Text = string.Empty;
            WithholdingRateTextBox.Text = string.Empty;

            moduleContext.SetStatus("Retenue à la source enregistrée.");
        });
    }

    private async Task ReloadWithholdingAsync()
    {
        var moduleContext = context;
        if (moduleContext is null) return;

        var entries = await moduleContext.ApiClient.GetWithholdingTaxEntriesAsync(moduleContext.ApiBaseUrl);
        WithholdingDataGrid.ItemsSource = entries.Select(entry => new WithholdingRow(entry)).ToList();
    }

    // ============================== Liasse fiscale ==============================

    private async void RefreshFiscalReturnButton_Click(object sender, RoutedEventArgs e)
    {
        await RunIfReady(async moduleContext =>
        {
            await ReloadFiscalReturnsAsync();
            moduleContext.SetStatus("Liasse fiscale actualisée.");
        });
    }

    private async void GenerateSimpleFiscalReturnButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadYear(out var year)) return;

        await RunIfReady(async moduleContext =>
        {
            await moduleContext.ApiClient.GenerateSimpleFiscalReturnAsync(moduleContext.ApiBaseUrl, year);
            await ReloadFiscalReturnsAsync();
            moduleContext.SetStatus($"Liasse fiscale simple {year} générée.");
        });
    }

    private async void GenerateAdvancedFiscalReturnButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadYear(out var year)) return;

        await RunIfReady(async moduleContext =>
        {
            await moduleContext.ApiClient.GenerateAdvancedFiscalReturnAsync(moduleContext.ApiBaseUrl, year);
            await ReloadFiscalReturnsAsync();
            moduleContext.SetStatus($"Liasse fiscale avancée {year} générée (IBS estimé, non une liquidation officielle).");
        });
    }

    private async Task ReloadFiscalReturnsAsync()
    {
        var moduleContext = context;
        if (moduleContext is null || !TryReadYear(out var year)) return;

        var returns = await moduleContext.ApiClient.GetFiscalReturnsAsync(moduleContext.ApiBaseUrl, year);
        var rows = returns
            .SelectMany(fiscalReturn => fiscalReturn.Lines.Select(line => new FiscalReturnRow(fiscalReturn.Kind, line)))
            .ToList();
        FiscalReturnDataGrid.ItemsSource = rows;
    }

    // ============================== SIFEC ==============================

    private async void RefreshSifecButton_Click(object sender, RoutedEventArgs e)
    {
        await RunIfReady(async moduleContext =>
        {
            await ReloadSifecAsync();
            moduleContext.SetStatus("État SIFEC actualisé.");
        });
    }

    private async void SendSifecInvoiceButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Guid.TryParse(SifecInvoiceIdTextBox.Text, out var invoiceId))
        {
            context?.SetStatus("Identifiant de facture invalide.", isError: true);
            return;
        }

        // Le sandbox simule toujours une acceptation ; la production échoue toujours de façon
        // explicite (aucune intégration DGI réelle dans ce dépôt) - RunAsync affiche l'erreur
        // renvoyée par l'API telle quelle, sans la travestir en succès.
        await RunIfReady(async moduleContext =>
        {
            var transmission = await moduleContext.ApiClient.SendSifecInvoiceAsync(moduleContext.ApiBaseUrl, invoiceId);
            await ReloadSifecAsync();
            moduleContext.SetStatus($"Transmission SIFEC : {StatusLabel(transmission.Status)}.");
        });
    }

    private async void SubmitSifecBatchButton_Click(object sender, RoutedEventArgs e)
    {
        await RunIfReady(async moduleContext =>
        {
            var results = await moduleContext.ApiClient.SubmitSifecBatchAsync(moduleContext.ApiBaseUrl);
            await ReloadSifecAsync();
            moduleContext.SetStatus($"Lot SIFEC transmis : {results.Count} facture(s) traitée(s).");
        });
    }

    private async Task ReloadSifecAsync()
    {
        var moduleContext = context;
        if (moduleContext is null) return;

        var hub = await moduleContext.ApiClient.GetSifecHubSummaryAsync(moduleContext.ApiBaseUrl);
        SifecHubEnAttenteTextBlock.Text = hub.EnAttente.ToString(CultureInfo.CurrentCulture);
        SifecHubSoumisTextBlock.Text = hub.Soumis.ToString(CultureInfo.CurrentCulture);
        SifecHubAcceptesTextBlock.Text = hub.Acceptes.ToString(CultureInfo.CurrentCulture);
        SifecHubRejetesTextBlock.Text = hub.Rejetes.ToString(CultureInfo.CurrentCulture);
        SifecHubErreursTextBlock.Text = hub.Erreurs.ToString(CultureInfo.CurrentCulture);
        SifecHubModeTextBlock.Text = hub.Mode == SifecMode.Sandbox ? "Sandbox" : "Production";

        var transmissions = await moduleContext.ApiClient.GetSifecTransmissionsAsync(moduleContext.ApiBaseUrl);
        SifecTransmissionsDataGrid.ItemsSource = transmissions.Select(t => new SifecTransmissionRow(t)).ToList();

        var config = await moduleContext.ApiClient.GetSifecConfigAsync(moduleContext.ApiBaseUrl);
        RenderSifecConfig(config);
    }

    private async void SaveSifecConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedMode = SifecModeComboBox.SelectedIndex == 1 ? SifecMode.Production : SifecMode.Sandbox;

        await RunIfReady(async moduleContext =>
        {
            var request = new SaveSifecConfigRequest(
                selectedMode,
                string.IsNullOrWhiteSpace(SifecApiUrlTextBox.Text) ? null : SifecApiUrlTextBox.Text.Trim(),
                string.IsNullOrWhiteSpace(SifecApiKeyReferenceTextBox.Text) ? null : SifecApiKeyReferenceTextBox.Text.Trim(),
                string.IsNullOrWhiteSpace(SifecDeclarantNifTextBox.Text) ? null : SifecDeclarantNifTextBox.Text.Trim(),
                SifecIsActiveCheckBox.IsChecked == true);

            var config = await moduleContext.ApiClient.SaveSifecConfigAsync(moduleContext.ApiBaseUrl, request);
            RenderSifecConfig(config);
            moduleContext.SetStatus("Configuration SIFEC enregistrée.");
        });
    }

    private async void TestSifecConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        await RunIfReady(async moduleContext =>
        {
            var config = await moduleContext.ApiClient.TestSifecConnectionAsync(moduleContext.ApiBaseUrl);
            RenderSifecConfig(config);
            moduleContext.SetStatus("Connexion SIFEC : sandbox simulé avec succès.");
        });
    }

    private void RenderSifecConfig(SifecConfigResponse config)
    {
        SifecModeComboBox.SelectedIndex = config.Mode == SifecMode.Production ? 1 : 0;
        SifecApiUrlTextBox.Text = config.ApiUrl ?? string.Empty;
        SifecApiKeyReferenceTextBox.Text = config.ApiKeyReference ?? string.Empty;
        SifecDeclarantNifTextBox.Text = config.DeclarantNif ?? string.Empty;
        SifecIsActiveCheckBox.IsChecked = config.IsActive;

        SifecConfigLastTestTextBlock.Text = config.LastConnectionTestAt is { } testedAt
            ? $"Dernier test : {testedAt.ToLocalTime():dd/MM/yyyy HH:mm} — {(config.LastConnectionTestSucceeded == true ? "réussi" : "échoué")}"
            : "Aucun test de connexion effectué.";
    }

    // ============================== Aides communes ==============================

    private async Task RunIfReady(Func<ModuleViewContext, Task> action)
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(() => action(moduleContext));
    }

    private bool TryReadYear(out int year)
    {
        if (int.TryParse(FiscalReturnYearTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out year) && year is >= 2000 and <= 2100)
        {
            return true;
        }

        context?.SetStatus("Année invalide.", isError: true);
        year = DateTime.Today.Year;
        return false;
    }

    private static (int Year, int Month) ReadPeriod(DatePicker picker)
    {
        var date = picker.SelectedDate ?? DateTime.Today;
        return (date.Year, date.Month);
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
    }

    private static string StatusLabel(DeclarationStatus status) => status switch
    {
        DeclarationStatus.Calculee => "Calculée",
        DeclarationStatus.Exportee => "Exportée",
        DeclarationStatus.Declaree => "Déclarée",
        _ => status.ToString()
    };

    private static string StatusLabel(SifecTransmissionStatus status) => status switch
    {
        SifecTransmissionStatus.Prepare => "En attente",
        SifecTransmissionStatus.Soumis => "Soumis",
        SifecTransmissionStatus.Accepte => "Accepté",
        SifecTransmissionStatus.Rejete => "Rejeté",
        SifecTransmissionStatus.Erreur => "Erreur",
        _ => status.ToString()
    };

    // Les boutons d'ecriture restent visibles mais desactives, avec info-bulle explicative,
    // plutot que masques (motif ApplyPermissionHint, voir BackupView).
    private void UpdateActionButtons()
    {
        foreach (var button in new[]
                 {
                     ImportPurchaseOrdersButton, AddPurchaseEntryButton, CalculateDeclarationButton, ExportG50Button,
                     MarkTeleDeclarationDeclaredButton, AddWithholdingButton, GenerateSimpleFiscalReturnButton,
                     GenerateAdvancedFiscalReturnButton, SendSifecInvoiceButton, SubmitSifecBatchButton
                 })
        {
            button.IsEnabled = canDeclare;
            ApplyPermissionHint(button, canDeclare, DeclareHint);
        }

        foreach (var button in new[] { SaveSifecConfigButton, TestSifecConnectionButton })
        {
            button.IsEnabled = canManageSifec;
            ApplyPermissionHint(button, canManageSifec, SifecManageHint);
        }
    }

    private void ApplyPermissionHint(Button button, bool allowed, string hint)
    {
        if (!originalToolTips.ContainsKey(button))
        {
            originalToolTips[button] = button.ToolTip;
        }

        button.ToolTip = allowed ? originalToolTips[button] : hint;
    }

    private sealed class SalesRegisterRow(VatSalesRegisterEntryResponse entry)
    {
        public string PieceNumber { get; } = entry.PieceNumber;
        public DateOnly PieceDate { get; } = entry.PieceDate;
        public string CustomerName { get; } = entry.CustomerName;
        public string TypeLabel { get; } = entry.Type == VatMovementType.Avoir ? "Avoir" : "Vente";
        public decimal BaseHt { get; } = entry.BaseHt;
        public decimal VatAmount { get; } = entry.VatAmount;
        public decimal Ttc { get; } = entry.Ttc;
    }

    private sealed class PurchaseRegisterRow(VatPurchaseRegisterEntryResponse entry)
    {
        public string PieceNumber { get; } = entry.PieceNumber;
        public DateOnly PieceDate { get; } = entry.PieceDate;
        public string SupplierName { get; } = entry.SupplierName;
        public decimal BaseHt { get; } = entry.BaseHt;
        public decimal VatAmount { get; } = entry.VatAmount;
        public decimal Ttc { get; } = entry.Ttc;
        public string SourceLabel { get; } = entry.Source == VatRegisterSource.Achats ? "Achats" : "Manuel";
    }

    private sealed class DeclarationRow(VatDeclarationResponse declaration)
    {
        public string Period { get; } = $"{declaration.Month:00}/{declaration.Year}";
        public decimal BaseHtVentes { get; } = declaration.BaseHtVentes;
        public decimal TvaCollectee { get; } = declaration.TvaCollectee;
        public decimal TvaDeductible { get; } = declaration.TvaDeductible;
        public decimal CreditAnterieur { get; } = declaration.CreditAnterieur;
        public decimal Solde { get; } = declaration.Solde;
        public string StatusLabel { get; } = FiscaliteView.StatusLabel(declaration.Status);
    }

    private sealed class TeleDeclarationRow(TeleDeclarationResponse teleDeclaration)
    {
        public Guid Id { get; } = teleDeclaration.Id;
        public string Period { get; } = $"{teleDeclaration.Month:00}/{teleDeclaration.Year}";
        public decimal Amount { get; } = teleDeclaration.Amount;
        public string StatusLabel { get; } = FiscaliteView.StatusLabel(teleDeclaration.Status);
        public string DgiReference { get; } = teleDeclaration.DgiReference ?? "—";
        public string ExportedLabel { get; } = teleDeclaration.ExportedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
    }

    private sealed class WithholdingRow(WithholdingTaxEntryResponse entry)
    {
        public string SupplierName { get; } = entry.SupplierName;
        public DateOnly Date { get; } = entry.Date;
        public decimal BaseHt { get; } = entry.BaseHt;
        public decimal Rate { get; } = entry.Rate;
        public decimal MontantRetenu { get; } = entry.MontantRetenu;
    }

    private sealed class FiscalReturnRow(FiscalReturnKind kind, FiscalReturnLineResponse line)
    {
        public string KindLabel { get; } = kind == FiscalReturnKind.Avancee ? "Avancée" : "Simple";
        public string Code { get; } = line.Code;
        public string Label { get; } = line.Label;
        public decimal Amount { get; } = line.Amount;
    }

    private sealed class SifecTransmissionRow(SifecTransmissionResponse transmission)
    {
        public Guid InvoiceId { get; } = transmission.InvoiceId;
        public string StatusLabel { get; } = FiscaliteView.StatusLabel(transmission.Status);
        public string Uid { get; } = transmission.Uid ?? "—";
        public string ModeLabel { get; } = transmission.Mode == SifecMode.Production ? "Production" : "Sandbox";
        public string SubmittedLabel { get; } = transmission.SubmittedAt is { } submittedAt
            ? submittedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture)
            : "—";
        public string Message { get; } = transmission.ResponseMessage ?? string.Empty;
    }
}
