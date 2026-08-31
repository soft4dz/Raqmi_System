using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using RaqmiSystem.Application.Purchasing;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Purchasing;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module Achats &amp; approvisionnements (module 12). Trois sections independantes,
/// donc des sous-onglets : bons de commande (filtres, cycle de vie, saisie des
/// lignes), reception des marchandises et referentiel fournisseurs.
///
/// PERIMETRE HONNETE, dit a l'ecran : demandes d'achat, consultations / demandes de
/// prix et factures fournisseurs ne sont PAS couvertes par ce module.
///
/// Vue autonome : elle ne connait que le ModuleViewContext que la fenetre lui prete,
/// jamais MainWindow ni une autre vue. Les regles rappelees a l'ecran (numero alloue
/// a l'approbation, lignes figees ensuite, sur-reception refusee, annulation
/// impossible apres une premiere reception) sont le MIROIR des regles du serveur,
/// jamais leur remplacement : le refus fait autorite cote serveur.
///
/// Les cles de permission viennent de PermissionCatalog (PurchasingWrite / PurchasingApprove /
/// PurchasingReceive), la meme source que les policies de PurchasingEndpoints. La lecture
/// (PurchasingRead) est portee par l'onglet lui-meme, verrouille par ApplyModulePermissions.
/// </summary>
public partial class PurchasingView : UserControl
{
    private const string PurchasingWritePermission = PermissionCatalog.PurchasingWrite;

    private const string PurchasingApprovePermission = PermissionCatalog.PurchasingApprove;

    private const string PurchasingReceivePermission = PermissionCatalog.PurchasingReceive;

    private const string WritePermissionHint =
        "Permission purchasing.write requise : votre profil ne peut que consulter les achats.";

    private const string ApprovePermissionHint =
        "Permission purchasing.approve requise : approuver un bon de commande engage la dépense.";

    private const string ReceivePermissionHint =
        "Permission purchasing.receive requise : la réception est un geste de magasin, distinct de la saisie des achats.";

    // Libelles francais des valeurs de l'enum SupplierType : seul l'affichage est
    // traduit, la valeur envoyee a l'API reste celle du domaine.
    private static readonly SupplierTypeOption[] SupplierTypeOptions =
    [
        new(SupplierType.Company, "Entreprise"),
        new(SupplierType.Individual, "Particulier"),
        new(SupplierType.PublicEntity, "Organisme public")
    ];

    // Filtre de statut : la valeur nulle est l'entree "Tous".
    private static readonly PurchaseOrderStatusOption[] StatusFilterOptions =
    [
        new(null, "Tous les statuts"),
        new(PurchaseOrderStatus.Draft, "Brouillon"),
        new(PurchaseOrderStatus.Approved, "Approuvé"),
        new(PurchaseOrderStatus.PartiallyReceived, "Partiellement reçue"),
        new(PurchaseOrderStatus.Received, "Reçue"),
        new(PurchaseOrderStatus.Cancelled, "Annulée")
    ];

    private readonly ObservableCollection<PurchaseOrderLineEditorRow> editorLines = [];

    private readonly ObservableCollection<ReceptionLineRow> receptionLines = [];

    // Info-bulles d'origine des boutons d'action, capturees avant toute substitution
    // par un message de permission : l'affectation doit rester symetrique.
    private readonly Dictionary<Button, object?> originalToolTips = [];

    private ModuleViewContext? context;

    // Null en mode creation, identifiant du bon edite en mode modification.
    private Guid? editingOrderId;

    // Null en mode creation, code du fournisseur edite en mode modification.
    private string? editingSupplierCode;

    private bool canWritePurchasing = true;

    private bool canApprovePurchasing = true;

    private bool canReceivePurchasing = true;

    public PurchasingView()
    {
        InitializeComponent();

        // Les StringFormat du XAML suivent la culture de la vue : sans cela, une
        // grille afficherait les montants et les dates dans une culture differente
        // de celle utilisee par le code-behind.
        Language = XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);

        SupplierTypeComboBox.ItemsSource = SupplierTypeOptions;
        OrdersStatusComboBox.ItemsSource = StatusFilterOptions;
        OrdersStatusComboBox.SelectedIndex = 0;

        editorLines.CollectionChanged += EditorLines_CollectionChanged;
        EditorLinesDataGrid.ItemsSource = editorLines;

        receptionLines.CollectionChanged += ReceptionLines_CollectionChanged;
        ReceptionLinesDataGrid.ItemsSource = receptionLines;

