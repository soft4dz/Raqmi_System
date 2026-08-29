using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Domain.Billing;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module Facturation : liste des factures de vente, detail de la facture
/// selectionnee, creation d'un brouillon et cycle de vie
/// (modification des lignes, emission, encaissement, annulation motivee).
///
/// La vue est autonome : elle ne connait ni MainWindow ni les autres modules,
/// et passe par <see cref="ModuleViewContext.RunAsync"/> pour tout appel API
/// (curseur d'attente, barre de progression, traduction des erreurs).
/// </summary>
public partial class InvoicesView : UserControl
{
    /// <summary>
    /// Taux de TVA algeriens admis par le domaine (exonere, reduit, normal).
    /// Reference directement la constante metier : aucune valeur recopiee.
    /// </summary>
    public static IReadOnlyList<decimal> VatRateOptions { get; } = InvoiceLine.AllowedVatRates.ToArray();

    /// <summary>Entree de tete du choix du client : presente, mais non selectionnable.</summary>
    private const string CustomerPlaceholderLabel = "Sélectionner un client…";

    /// <summary>
    /// Capacite des colonnes de ligne de facture (InvoiceLineConfiguration) :
    /// quantite numeric(18,3), prix unitaire et total HT numeric(18,2). Au-dela,
    /// PostgreSQL refuse la valeur ; la saisie est donc bornee ici, avec un message
    /// explicite plutot qu'une erreur serveur apres l'aller-retour.
    /// </summary>
    private const decimal MaxQuantity = 999_999_999_999_999.999m;

    private const decimal MaxMoney = 9_999_999_999_999_999.99m;

    private readonly ObservableCollection<InvoiceLineEditorRow> editorLines = new();

    private ModuleViewContext? context;
    private IReadOnlyList<InvoiceResponse> invoices = Array.Empty<InvoiceResponse>();
    private IReadOnlyList<CustomerResponse> customers = Array.Empty<CustomerResponse>();
    private IReadOnlyList<HotelUnitResponse> hotelUnits = Array.Empty<HotelUnitResponse>();

    // Null : le formulaire cree une nouvelle facture. Renseigne : il modifie les
    // lignes du brouillon dont l'identifiant est memorise ici.
    private Guid? editingInvoiceId;

    public InvoicesView()
    {
        InitializeComponent();

        // Les formats {0:N2} des grilles suivent la culture de l'utilisateur,
        // comme les montants formates dans le code-behind.
        var languageTag = CultureInfo.CurrentCulture.IetfLanguageTag;

        if (!string.IsNullOrEmpty(languageTag))
        {
            Language = XmlLanguage.GetLanguage(languageTag);
        }

        editorLines.CollectionChanged += EditorLines_CollectionChanged;
        EditorLinesDataGrid.ItemsSource = editorLines;

        var today = DateTime.Today;
        FromDatePicker.SelectedDate = new DateTime(today.Year, today.Month, 1);
        ToDatePicker.SelectedDate = today;
        InvoiceDatePicker.SelectedDate = today;

        ResetEditor();
        ShowSelectedInvoiceDetail();
        UpdateActionAvailability();
    }

    /// <summary>Memorise le contexte prete par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext moduleViewContext)
    {
        context = moduleViewContext;
    }

    /// <summary>
    /// (Re)charge les listes de reference et les factures correspondant aux filtres.
    /// Sort silencieusement tant qu'aucun contexte n'a ete fourni ou que la session
    /// n'est pas ouverte.
    /// </summary>
    public async Task LoadAsync()
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            await LoadReferenceDataAsync(active);
            await LoadInvoicesAsync(active);

            active.SetStatus(invoices.Count == 0
                ? "Aucune facture pour ces critères."
                : $"{invoices.Count} facture(s) chargée(s).");
        });
    }

    /// <summary>
    /// Vide toutes les surfaces de la vue : appelee a la deconnexion pour ne jamais
    /// laisser les donnees d'un utilisateur affichees pour le suivant.
    /// </summary>
    public void ResetState()
    {
        invoices = Array.Empty<InvoiceResponse>();
        customers = Array.Empty<CustomerResponse>();
        hotelUnits = Array.Empty<HotelUnitResponse>();

        InvoicesDataGrid.ItemsSource = null;
        CustomerFilterComboBox.ItemsSource = null;
        UnitFilterComboBox.ItemsSource = null;
        EditorCustomerComboBox.ItemsSource = null;
        EditorUnitComboBox.ItemsSource = null;
        StatusFilterComboBox.SelectedIndex = 0;
        CancelReasonTextBox.Text = string.Empty;
        InvoiceCountTextBlock.Text = "Aucune facture chargée.";

        ResetEditor();
        ShowSelectedInvoiceDetail();
        UpdateActionAvailability();
    }

    // ============================== Chargements ==============================

    private async Task LoadReferenceDataAsync(ModuleViewContext active)
    {
        customers = (await active.ApiClient.GetCustomersAsync(active.ApiBaseUrl, search: null, includeInactive: false))
            .OrderBy(customer => customer.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        hotelUnits = (await active.ApiClient.GetHotelUnitsAsync(active.ApiBaseUrl, includeInactive: false))
            .Where(unit => unit.IsActive)
            .OrderBy(unit => unit.DisplayOrder)
            .ThenBy(unit => unit.Name)
            .ToArray();

        var customerOptions = customers
            .Select(customer => new InvoiceCodeOption(customer.Code, $"{customer.Code} — {customer.Name}"))
            .ToList();

        var unitOptions = hotelUnits
            .Select(unit => new InvoiceCodeOption(unit.Code, $"{unit.Code} — {unit.Name}"))
            .ToList();

        var customerFilterOptions = new List<InvoiceCodeOption> { new(null, "Tous les clients") };
        customerFilterOptions.AddRange(customerOptions);

        var unitFilterOptions = new List<InvoiceCodeOption> { new(null, "Toutes les unités") };
        unitFilterOptions.AddRange(unitOptions);

        // Le client a facturer n'est jamais preselectionne : une facture emise au
        // mauvais client ne se corrige que par une annulation motivee. L'entree de
        // tete, non selectionnable, oblige a un choix explicite.
        var editorCustomerOptions = new List<InvoiceCodeOption> { new(null, CustomerPlaceholderLabel) };
        editorCustomerOptions.AddRange(customerOptions);

        // Les selections en cours sont conservees quand le code existe toujours.
        RebindOptions(CustomerFilterComboBox, customerFilterOptions, customerFilterOptions[0]);
        RebindOptions(UnitFilterComboBox, unitFilterOptions, unitFilterOptions[0]);
        RebindOptions(EditorCustomerComboBox, editorCustomerOptions, editorCustomerOptions[0]);
        RebindOptions(EditorUnitComboBox, unitOptions, unitOptions.FirstOrDefault());
    }

    private static void RebindOptions(ComboBox comboBox, List<InvoiceCodeOption> options, InvoiceCodeOption? fallback)
    {
        var previousCode = (comboBox.SelectedItem as InvoiceCodeOption)?.Code;

        comboBox.ItemsSource = options;
        comboBox.SelectedItem = options.FirstOrDefault(option => option.Code == previousCode) ?? fallback;
    }

    private async Task LoadInvoicesAsync(ModuleViewContext active)
    {
        var selectedId = (InvoicesDataGrid.SelectedItem as InvoiceResponse)?.Id;

        invoices = (await active.ApiClient.GetInvoicesAsync(
                active.ApiBaseUrl,
                ToDateOnly(FromDatePicker.SelectedDate),
                ToDateOnly(ToDatePicker.SelectedDate),
                (CustomerFilterComboBox.SelectedItem as InvoiceCodeOption)?.Code,
                (UnitFilterComboBox.SelectedItem as InvoiceCodeOption)?.Code,
                SelectedStatusFilter()))
            .ToArray();

        InvoicesDataGrid.ItemsSource = invoices;
        InvoiceCountTextBlock.Text = invoices.Count switch
        {
            0 => "Aucune facture pour ces critères.",
            1 => "1 facture affichée.",
            _ => $"{invoices.Count} factures affichées."
        };

        SelectInvoice(selectedId);
    }

    private void SelectInvoice(Guid? invoiceId)
    {
        InvoicesDataGrid.SelectedItem = invoiceId is Guid id
            ? invoices.FirstOrDefault(invoice => invoice.Id == id)
            : null;

        if (InvoicesDataGrid.SelectedItem is not null)
        {
            InvoicesDataGrid.ScrollIntoView(InvoicesDataGrid.SelectedItem);
        }

        ShowSelectedInvoiceDetail();
        UpdateActionAvailability();
    }

    private string? SelectedStatusFilter()
    {
        var tag = (StatusFilterComboBox.SelectedItem as ComboBoxItem)?.Tag as string;

        return string.IsNullOrWhiteSpace(tag) ? null : tag;
    }

    // ================================ Detail =================================

    private void InvoicesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ShowSelectedInvoiceDetail();
        UpdateActionAvailability();
    }

    private void ShowSelectedInvoiceDetail()
    {
        var invoice = InvoicesDataGrid.SelectedItem as InvoiceResponse;

        // Le panneau de detail est entierement pilote par ce DataContext : les
        // libelles, les totaux et les lignes suivent la facture selectionnee.
        DetailPanel.DataContext = invoice;

        ApplyDetailStatusBadge(invoice?.Status);
        ApplyIssuerBlock(invoice);

        var trace = BuildTraceText(invoice);
        DetailTraceTextBlock.Text = trace;
        DetailTraceTextBlock.Visibility = string.IsNullOrEmpty(trace) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Affiche l'identite de l'emetteur telle qu'elle a ete FIGEE sur la facture a
    /// son emission (colonnes Issuer* de la reponse). Elle est copiee depuis le
    /// parametrage global au moment de l'emission et ne suit plus ses modifications
    /// ensuite : c'est ce qui rend le parametrage opposable.
    ///
    /// Un brouillon n'en porte aucune - il serait faux de lui montrer le
    /// parametrage courant comme s'il etait deja acquis, puisqu'il peut encore
    /// changer d'ici l'emission.
    /// </summary>
    private void ApplyIssuerBlock(InvoiceResponse? invoice)
    {
        var hasIssuer = invoice is not null && !string.IsNullOrWhiteSpace(invoice.IssuerName);

        IssuerNameTextBlock.Text = hasIssuer ? invoice!.IssuerName : string.Empty;
        IssuerNameTextBlock.Visibility = hasIssuer ? Visibility.Visible : Visibility.Collapsed;

        var identifiers = hasIssuer ? BuildIssuerIdentifiers(invoice!) : string.Empty;
        IssuerIdentifiersTextBlock.Text = identifiers;
        IssuerIdentifiersTextBlock.Visibility = string.IsNullOrEmpty(identifiers)
            ? Visibility.Collapsed
            : Visibility.Visible;

        var address = hasIssuer ? invoice!.IssuerAddress : null;
        IssuerAddressTextBlock.Text = address ?? string.Empty;
        IssuerAddressTextBlock.Visibility = string.IsNullOrWhiteSpace(address)
            ? Visibility.Collapsed
            : Visibility.Visible;

        IssuerPendingTextBlock.Text = invoice is null
            ? "Sélectionnez une facture."
            : "L'émetteur sera figé à l'émission : la facture portera alors l'identité de l'établissement telle qu'elle sera paramétrée ce jour-là.";
        IssuerPendingTextBlock.Visibility = hasIssuer ? Visibility.Collapsed : Visibility.Visible;
    }

    // NIF, RC, AI et NIS de l'emetteur sur une seule ligne : ceux qui n'ont pas ete
    // renseignes au moment de l'emission sont simplement absents, plutot que
    // presentes comme des cases vides.
    private static string BuildIssuerIdentifiers(InvoiceResponse invoice)
    {
        var parts = new List<string>();

        AddIdentifier(parts, "NIF", invoice.IssuerNif);
        AddIdentifier(parts, "RC", invoice.IssuerRc);
        AddIdentifier(parts, "AI", invoice.IssuerAi);
        AddIdentifier(parts, "NIS", invoice.IssuerNis);

        return string.Join("  ·  ", parts);
    }

    private static void AddIdentifier(List<string> parts, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{label} {value}");
        }
    }

    private static string BuildTraceText(InvoiceResponse? invoice)
    {
        if (invoice is null)
        {
            return string.Empty;
        }

        var lines = new List<string>();

        if (invoice.IssuedAt is DateTimeOffset issuedAt)
        {
            lines.Add($"Émise le {FormatMoment(issuedAt)} par {invoice.IssuedBy ?? "—"}.");
        }

        if (invoice.PaidAt is DateTimeOffset paidAt)
        {
            lines.Add($"Payée le {FormatMoment(paidAt)} par {invoice.PaidBy ?? "—"}.");
        }

        if (invoice.CancelledAt is DateTimeOffset cancelledAt)
        {
            lines.Add($"Annulée le {FormatMoment(cancelledAt)} par {invoice.CancelledBy ?? "—"}.");

            if (!string.IsNullOrWhiteSpace(invoice.CancellationReason))
            {
                lines.Add($"Motif : {invoice.CancellationReason}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void ApplyDetailStatusBadge(InvoiceStatus? status)
    {
        var (background, foreground, label) = status switch
        {
            InvoiceStatus.Issued => ("AccentSoftBrush", "ModuleStatusApiForegroundBrush", "Émise"),
            InvoiceStatus.Paid => ("StatusValidatedBackgroundBrush", "StatusValidatedForegroundBrush", "Payée"),
            InvoiceStatus.Cancelled => ("StatusRejectedBackgroundBrush", "StatusRejectedForegroundBrush", "Annulée"),
            InvoiceStatus.Draft => ("StatusDraftBackgroundBrush", "StatusDraftForegroundBrush", "Brouillon"),
            _ => ("StatusDraftBackgroundBrush", "StatusDraftForegroundBrush", "—")
        };

        if (TryFindResource(background) is Brush backgroundBrush)
        {
            DetailStatusBadge.Background = backgroundBrush;
        }

        if (TryFindResource(foreground) is Brush foregroundBrush)
        {
            DetailStatusBadgeText.Foreground = foregroundBrush;
        }

        DetailStatusBadgeText.Text = label;
    }

    // ============================ Actions du cycle ============================

    private void UpdateActionAvailability()
    {
        var status = (InvoicesDataGrid.SelectedItem as InvoiceResponse)?.Status;
        var canCancel = status is InvoiceStatus.Draft or InvoiceStatus.Issued;

        // Plutot que de laisser l'utilisateur declencher une erreur API previsible,
        // chaque bouton n'est actif que pour les statuts qui l'admettent.
        EditLinesButton.IsEnabled = status == InvoiceStatus.Draft;
        IssueInvoiceButton.IsEnabled = status == InvoiceStatus.Draft;
        MarkPaidButton.IsEnabled = status == InvoiceStatus.Issued;

        // L'annulation exige un motif : le bouton ne s'active qu'une fois ce motif
        // saisi, plutot que d'ouvrir une confirmation vouee a un refus.
        CancelInvoiceButton.IsEnabled = canCancel && !string.IsNullOrWhiteSpace(CancelReasonTextBox.Text);
        CancelReasonTextBox.IsEnabled = canCancel;
    }

    private void CancelReasonTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateActionAvailability();
    }

    private async void RefreshInvoicesButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    private async void EditLinesButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || InvoicesDataGrid.SelectedItem is not InvoiceResponse selected)
        {
            return;
        }

        if (selected.Status != InvoiceStatus.Draft)
        {
            active.SetStatus("Seule une facture en brouillon peut être modifiée.", isError: true);
            return;
        }

        await active.RunAsync(async () =>
        {
            // Relecture de la facture pour repartir des lignes reellement stockees.
            var invoice = await active.ApiClient.GetInvoiceAsync(active.ApiBaseUrl, selected.Id);

            LoadInvoiceIntoEditor(invoice);
            active.SetStatus($"Modification des lignes du brouillon du {invoice.InvoiceDate:yyyy-MM-dd} ({invoice.CustomerCode}).");
        });
    }

    private async void IssueInvoiceButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || InvoicesDataGrid.SelectedItem is not InvoiceResponse selected)
        {
            return;
        }

        if (selected.Status != InvoiceStatus.Draft)
        {
            active.SetStatus("Seule une facture en brouillon peut être émise.", isError: true);
            return;
        }

        if (selected.Lines.Count == 0)
        {
            active.SetStatus("Une facture doit comporter au moins une ligne pour être émise.", isError: true);
            return;
        }

        var confirmed = Confirm(
            "L'émission attribue le numéro définitif de la facture et la fige :"
            + Environment.NewLine + Environment.NewLine
            + "• le numéro légal est alloué et ne pourra plus changer ;"
            + Environment.NewLine
            + "• les montants et les lignes ne seront plus modifiables ;"
            + Environment.NewLine
            + "• l'identification du client (nom, NIF, RC, AI, NIS, adresse) est conservée telle qu'elle est aujourd'hui."
            + Environment.NewLine + Environment.NewLine
            + $"Client : {selected.CustomerName ?? selected.CustomerCode}"
            + Environment.NewLine
            + $"Total TTC : {FormatAmount(selected.TotalInclVat)}"
            + Environment.NewLine + Environment.NewLine
            + "Émettre cette facture ?",
            "Émettre la facture");

        if (!confirmed)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            var issued = await active.ApiClient.IssueInvoiceAsync(active.ApiBaseUrl, selected.Id);

            await LoadInvoicesAsync(active);
            SelectInvoice(issued.Id);
            active.SetStatus($"Facture émise sous le numéro {issued.Number}.");
        });
    }

    private async void MarkPaidButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || InvoicesDataGrid.SelectedItem is not InvoiceResponse selected)
        {
            return;
        }

        if (selected.Status != InvoiceStatus.Issued)
        {
            active.SetStatus("Seule une facture émise peut être marquée payée.", isError: true);
            return;
        }

        var confirmed = Confirm(
            $"Enregistrer l'encaissement de la facture {selected.Number} ?"
            + Environment.NewLine + Environment.NewLine
            + $"Client : {selected.CustomerName ?? selected.CustomerCode}"
            + Environment.NewLine
            + $"Total TTC : {FormatAmount(selected.TotalInclVat)}",
            "Marquer la facture payée");

        if (!confirmed)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            var paid = await active.ApiClient.MarkInvoicePaidAsync(active.ApiBaseUrl, selected.Id);

            await LoadInvoicesAsync(active);
            SelectInvoice(paid.Id);
            active.SetStatus($"Facture {paid.Number} marquée payée.");
        });
    }

    private async void CancelInvoiceButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || InvoicesDataGrid.SelectedItem is not InvoiceResponse selected)
        {
            return;
        }

        if (selected.Status is not (InvoiceStatus.Draft or InvoiceStatus.Issued))
        {
            active.SetStatus("Seule une facture en brouillon ou émise peut être annulée.", isError: true);
            return;
        }

        var reason = CancelReasonTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(reason))
        {
            active.SetStatus("Le motif d'annulation est obligatoire.", isError: true);
            CancelReasonTextBox.Focus();
            return;
        }

        var label = selected.Number ?? $"brouillon du {selected.InvoiceDate:yyyy-MM-dd}";

        var confirmed = Confirm(
            $"Annuler la facture {label} ?"
            + Environment.NewLine + Environment.NewLine
            + "L'annulation est définitive : la facture reste conservée, mais neutralisée, avec son motif et son auteur."
            + Environment.NewLine + Environment.NewLine
            + $"Motif : {reason}",
            "Annuler la facture");

        if (!confirmed)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            var cancelled = await active.ApiClient.CancelInvoiceAsync(
                active.ApiBaseUrl,
                selected.Id,
                new CancelInvoiceRequest(reason));

            CancelReasonTextBox.Text = string.Empty;

            await LoadInvoicesAsync(active);
            SelectInvoice(cancelled.Id);
            active.SetStatus($"Facture {label} annulée.");
        });
    }

    // ======================= Saisie d'un brouillon ==========================

    private void NewDraftButton_Click(object sender, RoutedEventArgs e)
    {
        ResetEditor();
        context?.SetStatus("Formulaire vidé : nouvelle facture brouillon.");
    }

    private void AddLineButton_Click(object sender, RoutedEventArgs e)
    {
        editorLines.Add(new InvoiceLineEditorRow());
        EditorLinesDataGrid.ScrollIntoView(editorLines[^1]);
    }

    private void RemoveLineButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: InvoiceLineEditorRow row })
        {
            editorLines.Remove(row);
        }
    }

    private async void SaveInvoiceButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            if (!TryBuildLines(active, out var lines))
            {
                return;
            }

            InvoiceResponse saved;

            if (editingInvoiceId is Guid invoiceId)
            {
                saved = await active.ApiClient.UpdateInvoiceLinesAsync(
                    active.ApiBaseUrl,
                    invoiceId,
                    new UpdateInvoiceLinesRequest(lines));

                active.SetStatus($"Lignes du brouillon mises à jour ({lines.Count} ligne(s)).");
            }
            else
            {
                var customerCode = (EditorCustomerComboBox.SelectedItem as InvoiceCodeOption)?.Code;

                if (string.IsNullOrWhiteSpace(customerCode))
                {
                    active.SetStatus("Sélectionnez le client à facturer.", isError: true);
                    return;
                }

                var unitCode = (EditorUnitComboBox.SelectedItem as InvoiceCodeOption)?.Code;

                if (string.IsNullOrWhiteSpace(unitCode))
                {
                    active.SetStatus("Sélectionnez l'unité hôtelière émettrice.", isError: true);
                    return;
                }

                if (InvoiceDatePicker.SelectedDate is not DateTime invoiceDate)
                {
                    active.SetStatus("La date de facture est obligatoire.", isError: true);
                    return;
                }

                saved = await active.ApiClient.CreateInvoiceAsync(
                    active.ApiBaseUrl,
                    new CreateInvoiceRequest(customerCode, unitCode, DateOnly.FromDateTime(invoiceDate), lines));

                active.SetStatus("Facture créée en brouillon : sans numéro, modifiable jusqu'à son émission.");
            }

            ResetEditor();
            await LoadInvoicesAsync(active);
            SelectInvoice(saved.Id);
        });
    }

    /// <summary>
    /// Controle de saisie aligne sur les regles du domaine (InvoiceLine) : au moins
    /// une ligne, quantite strictement positive a 3 decimales au plus, prix unitaire
    /// positif ou nul a 2 decimales au plus, taux de TVA admis.
    /// </summary>
    private bool TryBuildLines(ModuleViewContext active, out IReadOnlyCollection<InvoiceLineRequest> lines)
    {
        lines = Array.Empty<InvoiceLineRequest>();

        if (editorLines.Count == 0)
        {
            active.SetStatus("Ajoutez au moins une ligne à la facture.", isError: true);
            return false;
        }

        var result = new List<InvoiceLineRequest>(editorLines.Count);
        var lineNumber = 1;

        foreach (var row in editorLines)
        {
            var designation = row.Designation.Trim();

            if (string.IsNullOrWhiteSpace(designation))
            {
                active.SetStatus($"Ligne {lineNumber} : la désignation est obligatoire.", isError: true);
                return false;
            }

            if (designation.Length > 300)
            {
                active.SetStatus($"Ligne {lineNumber} : la désignation ne peut pas dépasser 300 caractères.", isError: true);
                return false;
            }

            if (!row.TryGetQuantity(out var quantity))
            {
                active.SetStatus($"Ligne {lineNumber} : la quantité doit être un nombre valide.", isError: true);
                return false;
            }

            if (quantity <= 0)
            {
                active.SetStatus($"Ligne {lineNumber} : la quantité doit être strictement positive.", isError: true);
                return false;
            }

            if (decimal.Round(quantity, 3) != quantity)
            {
                active.SetStatus($"Ligne {lineNumber} : la quantité accepte 3 décimales au maximum.", isError: true);
                return false;
            }

            if (quantity > MaxQuantity)
            {
                active.SetStatus(
                    $"Ligne {lineNumber} : la quantité ne peut pas dépasser {MaxQuantity.ToString("N3", CultureInfo.CurrentCulture)}.",
                    isError: true);
                return false;
            }

            if (!row.TryGetUnitPrice(out var unitPrice))
            {
                active.SetStatus($"Ligne {lineNumber} : le prix unitaire doit être un montant valide.", isError: true);
                return false;
            }

            if (unitPrice < 0)
            {
                active.SetStatus($"Ligne {lineNumber} : le prix unitaire ne peut pas être négatif.", isError: true);
                return false;
            }

            if (decimal.Round(unitPrice, 2) != unitPrice)
            {
                active.SetStatus($"Ligne {lineNumber} : le prix unitaire accepte 2 décimales au maximum.", isError: true);
                return false;
            }

            if (unitPrice > MaxMoney)
            {
                active.SetStatus(
                    $"Ligne {lineNumber} : le prix unitaire ne peut pas dépasser {MaxMoney.ToString("N2", CultureInfo.CurrentCulture)}.",
                    isError: true);
                return false;
            }

            // Le total de ligne est stocke dans une colonne de meme capacite que le
            // prix unitaire : une quantite et un prix acceptables isolement peuvent
            // produire un total qui ne l'est pas. Le controle se fait par division
            // pour ne pas provoquer d'OverflowException sur le produit lui-meme.
            if (unitPrice > 0 && quantity > MaxMoney / unitPrice)
            {
                active.SetStatus(
                    $"Ligne {lineNumber} : le total HT de la ligne dépasse le montant maximal de {MaxMoney.ToString("N2", CultureInfo.CurrentCulture)}.",
                    isError: true);
                return false;
            }

            if (!VatRateOptions.Contains(row.VatRate))
            {
                active.SetStatus($"Ligne {lineNumber} : le taux de TVA doit être 0, 9 ou 19 %.", isError: true);
                return false;
            }

            result.Add(new InvoiceLineRequest(designation, quantity, unitPrice, row.VatRate));
            lineNumber++;
        }

        lines = result;
        return true;
    }

    private void LoadInvoiceIntoEditor(InvoiceResponse invoice)
    {
        editingInvoiceId = invoice.Id;

        ClearEditorLines();

        foreach (var line in invoice.Lines.OrderBy(item => item.LineNumber))
        {
            editorLines.Add(InvoiceLineEditorRow.FromResponse(line));
        }

        SelectOption(EditorCustomerComboBox, invoice.CustomerCode);
        SelectOption(EditorUnitComboBox, invoice.HotelUnitCode);
        InvoiceDatePicker.SelectedDate = invoice.InvoiceDate.ToDateTime(TimeOnly.MinValue);

        // En modification, seules les lignes sont envoyees au serveur : l'en-tete
        // de la facture reste celui du brouillon existant.
        EditorCustomerComboBox.IsEnabled = false;
        EditorUnitComboBox.IsEnabled = false;
        InvoiceDatePicker.IsEnabled = false;

        EditorTitleTextBlock.Text = "Modification des lignes d'un brouillon";
        EditorHintTextBlock.Text = $"Brouillon du {invoice.InvoiceDate:yyyy-MM-dd} — client {invoice.CustomerCode}, unité {invoice.HotelUnitCode}. Seules les lignes sont modifiables.";
        SaveInvoiceButton.Content = "Enregistrer les lignes";

        UpdateEditorTotals();
    }

    private void ResetEditor()
    {
        editingInvoiceId = null;

        ClearEditorLines();

        EditorCustomerComboBox.IsEnabled = true;
        EditorUnitComboBox.IsEnabled = true;
        InvoiceDatePicker.IsEnabled = true;
        InvoiceDatePicker.SelectedDate = DateTime.Today;

        // Le client du brouillon precedent ne doit pas etre reconduit en silence
        // sur la facture suivante : le formulaire repart sur l'entree de tete.
        SelectCustomerPlaceholder();

        EditorTitleTextBlock.Text = "Nouvelle facture (brouillon)";
        EditorHintTextBlock.Text = "Un brouillon ne porte pas de numéro et reste modifiable jusqu'à son émission.";
        SaveInvoiceButton.Content = "Créer le brouillon";

        UpdateEditorTotals();
    }

    private void SelectCustomerPlaceholder()
    {
        if (EditorCustomerComboBox.ItemsSource is IEnumerable<InvoiceCodeOption> options)
        {
            EditorCustomerComboBox.SelectedItem = options.FirstOrDefault(option => option.Code is null);
        }
    }

    private static void SelectOption(ComboBox comboBox, string code)
    {
        if (comboBox.ItemsSource is IEnumerable<InvoiceCodeOption> options)
        {
            comboBox.SelectedItem = options.FirstOrDefault(option =>
                string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void ClearEditorLines()
    {
        foreach (var row in editorLines)
        {
            row.PropertyChanged -= EditorLine_PropertyChanged;
        }

        editorLines.Clear();
    }

    private void EditorLines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (InvoiceLineEditorRow row in e.OldItems)
            {
                row.PropertyChanged -= EditorLine_PropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (InvoiceLineEditorRow row in e.NewItems)
            {
                row.PropertyChanged += EditorLine_PropertyChanged;
            }
        }

        UpdateEditorTotals();
    }

    private void EditorLine_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateEditorTotals();
    }

    /// <summary>
    /// Apercu local des totaux pendant la saisie. La source de verite reste le
    /// serveur, qui renvoie les totaux definitifs a l'enregistrement.
    /// </summary>
    private void UpdateEditorTotals()
    {
        var totalExclVat = 0m;
        var totalVat = 0m;

        foreach (var row in editorLines)
        {
            totalExclVat += row.LineTotalExclVat;
            totalVat += row.VatAmount;
        }

        EditorTotalExclVatTextBlock.Text = FormatAmount(totalExclVat);
        EditorTotalVatTextBlock.Text = FormatAmount(totalVat);
        EditorTotalInclVatTextBlock.Text = FormatAmount(totalExclVat + totalVat);
    }

    // ================================ Outils =================================

    private bool Confirm(string message, string caption)
    {
        var owner = Window.GetWindow(this);

        var result = owner is null
            ? MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(owner, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    private static DateOnly? ToDateOnly(DateTime? value)
    {
        return value.HasValue ? DateOnly.FromDateTime(value.Value) : null;
    }

    private static string FormatAmount(decimal value)
    {
        return value.ToString("N2", CultureInfo.CurrentCulture);
    }

    private static string FormatMoment(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
    }
}

/// <summary>
/// Option d'une liste deroulante de codes (client ou unite). Code null : entree
/// "tous" des filtres.
/// </summary>
public sealed record InvoiceCodeOption(string? Code, string Label);

/// <summary>
/// Ligne en cours de saisie dans la grille editable. Les montants sont conserves
/// sous forme de texte pour accepter la virgule comme le point pendant la frappe ;
/// la conversion et les controles de format ont lieu a l'enregistrement.
/// </summary>
public sealed class InvoiceLineEditorRow : INotifyPropertyChanged
{
    private string designation = string.Empty;
    private string quantityText = "1";
    private string unitPriceText = "0";
    private decimal vatRate = 19m;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Designation
    {
        get => designation;
        set => SetField(ref designation, value ?? string.Empty);
    }

    public string QuantityText
    {
        get => quantityText;
        set
        {
            if (SetField(ref quantityText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(LineTotalExclVatText));
            }
        }
    }

    public string UnitPriceText
    {
        get => unitPriceText;
        set
        {
            if (SetField(ref unitPriceText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(LineTotalExclVatText));
            }
        }
    }

    public decimal VatRate
    {
        get => vatRate;
        set => SetField(ref vatRate, value);
    }

    /// <summary>
    /// Total HT de la ligne, arrondi comme le domaine (2 decimales, demi vers le haut).
    /// Une saisie hors d'echelle (produit non representable) rend 0 : l'apercu reste
    /// affichable, et le controle de saisie refuse la ligne a l'enregistrement avec
    /// un message explicite.
    /// </summary>
    public decimal LineTotalExclVat
    {
        get
        {
            if (!TryGetQuantity(out var quantity) || !TryGetUnitPrice(out var unitPrice))
            {
                return 0m;
            }

            try
            {
                return Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
            }
            catch (OverflowException)
            {
                return 0m;
            }
        }
    }

    public decimal VatAmount => Math.Round(LineTotalExclVat * VatRate / 100m, 2, MidpointRounding.AwayFromZero);

    public string LineTotalExclVatText => LineTotalExclVat.ToString("N2", CultureInfo.CurrentCulture);

    public static InvoiceLineEditorRow FromResponse(InvoiceLineResponse line)
    {
        return new InvoiceLineEditorRow
        {
            Designation = line.Designation,
            QuantityText = line.Quantity.ToString("0.###", CultureInfo.CurrentCulture),
            UnitPriceText = line.UnitPrice.ToString("0.00", CultureInfo.CurrentCulture),
            VatRate = line.VatRate
        };
    }

    public bool TryGetQuantity(out decimal value) => TryParseNumber(quantityText, out value);

    public bool TryGetUnitPrice(out decimal value) => TryParseNumber(unitPriceText, out value);

    // Meme tolerance de saisie que les recettes journalieres : la virgule et le
    // point sont acceptes, quelle que soit la culture du poste.
    private static bool TryParseNumber(string text, out decimal value)
    {
        var trimmed = text.Trim();

        return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            || decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