        ResetSupplierForm();
        ResetOrderEditor();
        UpdateActionButtons();
    }

    /// <summary>Memorise le contexte fourni par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext context)
    {
        this.context = context;

        canWritePurchasing = context.HasPermission(PurchasingWritePermission);
        canApprovePurchasing = context.HasPermission(PurchasingApprovePermission);
        canReceivePurchasing = context.HasPermission(PurchasingReceivePermission);

        UpdateActionButtons();
    }

    /// <summary>
    /// (Re)charge fournisseurs et bons de commande. Appelee a la premiere ouverture
    /// de l'onglet et par les boutons Actualiser. Sort silencieusement tant qu'aucun
    /// contexte n'est disponible ou qu'aucune session n'est ouverte.
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
            await ReloadSuppliersAsync();
            await ReloadOrdersAsync();
        });
    }

    /// <summary>
    /// Vide grilles, formulaires et compteurs (appelee a la deconnexion). Les vues
    /// survivent a la deconnexion et resservent au profil suivant : tout etat pose
    /// pour un profil doit etre reversible.
    /// </summary>
    public void ResetState()
    {
        OrdersDataGrid.ItemsSource = null;
        ReceivableOrdersDataGrid.ItemsSource = null;
        SuppliersDataGrid.ItemsSource = null;

        OrdersFromDatePicker.SelectedDate = null;
        OrdersToDatePicker.SelectedDate = null;
        OrdersWarehouseTextBox.Text = string.Empty;
        OrdersStatusComboBox.SelectedIndex = 0;
        OrdersSupplierComboBox.ItemsSource = null;
        EditorSupplierComboBox.ItemsSource = null;
        CancelReasonTextBox.Text = string.Empty;
        OrderCountTextBlock.Text = string.Empty;

        SupplierSearchTextBox.Text = string.Empty;
        IncludeInactiveSuppliersCheckBox.IsChecked = false;
        SupplierCountTextBlock.Text = string.Empty;

        ClearReceptionLines();
        ReceptionOrderTitleTextBlock.Text = "Lignes à recevoir";
        ReceptionSummaryTextBlock.Text = "Sélectionnez une commande approuvée pour saisir une réception.";

        ResetSupplierForm();
        ResetOrderEditor();
        UpdateActionButtons();
    }

    // ====================== Chargements et rafraichissements ======================

    private async void RefreshAllButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await ReloadSuppliersAsync();
            await ReloadOrdersAsync();
            moduleContext.SetStatus("Achats actualisés.");
        });
    }

    private async void RefreshOrdersButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await ReloadOrdersAsync();
            moduleContext.SetStatus("Bons de commande actualisés.");
        });
    }

    private async void RefreshReceivableButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await ReloadOrdersAsync();
            moduleContext.SetStatus("Commandes à recevoir actualisées.");
        });
    }

    private async void RefreshSuppliersButton_Click(object sender, RoutedEventArgs e)
    {
        await ReloadSuppliersWithStatusAsync();
    }

    private async void IncludeInactiveSuppliersCheckBox_Click(object sender, RoutedEventArgs e)
    {
        await ReloadSuppliersWithStatusAsync();
    }

    private async void SupplierSearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await ReloadSuppliersWithStatusAsync();
    }

    private async Task ReloadSuppliersWithStatusAsync()
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await ReloadSuppliersAsync();
            moduleContext.SetStatus("Référentiel fournisseurs actualisé.");
        });
    }

    private async Task ReloadSuppliersAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var search = string.IsNullOrWhiteSpace(SupplierSearchTextBox.Text)
            ? null
            : SupplierSearchTextBox.Text.Trim();

        // La ligne selectionnee est identifiee par son code pour etre restauree apres
        // le rechargement : sans cela, activer ou desactiver un fournisseur fait
        // perdre la selection et l'operateur doit retrouver sa ligne a la main.
        var selectedCode = (SuppliersDataGrid.SelectedItem as SupplierRowView)?.Code;

        var suppliers = await moduleContext.ApiClient.GetSuppliersAsync(
            moduleContext.ApiBaseUrl,
            search,
            IncludeInactiveSuppliersCheckBox.IsChecked == true);

        var rows = suppliers.Select(ToRowView).ToArray();
        SuppliersDataGrid.ItemsSource = rows;

        SupplierCountTextBlock.Text = suppliers.Count == 1
            ? "1 fournisseur"
            : $"{suppliers.Count.ToString(CultureInfo.CurrentCulture)} fournisseurs";

        RestoreSupplierSelection(rows, selectedCode);
        RefreshSupplierChoices(suppliers);
        UpdateActionButtons();
    }

    /// <summary>
    /// Alimente les deux listes deroulantes de fournisseurs (filtre et formulaire) en
    /// conservant le choix courant quand il existe encore. Seuls les fournisseurs
    /// ACTIFS sont proposes a la commande : le serveur refuse une commande adressee a
    /// un fournisseur desactive, l'ecran n'en propose donc pas.
    /// </summary>
    private void RefreshSupplierChoices(IReadOnlyCollection<SupplierResponse> suppliers)
    {
        var previousFilter = (OrdersSupplierComboBox.SelectedItem as SupplierOption)?.Code;
        var previousEditor = (EditorSupplierComboBox.SelectedItem as SupplierOption)?.Code;

        var filterOptions = new List<SupplierOption> { new(null, "Tous les fournisseurs") };
        var editorOptions = new List<SupplierOption> { new(null, "Sélectionnez un fournisseur") };

        foreach (var supplier in suppliers.OrderBy(supplier => supplier.Code, StringComparer.OrdinalIgnoreCase))
        {
            filterOptions.Add(new SupplierOption(supplier.Code, $"{supplier.Code} — {supplier.Name}"));

            if (supplier.IsActive)
            {
                editorOptions.Add(new SupplierOption(supplier.Code, $"{supplier.Code} — {supplier.Name}"));
            }
        }

        OrdersSupplierComboBox.ItemsSource = filterOptions;
        EditorSupplierComboBox.ItemsSource = editorOptions;

        OrdersSupplierComboBox.SelectedItem = filterOptions.FirstOrDefault(option =>
            string.Equals(option.Code, previousFilter, StringComparison.OrdinalIgnoreCase)) ?? filterOptions[0];

        EditorSupplierComboBox.SelectedItem = editorOptions.FirstOrDefault(option =>
            string.Equals(option.Code, previousEditor, StringComparison.OrdinalIgnoreCase)) ?? editorOptions[0];
    }

    private async Task ReloadOrdersAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var selectedOrderId = (OrdersDataGrid.SelectedItem as PurchaseOrderRowView)?.Id;
        var selectedReceivableId = (ReceivableOrdersDataGrid.SelectedItem as PurchaseOrderRowView)?.Id;

        var orders = await moduleContext.ApiClient.GetPurchaseOrdersAsync(
            moduleContext.ApiBaseUrl,
            ToDateOnly(OrdersFromDatePicker.SelectedDate),
            ToDateOnly(OrdersToDatePicker.SelectedDate),
            (OrdersSupplierComboBox.SelectedItem as SupplierOption)?.Code,
            string.IsNullOrWhiteSpace(OrdersWarehouseTextBox.Text) ? null : OrdersWarehouseTextBox.Text.Trim(),
            (OrdersStatusComboBox.SelectedItem as PurchaseOrderStatusOption)?.Status?.ToString());

        var rows = orders.Select(ToRowView).ToArray();
        OrdersDataGrid.ItemsSource = rows;

        OrderCountTextBlock.Text = orders.Count == 1
            ? "1 bon de commande"
            : $"{orders.Count.ToString(CultureInfo.CurrentCulture)} bons de commande";

        // L'onglet Reception ne montre que les commandes reellement recevables : le
        // drapeau vient du serveur (CanReceive), il n'est pas redecide ici.
        var receivable = rows.Where(row => row.Source.CanReceive).ToArray();
        ReceivableOrdersDataGrid.ItemsSource = receivable;

        RestoreOrderSelection(OrdersDataGrid, rows, selectedOrderId);
        RestoreOrderSelection(ReceivableOrdersDataGrid, receivable, selectedReceivableId);

        UpdateActionButtons();
    }

    private static void RestoreOrderSelection(DataGrid grid, IReadOnlyList<PurchaseOrderRowView> rows, Guid? id)
    {
        if (id is not Guid orderId)
        {
            return;
        }

        var restored = rows.FirstOrDefault(row => row.Id == orderId);

        if (restored is null)
        {
            return;
        }

        grid.SelectedItem = restored;
        grid.ScrollIntoView(restored);
    }

    private void RestoreSupplierSelection(IReadOnlyList<SupplierRowView> rows, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        var restored = rows.FirstOrDefault(row => string.Equals(row.Code, code, StringComparison.OrdinalIgnoreCase));

        if (restored is null)
        {
            return;
        }

        SuppliersDataGrid.SelectedItem = restored;
        SuppliersDataGrid.ScrollIntoView(restored);
    }

    // ============================ Bons de commande ============================

    private void OrdersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OrdersDataGrid.SelectedItem is PurchaseOrderRowView selected)
        {
            LoadOrderIntoEditor(selected.Source);
        }

        UpdateActionButtons();
    }

    /// <summary>
    /// Selectionner une commande bascule le formulaire en modification. Une commande
    /// approuvee a ses lignes FIGEES cote serveur : la grille de saisie passe alors en
    /// consultation, et l'ecran le dit au lieu de laisser saisir puis refuser.
    /// </summary>
    private void LoadOrderIntoEditor(PurchaseOrderResponse order)
    {
        editingOrderId = order.Id;

        ClearEditorLines();

        foreach (var line in order.Lines)
        {
            editorLines.Add(PurchaseOrderLineEditorRow.FromResponse(line));
        }

        SelectSupplierOption(EditorSupplierComboBox, order.SupplierCode);
        EditorWarehouseTextBox.Text = order.WarehouseCode;
        EditorOrderDatePicker.SelectedDate = order.OrderDate.ToDateTime(TimeOnly.MinValue);

        var label = order.Number ?? "brouillon";
        EditorTitleTextBlock.Text = $"Bon de commande {label}";
        EditorModeTextBlock.Text = PurchaseOrderStatusDisplay.Describe(order.Status);

        EditorHintTextBlock.Text = order.CanEdit
            ? "Ce brouillon ne porte pas de numéro : le numéro définitif n'est alloué qu'à l'approbation, qui fige aussi les lignes."
            : "Les lignes de ce bon de commande sont figées depuis son approbation : elles ne peuvent plus être modifiées.";

        SaveOrderButton.Content = "Enregistrer les lignes";

        UpdateEditorTotals();
        UpdateActionButtons();
    }

    private void NewOrderButton_Click(object sender, RoutedEventArgs e)
    {
        OrdersDataGrid.SelectedItem = null;
        ResetOrderEditor();
        UpdateActionButtons();
        context?.SetStatus("Formulaire vidé : nouveau bon de commande.");
    }

    private void ResetOrderEditor()
    {
        editingOrderId = null;

        ClearEditorLines();

        EditorWarehouseTextBox.Text = string.Empty;
        EditorOrderDatePicker.SelectedDate = DateTime.Today;
        SelectSupplierPlaceholder(EditorSupplierComboBox);

        EditorTitleTextBlock.Text = "Nouveau bon de commande (brouillon)";
        EditorModeTextBlock.Text = "Nouveau bon de commande";
        EditorHintTextBlock.Text = "Un brouillon ne porte pas de numéro : le numéro définitif n'est alloué qu'à l'approbation, qui fige aussi les lignes.";
        SaveOrderButton.Content = "Créer le brouillon";

        UpdateEditorTotals();
    }

    private void AddLineButton_Click(object sender, RoutedEventArgs e)
    {
        editorLines.Add(new PurchaseOrderLineEditorRow());
        EditorLinesDataGrid.ScrollIntoView(editorLines[^1]);
    }

    private void RemoveLineButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PurchaseOrderLineEditorRow row })
        {
            editorLines.Remove(row);
        }
    }

    private async void SaveOrderButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            if (!TryBuildLines(moduleContext, out var lines))
            {
                return;
            }

            PurchaseOrderResponse saved;

            if (editingOrderId is Guid orderId)
            {
                saved = await moduleContext.ApiClient.UpdatePurchaseOrderLinesAsync(
                    moduleContext.ApiBaseUrl,
                    orderId,
                    new UpdatePurchaseOrderLinesRequest(lines));

                moduleContext.SetStatus(lines.Count == 1
                    ? "Lignes du brouillon mises à jour (1 ligne)."
                    : $"Lignes du brouillon mises à jour ({lines.Count.ToString(CultureInfo.CurrentCulture)} lignes).");
            }
            else
            {
                var supplierCode = (EditorSupplierComboBox.SelectedItem as SupplierOption)?.Code;

                if (string.IsNullOrWhiteSpace(supplierCode))
                {
                    moduleContext.SetStatus("Sélectionnez le fournisseur du bon de commande.", isError: true);
                    return;
                }

                var warehouseCode = EditorWarehouseTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(warehouseCode))
                {
                    moduleContext.SetStatus("Le dépôt de livraison est obligatoire.", isError: true);
                    return;
                }

                if (EditorOrderDatePicker.SelectedDate is not DateTime orderDate)
                {
                    moduleContext.SetStatus("La date de commande est obligatoire.", isError: true);
                    return;
                }

                saved = await moduleContext.ApiClient.CreatePurchaseOrderAsync(
                    moduleContext.ApiBaseUrl,
                    new CreatePurchaseOrderRequest(
                        supplierCode,
                        warehouseCode,
                        DateOnly.FromDateTime(orderDate),
                        lines));

                moduleContext.SetStatus("Bon de commande créé en brouillon : sans numéro, modifiable jusqu'à son approbation.");
            }

            ResetOrderEditor();
            await ReloadOrdersAsync();
            SelectOrder(saved.Id);
        });
    }

    /// <summary>
    /// Controle de saisie aligne sur les regles du domaine (PurchaseOrderLine) : au
    /// moins une ligne, code article et designation requis, quantite strictement
    /// positive a 3 decimales au plus, prix unitaire positif ou nul a 2 decimales au
    /// plus. Les bornes ne sont pas recopiees pour remplacer le serveur : elles
    /// evitent un aller-retour previsible.
    /// </summary>
    private bool TryBuildLines(ModuleViewContext moduleContext, out IReadOnlyCollection<PurchaseOrderLineRequest> lines)
    {
        lines = Array.Empty<PurchaseOrderLineRequest>();

        if (editorLines.Count == 0)
        {
            moduleContext.SetStatus("Ajoutez au moins une ligne au bon de commande.", isError: true);
            return false;
        }

        var result = new List<PurchaseOrderLineRequest>(editorLines.Count);
        var lineNumber = 1;

        foreach (var row in editorLines)
        {
            var itemCode = row.ItemCode.Trim();
            var designation = row.Designation.Trim();

            if (string.IsNullOrWhiteSpace(itemCode))
            {
                moduleContext.SetStatus($"Ligne {lineNumber} : le code article est obligatoire.", isError: true);
                return false;
            }

            if (string.IsNullOrWhiteSpace(designation))
            {
                moduleContext.SetStatus($"Ligne {lineNumber} : la désignation est obligatoire.", isError: true);
                return false;
            }

            if (!row.TryGetQuantity(out var quantity) || quantity <= 0m)
            {
                moduleContext.SetStatus($"Ligne {lineNumber} : la quantité doit être strictement positive.", isError: true);
                return false;
            }

            if (decimal.Round(quantity, 3) != quantity)
            {
                moduleContext.SetStatus($"Ligne {lineNumber} : la quantité admet 3 décimales au maximum.", isError: true);
                return false;
            }

            if (!row.TryGetUnitPrice(out var unitPrice) || unitPrice < 0m)
            {
                moduleContext.SetStatus($"Ligne {lineNumber} : le prix unitaire doit être positif ou nul.", isError: true);
                return false;
            }

            if (decimal.Round(unitPrice, 2) != unitPrice)
            {
                moduleContext.SetStatus($"Ligne {lineNumber} : le prix unitaire admet 2 décimales au maximum.", isError: true);
                return false;
            }

            // Capacite de la colonne numeric(18,2) : le produit est verifie par
            // division pour ne pas dependre d'une exception d'overflow.
            if (unitPrice != 0m && quantity > decimal.MaxValue / unitPrice)
            {
                moduleContext.SetStatus($"Ligne {lineNumber} : le total dépasse la capacité de la colonne.", isError: true);
                return false;
            }

            result.Add(new PurchaseOrderLineRequest(itemCode, designation, quantity, unitPrice));
            lineNumber++;
        }

        lines = result;
        return true;
    }

    private async void ApproveOrderButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        if (OrdersDataGrid.SelectedItem is not PurchaseOrderRowView selected)
        {
            moduleContext.SetStatus("Sélectionnez un bon de commande.", isError: true);
            return;
        }

        var confirmed = Confirm(
            $"Approuver le bon de commande du {selected.OrderDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)} " +
            $"pour {selected.SupplierLabel} ?\n" +
            $"Total HT : {selected.TotalExclVat.ToString("N2", CultureInfo.CurrentCulture)}.\n\n" +
            "L'approbation engage la dépense : un numéro définitif est alloué et les lignes deviennent définitivement non modifiables.",
            "Approuver le bon de commande");

        if (!confirmed)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var approved = await moduleContext.ApiClient.ApprovePurchaseOrderAsync(
                moduleContext.ApiBaseUrl,
                selected.Id);

            await ReloadOrdersAsync();
            SelectOrder(approved.Id);

            moduleContext.SetStatus($"Bon de commande {approved.Number} approuvé.");
        });
    }

    private async void CancelOrderButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        if (OrdersDataGrid.SelectedItem is not PurchaseOrderRowView selected)
        {
            moduleContext.SetStatus("Sélectionnez un bon de commande.", isError: true);
            return;
        }

        var reason = CancelReasonTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(reason))
        {
            moduleContext.SetStatus("Le motif d'annulation est obligatoire.", isError: true);
            return;
        }

        var confirmed = Confirm(
            $"Annuler le bon de commande {selected.NumberLabel} ({selected.SupplierLabel}) ?\n" +
            $"Motif : {reason}\n\n" +
            "L'annulation est définitive et sera tracée. Elle devient impossible dès la première réception.",
            "Annuler le bon de commande");

        if (!confirmed)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var cancelled = await moduleContext.ApiClient.CancelPurchaseOrderAsync(
                moduleContext.ApiBaseUrl,
                selected.Id,
                new CancelPurchaseOrderRequest(reason));

            CancelReasonTextBox.Text = string.Empty;

            await ReloadOrdersAsync();
            SelectOrder(cancelled.Id);

            moduleContext.SetStatus($"Bon de commande {selected.NumberLabel} annulé.");
        });
    }

    private void SelectOrder(Guid id)
    {
        if (OrdersDataGrid.ItemsSource is not IEnumerable<PurchaseOrderRowView> rows)
        {
            return;
        }

        var row = rows.FirstOrDefault(candidate => candidate.Id == id);

        if (row is null)
        {
            return;
        }

        OrdersDataGrid.SelectedItem = row;
        OrdersDataGrid.ScrollIntoView(row);
    }

    // ================================ Reception ================================

    private void ReceivableOrdersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ClearReceptionLines();

        if (ReceivableOrdersDataGrid.SelectedItem is PurchaseOrderRowView selected)
        {
            // Une ligne deja entierement recue n'a plus rien a recevoir : elle n'est
            // pas proposee a la saisie.
            foreach (var line in selected.Source.Lines.Where(line => line.RemainingQuantity > 0m))
            {
                receptionLines.Add(new ReceptionLineRow(line));
            }

            ReceptionOrderTitleTextBlock.Text = $"Lignes à recevoir — {selected.NumberLabel}";
        }
        else
        {
            ReceptionOrderTitleTextBlock.Text = "Lignes à recevoir";
        }

        UpdateReceptionSummary();
        UpdateActionButtons();
    }

    private async void RegisterReceiptButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        if (ReceivableOrdersDataGrid.SelectedItem is not PurchaseOrderRowView selected)
        {
            moduleContext.SetStatus("Sélectionnez une commande à recevoir.", isError: true);
            return;
        }

        if (!TryBuildReceiptLines(moduleContext, out var lines))
        {
            return;
        }

        // La confirmation dit ce que la reception PRODUIT : des entrees en stock
        // reelles, dans le depot de la commande. C'est aussi le point de non-retour
        // pour l'annulation du bon.
        var confirmed = Confirm(
            $"Enregistrer la réception du bon de commande {selected.NumberLabel} ({selected.SupplierLabel}) ?\n" +
            $"{DescribeReceiptLines(lines.Count)} pour un total de {FormatQuantity(lines.Sum(line => line.Quantity))} unité(s).\n\n" +
            $"Une entrée en stock sera générée dans le dépôt {selected.WarehouseCode}, au prix unitaire commandé. " +
            "Après cette réception, le bon de commande ne pourra plus être annulé.",
            "Enregistrer la réception");

        if (!confirmed)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var received = await moduleContext.ApiClient.ReceivePurchaseOrderAsync(
                moduleContext.ApiBaseUrl,
                selected.Id,
                new ReceivePurchaseOrderRequest(lines));

            await ReloadOrdersAsync();
            SelectOrder(received.Id);

            moduleContext.SetStatus(
                $"Réception enregistrée pour {received.Number} — {PurchaseOrderStatusDisplay.Describe(received.Status)}.");
        });
    }

    /// <summary>
    /// Construit les lignes de la livraison : seules les lignes portant une quantite
    /// saisie sont transmises. Les bornes verifiees ici (quantite strictement
    /// positive, 3 decimales au plus, jamais plus que le restant) sont le miroir des
    /// regles du serveur, qui refuse de toute facon la sur-reception.
    /// </summary>
    private bool TryBuildReceiptLines(
        ModuleViewContext moduleContext,
        out IReadOnlyCollection<ReceivePurchaseOrderLineRequest> lines)
    {
        lines = Array.Empty<ReceivePurchaseOrderLineRequest>();

        var result = new List<ReceivePurchaseOrderLineRequest>();

        foreach (var row in receptionLines)
        {
            if (string.IsNullOrWhiteSpace(row.QuantityText))
            {
                continue;
            }

            if (!row.TryGetQuantity(out var quantity) || quantity <= 0m)
            {
                moduleContext.SetStatus(
                    $"Article {row.ItemCode} : la quantité reçue doit être strictement positive.",
                    isError: true);
                return false;
            }

            if (decimal.Round(quantity, 3) != quantity)
            {
                moduleContext.SetStatus(
                    $"Article {row.ItemCode} : la quantité admet 3 décimales au maximum.",
                    isError: true);
                return false;
            }

            if (quantity > row.Remaining)
            {
                moduleContext.SetStatus(
                    $"Article {row.ItemCode} : il ne reste que {FormatQuantity(row.Remaining)} à recevoir.",
                    isError: true);
                return false;
            }

            result.Add(new ReceivePurchaseOrderLineRequest(
                row.LineId,
                quantity,
                string.IsNullOrWhiteSpace(row.LotNumber) ? null : row.LotNumber.Trim(),
                row.ExpiryDate is DateTime expiry ? DateOnly.FromDateTime(expiry) : null));
        }

        if (result.Count == 0)
        {
            moduleContext.SetStatus("Saisissez au moins une quantité reçue.", isError: true);
            return false;
        }

        lines = result;
        return true;
    }

    private void ClearReceptionLines()
    {
        foreach (var row in receptionLines)
        {
            row.PropertyChanged -= ReceptionLine_PropertyChanged;
        }

        receptionLines.Clear();
    }

    private void ReceptionLines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ReceptionLineRow row in e.OldItems)
            {
                row.PropertyChanged -= ReceptionLine_PropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (ReceptionLineRow row in e.NewItems)
            {
                row.PropertyChanged += ReceptionLine_PropertyChanged;
            }
        }

        UpdateReceptionSummary();
    }

    private void ReceptionLine_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateReceptionSummary();
    }

    private void UpdateReceptionSummary()
    {
        if (ReceivableOrdersDataGrid.SelectedItem is not PurchaseOrderRowView selected)
        {
            ReceptionSummaryTextBlock.Text = "Sélectionnez une commande approuvée pour saisir une réception.";
            return;
        }

        var filled = receptionLines.Count(row =>
            !string.IsNullOrWhiteSpace(row.QuantityText) && row.TryGetQuantity(out var quantity) && quantity > 0m);

        ReceptionSummaryTextBlock.Text = filled == 0
            ? $"Commande {selected.NumberLabel} — saisissez les quantités livrées : l'entrée en stock sera générée dans le dépôt {selected.WarehouseCode}."
            : $"Commande {selected.NumberLabel} — {DescribeReceiptLines(filled)} à recevoir, entrée en stock dans le dépôt {selected.WarehouseCode}.";
    }

    private static string DescribeReceiptLines(int count)
    {
        return count == 1 ? "1 ligne" : $"{count.ToString(CultureInfo.CurrentCulture)} lignes";
    }

    // =============================== Fournisseurs ===============================

    private void SuppliersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateActionButtons();

        if (SuppliersDataGrid.SelectedItem is not SupplierRowView selected)
        {
            return;
        }

        // Selectionner une ligne bascule le formulaire en modification : le code
        // identifie la fiche cote API, il n'est donc plus modifiable.
        var supplier = selected.Source;

        editingSupplierCode = supplier.Code;
        SupplierFormTitleTextBlock.Text = $"Modifier {supplier.Code}";
        SupplierCodeTextBox.Text = supplier.Code;
        SupplierCodeTextBox.IsEnabled = false;
        SupplierNameTextBox.Text = supplier.Name;
        SupplierTypeComboBox.SelectedValue = supplier.SupplierType;
        SupplierNifTextBox.Text = supplier.Nif ?? string.Empty;
        SupplierRcTextBox.Text = supplier.Rc ?? string.Empty;
        SupplierAiTextBox.Text = supplier.Ai ?? string.Empty;
        SupplierNisTextBox.Text = supplier.Nis ?? string.Empty;
        SupplierAddressTextBox.Text = supplier.Address ?? string.Empty;
        SupplierCityTextBox.Text = supplier.City ?? string.Empty;
        SupplierPhoneTextBox.Text = supplier.Phone ?? string.Empty;
        SupplierEmailTextBox.Text = supplier.Email ?? string.Empty;
        SaveSupplierButton.Content = "Enregistrer les modifications";
    }

    private void NewSupplierButton_Click(object sender, RoutedEventArgs e)
    {
        ResetSupplierForm();
        UpdateActionButtons();
    }

    private void SupplierTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplySupplierTypeToForm();
    }

    // NIF, RC, AI et NIS identifient une entite immatriculee : ils n'ont pas de sens
    // pour un particulier. Le bloc entier disparait dans ce cas (avec son espacement,
    // pour ne pas laisser un trou) et les champs sont vides, afin qu'aucune valeur
    // saisie avant le changement de type ne subsiste a l'ecran - meme regle que la
    // fiche client.
    private void ApplySupplierTypeToForm()
    {
        // Le bloc fiscal est declare apres la liste deroulante dans le XAML : si
        // l'evenement se declenchait pendant le chargement de la vue, les champs ne
        // seraient pas encore construits.
        if (FiscalIdentifiersGrid is null)
        {
            return;
        }

        var isIndividual = SupplierTypeComboBox.SelectedValue is SupplierType.Individual;

        FiscalIdentifiersGrid.Visibility = isIndividual ? Visibility.Collapsed : Visibility.Visible;
        FiscalSpacerRow.Height = isIndividual ? new GridLength(0) : new GridLength(12);
        IndividualHintTextBlock.Visibility = isIndividual ? Visibility.Visible : Visibility.Collapsed;

        if (isIndividual)
        {
            SupplierNifTextBox.Text = string.Empty;
            SupplierRcTextBox.Text = string.Empty;
            SupplierAiTextBox.Text = string.Empty;
            SupplierNisTextBox.Text = string.Empty;
        }

        SupplierNameTextBox.Tag = isIndividual ? "Nom et prénom du fournisseur" : "Raison sociale";
    }

    private void ResetSupplierForm()
    {
        editingSupplierCode = null;
        SupplierFormTitleTextBlock.Text = "Nouveau fournisseur";
        SupplierCodeTextBox.Text = string.Empty;
        SupplierCodeTextBox.IsEnabled = true;
        SupplierNameTextBox.Text = string.Empty;
        SupplierTypeComboBox.SelectedValue = SupplierType.Company;
        SupplierNifTextBox.Text = string.Empty;
        SupplierRcTextBox.Text = string.Empty;
        SupplierAiTextBox.Text = string.Empty;
        SupplierNisTextBox.Text = string.Empty;
        SupplierAddressTextBox.Text = string.Empty;
        SupplierCityTextBox.Text = string.Empty;
        SupplierPhoneTextBox.Text = string.Empty;
        SupplierEmailTextBox.Text = string.Empty;
        SaveSupplierButton.Content = "Créer le fournisseur";
        SuppliersDataGrid.SelectedItem = null;
    }

    private async void SaveSupplierButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var code = SupplierCodeTextBox.Text.Trim();
            var name = SupplierNameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            {
                moduleContext.SetStatus("Le code et le nom du fournisseur sont requis.", isError: true);
                return;
            }

            if (SupplierTypeComboBox.SelectedValue is not SupplierType supplierType)
            {
                moduleContext.SetStatus("Sélectionnez un type de fournisseur.", isError: true);
                return;
            }

            var isIndividual = supplierType == SupplierType.Individual;

            string? nif = null;

            // Regle du domaine, partagee avec la fiche client (Customer.NormalizeNif) :
            // le NIF, s'il est renseigne, fait exactement 15 chiffres.
            if (!isIndividual && !TryReadNif(out nif))
            {
                moduleContext.SetStatus("Le NIF doit comporter exactement 15 chiffres.", isError: true);
                return;
            }

            if (!TryReadEmail(out var email))
            {
                moduleContext.SetStatus("L'adresse de courriel est invalide.", isError: true);
                return;
            }

            var rc = isIndividual ? null : ReadOptional(SupplierRcTextBox);
            var ai = isIndividual ? null : ReadOptional(SupplierAiTextBox);
            var nis = isIndividual ? null : ReadOptional(SupplierNisTextBox);
            var address = ReadOptional(SupplierAddressTextBox);
            var city = ReadOptional(SupplierCityTextBox);
            var phone = ReadOptional(SupplierPhoneTextBox);

            var existingCode = editingSupplierCode;

            // Le serveur normalise le code (majuscules) : c'est la valeur qu'il renvoie
            // qui est affichee et reutilisee, jamais la saisie brute.
            if (existingCode is null)
            {
                var created = await moduleContext.ApiClient.CreateSupplierAsync(
                    moduleContext.ApiBaseUrl,
                    new CreateSupplierRequest(code, name, supplierType, nif, rc, ai, nis, address, city, phone, email));

                moduleContext.SetStatus($"Fournisseur {created.Code} créé.");
            }
            else
            {
                var updated = await moduleContext.ApiClient.UpdateSupplierAsync(
                    moduleContext.ApiBaseUrl,
                    existingCode,
                    new UpdateSupplierRequest(name, supplierType, nif, rc, ai, nis, address, city, phone, email));

                moduleContext.SetStatus($"Fournisseur {updated.Code} mis à jour.");
            }

            ResetSupplierForm();
            await ReloadSuppliersAsync();
        });
    }

    private async void ActivateSupplierButton_Click(object sender, RoutedEventArgs e)
    {
        await SetSupplierActiveAsync(isActive: true);
    }

    private async void DeactivateSupplierButton_Click(object sender, RoutedEventArgs e)
    {
        await SetSupplierActiveAsync(isActive: false);
    }

    private async Task SetSupplierActiveAsync(bool isActive)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        if (SuppliersDataGrid.SelectedItem is not SupplierRowView selected)
        {
            moduleContext.SetStatus("Sélectionnez un fournisseur.", isError: true);
            return;
        }

        var question = isActive
            ? $"Réactiver le fournisseur {selected.Code} ({selected.Name}) ?\nIl sera de nouveau proposé aux bons de commande."
            : $"Désactiver le fournisseur {selected.Code} ({selected.Name}) ?\nIl ne sera plus proposé aux bons de commande, et une commande ne pourra plus lui être approuvée.";

        var confirmed = Confirm(question, isActive ? "Activer le fournisseur" : "Désactiver le fournisseur");

        if (!confirmed)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var changed = await moduleContext.ApiClient.SetSupplierActiveAsync(
                moduleContext.ApiBaseUrl,
                selected.Code,
                isActive);

            await ReloadSuppliersAsync();

            moduleContext.SetStatus(isActive
                ? $"Fournisseur {changed.Code} activé."
                : $"Fournisseur {changed.Code} désactivé.");
        });
    }

    // ============================ Etat des actions ============================

    /// <summary>
    /// Les actions sont conditionnees a la fois au STATUT de l'objet selectionne et
    /// aux PERMISSIONS du profil. Approuver est un droit distinct d'ecrire (il engage
    /// la depense) et recevoir en est un troisieme (geste de magasin) : les trois sont
    /// releves separement.
    /// </summary>
    private void UpdateActionButtons()
    {
        var selectedOrder = (OrdersDataGrid.SelectedItem as PurchaseOrderRowView)?.Source;
        var selectedReceivable = (ReceivableOrdersDataGrid.SelectedItem as PurchaseOrderRowView)?.Source;
        var selectedSupplier = SuppliersDataGrid.SelectedItem as SupplierRowView;

        // Un bon approuve a ses lignes figees cote serveur : la grille de saisie passe
        // en consultation plutot que de laisser saisir une modification refusee.
        var canEditLines = selectedOrder is null || selectedOrder.CanEdit;

        EditorLinesDataGrid.IsEnabled = canEditLines;
        EditorSupplierComboBox.IsEnabled = canEditLines && editingOrderId is null;
        EditorWarehouseTextBox.IsEnabled = canEditLines && editingOrderId is null;
        EditorOrderDatePicker.IsEnabled = canEditLines && editingOrderId is null;

        AddLineButton.IsEnabled = canWritePurchasing && canEditLines;
        SaveOrderButton.IsEnabled = canWritePurchasing && canEditLines;

        ApproveOrderButton.IsEnabled = canApprovePurchasing
            && selectedOrder is { Status: PurchaseOrderStatus.Draft };

        // Annulation possible tant que RIEN n'a ete recu : le serveur refuse au-dela,
        // l'ecran ne propose donc pas le geste.
        CancelOrderButton.IsEnabled = canWritePurchasing
            && selectedOrder is not null
            && selectedOrder.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Approved
            && selectedOrder.TotalQuantityReceived == 0m;

        RegisterReceiptButton.IsEnabled = canReceivePurchasing && selectedReceivable is not null;

        SaveSupplierButton.IsEnabled = canWritePurchasing;
        ActivateSupplierButton.IsEnabled = canWritePurchasing && selectedSupplier is { IsActive: false };
        DeactivateSupplierButton.IsEnabled = canWritePurchasing && selectedSupplier is { IsActive: true };

        ApplyPermissionHint(AddLineButton, canWritePurchasing, WritePermissionHint);
        ApplyPermissionHint(SaveOrderButton, canWritePurchasing, WritePermissionHint);
        ApplyPermissionHint(CancelOrderButton, canWritePurchasing, WritePermissionHint);
        ApplyPermissionHint(ApproveOrderButton, canApprovePurchasing, ApprovePermissionHint);
        ApplyPermissionHint(RegisterReceiptButton, canReceivePurchasing, ReceivePermissionHint);
        ApplyPermissionHint(SaveSupplierButton, canWritePurchasing, WritePermissionHint);
        ApplyPermissionHint(ActivateSupplierButton, canWritePurchasing, WritePermissionHint);
        ApplyPermissionHint(DeactivateSupplierButton, canWritePurchasing, WritePermissionHint);
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

    // ================================= Outils =================================

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
            foreach (PurchaseOrderLineEditorRow row in e.OldItems)
            {
                row.PropertyChanged -= EditorLine_PropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (PurchaseOrderLineEditorRow row in e.NewItems)
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
    /// Apercu local du total pendant la saisie, explicitement non contractuel. La
    /// source de verite reste le serveur, qui renvoie le total definitif a
    /// l'enregistrement.
    /// </summary>
    private void UpdateEditorTotals()
    {
        var total = 0m;

        foreach (var row in editorLines)
        {
            total += row.LineTotalExclVat;
        }

        EditorTotalExclVatTextBlock.Text = total.ToString("N2", CultureInfo.CurrentCulture);
    }

    private static void SelectSupplierOption(ComboBox comboBox, string code)
    {
        if (comboBox.ItemsSource is IEnumerable<SupplierOption> options)
        {
            comboBox.SelectedItem = options.FirstOrDefault(option =>
                string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void SelectSupplierPlaceholder(ComboBox comboBox)
    {
        if (comboBox.ItemsSource is IEnumerable<SupplierOption> options)
        {
            comboBox.SelectedItem = options.FirstOrDefault(option => option.Code is null);
        }
    }

    // Gabarit de confirmation des actes engageants : fenetre proprietaire, icone
    // d'avertissement, defaut sur Non - la touche Entree ne suffit jamais a engager
    // l'action.
    private bool Confirm(string message, string caption)
    {
        var owner = Window.GetWindow(this);

        var result = owner is null
            ? MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(owner, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    private bool TryReadNif(out string? nif)
    {
        nif = ReadOptional(SupplierNifTextBox);

        return nif is null || (nif.Length == 15 && nif.All(char.IsAsciiDigit));
    }

    private bool TryReadEmail(out string? email)
    {
        email = ReadOptional(SupplierEmailTextBox);

        if (email is null)
        {
            return true;
        }

        var atIndex = email.IndexOf('@');

        return atIndex > 0 && atIndex < email.Length - 1;
    }

    private static string? ReadOptional(TextBox textBox)
    {
        var value = textBox.Text.Trim();

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static DateOnly? ToDateOnly(DateTime? value)
    {
        return value.HasValue ? DateOnly.FromDateTime(value.Value) : null;
    }

    /// <summary>
    /// Quantites : le domaine admet 3 decimales, mais un entier doit s'afficher en
    /// entier ("12/20 reçus" et non "12,000/20,000"). Le format "0.###" rend les deux.
    /// </summary>
    internal static string FormatQuantity(decimal value)
    {
        return value.ToString("0.###", CultureInfo.CurrentCulture);
    }

    // Projection d'affichage d'un bon de commande. Les chiffres viennent du serveur :
    // l'avancement utilise les totaux renvoyes par l'API, jamais une somme locale.
    private static PurchaseOrderRowView ToRowView(PurchaseOrderResponse order)
    {
        return new PurchaseOrderRowView(
            order,
            order.Id,
            order.Number ?? "—",
            order.SupplierName is null ? order.SupplierCode : $"{order.SupplierCode} — {order.SupplierName}",
            order.WarehouseCode,
            order.OrderDate,
            order.Status,
            PurchaseOrderStatusDisplay.Describe(order.Status),
            $"{FormatQuantity(order.TotalQuantityReceived)}/{FormatQuantity(order.TotalQuantityOrdered)} reçus",
            order.TotalExclVat);
    }

    private static SupplierRowView ToRowView(SupplierResponse supplier)
    {
        return new SupplierRowView(
            supplier,
            supplier.Code,
            supplier.Name,
            DescribeSupplierType(supplier.SupplierType),
            supplier.Nif,
            supplier.City,
            supplier.Phone,
            supplier.IsActive);
    }

    private static string DescribeSupplierType(SupplierType supplierType)
    {
        var option = SupplierTypeOptions.FirstOrDefault(item => item.Value == supplierType);

        return option?.Label ?? supplierType.ToString();
    }

    private sealed record SupplierTypeOption(SupplierType Value, string Label);

    private sealed record SupplierOption(string? Code, string Label);

    private sealed record PurchaseOrderStatusOption(PurchaseOrderStatus? Status, string Label);

    private sealed record PurchaseOrderRowView(
        PurchaseOrderResponse Source,
        Guid Id,
        string Number,
        string SupplierLabel,
        string WarehouseCode,
        DateOnly OrderDate,
        PurchaseOrderStatus Status,
        string StatusLabel,
        string ProgressLabel,
        decimal TotalExclVat)
    {
        /// <summary>Numero, ou la mention "brouillon" tant qu'il n'est pas alloue.</summary>
        public string NumberLabel => Number == "—" ? "brouillon" : Number;
    }

    private sealed record SupplierRowView(
        SupplierResponse Source,
        string Code,
        string Name,
        string SupplierTypeLabel,
        string? Nif,
        string? City,
        string? Phone,
        bool IsActive);
}

/// <summary>
/// Libelles francais des statuts de bon de commande : source UNIQUE pour la grille,
/// le filtre, les messages d'etat et les confirmations - le meme mot partout.
/// </summary>
public static class PurchaseOrderStatusDisplay
{
    public static string Describe(PurchaseOrderStatus status)
    {
        return status switch
        {
            PurchaseOrderStatus.Draft => "Brouillon",
            PurchaseOrderStatus.Approved => "Approuvé",
            PurchaseOrderStatus.PartiallyReceived => "Partiellement reçue",
            PurchaseOrderStatus.Received => "Reçue",
            PurchaseOrderStatus.Cancelled => "Annulée",
            _ => status.ToString()
        };
    }
}

/// <summary>
/// Ligne en cours de saisie dans la grille des lignes d'un bon de commande. Les
/// nombres sont conserves sous forme de texte pour accepter la virgule comme le point
/// pendant la frappe ; la conversion et les controles de format ont lieu a
/// l'enregistrement.
/// </summary>
public sealed class PurchaseOrderLineEditorRow : INotifyPropertyChanged
{
    private string itemCode = string.Empty;

    private string designation = string.Empty;

    private string quantityText = "1";

    private string unitPriceText = "0";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ItemCode
    {
        get => itemCode;
        set => SetField(ref itemCode, value ?? string.Empty);
    }

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

    /// <summary>
    /// Total HT de la ligne, arrondi comme le domaine (2 decimales, demi vers le
    /// haut). Une saisie hors d'echelle rend 0 : l'apercu reste affichable, et le
    /// controle de saisie refuse la ligne a l'enregistrement avec un message explicite.
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

    public string LineTotalExclVatText => LineTotalExclVat.ToString("N2", CultureInfo.CurrentCulture);

    public static PurchaseOrderLineEditorRow FromResponse(PurchaseOrderLineResponse line)
    {
        return new PurchaseOrderLineEditorRow
        {
            ItemCode = line.ItemCode,
            Designation = line.Designation,
            QuantityText = line.Quantity.ToString("0.###", CultureInfo.CurrentCulture),
            UnitPriceText = line.UnitPrice.ToString("0.00", CultureInfo.CurrentCulture)
        };
    }

    public bool TryGetQuantity(out decimal value) => TryParseNumber(quantityText, out value);

    public bool TryGetUnitPrice(out decimal value) => TryParseNumber(unitPriceText, out value);

    // La virgule et le point sont acceptes, quelle que soit la culture du poste.
    internal static bool TryParseNumber(string text, out decimal value)
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Ligne de saisie d'une reception : les quantites commandee, deja recue et restante
/// viennent du serveur (lecture seule) ; seules la quantite recue MAINTENANT, le
/// numero de lot et la date de peremption sont saisis.
/// </summary>
public sealed class ReceptionLineRow : INotifyPropertyChanged
{
    private string quantityText = string.Empty;

    private string lotNumber = string.Empty;

    private DateTime? expiryDate;

    public ReceptionLineRow(PurchaseOrderLineResponse line)
    {
        LineId = line.Id;
        ItemCode = line.ItemCode;
        Designation = line.Designation;
        Ordered = line.Quantity;
        Received = line.QuantityReceived;
        Remaining = line.RemainingQuantity;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid LineId { get; }

    public string ItemCode { get; }

    public string Designation { get; }

    public decimal Ordered { get; }

    public decimal Received { get; }

    public decimal Remaining { get; }

    public string OrderedText => PurchasingView.FormatQuantity(Ordered);

    public string ReceivedText => PurchasingView.FormatQuantity(Received);

    public string RemainingText => PurchasingView.FormatQuantity(Remaining);

    public string QuantityText
    {
        get => quantityText;
        set => SetField(ref quantityText, value ?? string.Empty);
    }

    public string LotNumber
    {
        get => lotNumber;
        set => SetField(ref lotNumber, value ?? string.Empty);
    }

    public DateTime? ExpiryDate
    {
        get => expiryDate;
        set => SetField(ref expiryDate, value);
    }

    public bool TryGetQuantity(out decimal value) =>
        PurchaseOrderLineEditorRow.TryParseNumber(quantityText, out value);

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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
