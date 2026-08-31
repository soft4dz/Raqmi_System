using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using RaqmiSystem.Application.Inventory;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module Stocks &amp; consommations (11), reparti sur quatre onglets internes : stock courant
/// valorise, registre des mouvements, administration des articles et des magasins, inventaires
/// physiques.
///
/// Vue autonome : elle ne connait ni MainWindow ni les autres vues, tout passe par le
/// ModuleViewContext recu dans Initialize (client API, URL, message d'etat, execution d'un appel
/// avec curseur d'attente).
///
/// DOCTRINE RAPPELEE A L'ECRAN, jamais reinventee ici : le stock est la SOMME du registre des
/// mouvements, calculee par le serveur ; aucun total ni aucune quantite n'est recalcule
/// localement. Les regles affichees (quantite strictement positive, sortie impossible en dessous
/// de zero, transfert entre deux magasins distincts, inventaire valide immuable) sont le MIROIR
/// des regles du serveur, jamais leur remplacement : le refus fait autorite cote serveur.
///
/// Les trois cles de permission viennent de PermissionCatalog (InventoryRead / InventoryWrite /
/// InventoryValidate), la meme source que les policies d'InventoryEndpoints : l'ecran et le
/// serveur ne peuvent pas diverger sur le nom d'un droit.
/// </summary>
public partial class InventoryView : UserControl
{
    private const string InventoryRead = PermissionCatalog.InventoryRead;

    private const string InventoryWrite = PermissionCatalog.InventoryWrite;

    private const string InventoryValidate = PermissionCatalog.InventoryValidate;

    private const string WritePermissionHint =
        "Permission requise : inventory.write. Votre profil ne peut que consulter les stocks.";

    private const string ValidatePermissionHint =
        "Permission requise : inventory.validate. Valider un inventaire génère des ajustements de " +
        "stock : ce droit est distinct de la saisie quotidienne.";

    private ModuleViewContext? context;

    // Info-bulles d'origine des boutons d'ecriture, capturees avant toute substitution. Les vues
    // de module survivent a la deconnexion et resservent au profil suivant sur les memes
    // instances : un message "permission requise" pose pour un profil doit disparaitre pour le
    // profil suivant, sinon il persiste a tort pour un utilisateur qui a le droit.
    private readonly Dictionary<Button, object?> originalToolTips = [];

    // Comptages en cours de saisie pour l'inventaire selectionne. Tenus en memoire jusqu'a
    // "Enregistrer le comptage", qui les envoie en bloc : l'API remplace les lignes d'un coup.
    private readonly ObservableCollection<InventoryCountLineDraft> countLineDrafts = [];

    // Article repris dans le formulaire (modification) ; null en creation.
    private string? editingItemCode;

    // Magasin repris dans le formulaire (modification) ; null en creation.
    private string? editingWarehouseCode;

    // Inventaire selectionne, tel que renvoye par l'API. Sa source de verite reste le serveur :
    // l'ecran ne deduit jamais lui-meme qu'un inventaire est encore modifiable.
    private InventoryCountResponse? selectedCount;

    // Droits du profil connecte, releves a l'ouverture de la session. Les actions d'ecriture sont
    // grisees quand le droit manque, plutot que de laisser l'utilisateur decouvrir un 403 apres
    // avoir saisi tout un formulaire. Le serveur reste la seule autorite.
    private bool canRead = true;

    private bool canWrite = true;

    private bool canValidate = true;

    // Vrai le temps de ResetState : la remise a zero des filtres declenche leurs gestionnaires,
    // qui ne doivent en aucun cas relancer un chargement.
    private bool suspendFilterReload;

    public InventoryView()
    {
        InitializeComponent();

        // La vue rend ses StringFormat XAML (N2, N3, dd/MM/yyyy) dans la culture du poste :
        // sans cela, WPF formaterait en culture invariante et l'ecran afficherait des nombres
        // dans un format different de celui du code-behind.
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);

        InitializeDefaults();
    }

    /// <summary>Memorise le contexte prete par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext moduleViewContext)
    {
        context = moduleViewContext;
        canRead = moduleViewContext.HasPermission(InventoryRead);
        canWrite = moduleViewContext.HasPermission(InventoryWrite);
        canValidate = moduleViewContext.HasPermission(InventoryValidate);

        UpdateMovementActionState();
        UpdateItemActionState();
        UpdateWarehouseActionState();
        UpdateCountActionState();
    }

    /// <summary>
    /// (Re)charge les quatre sections du module. Sortie silencieuse tant que la vue n'a pas de
    /// contexte ou que personne n'est connecte.
    /// </summary>
    public async Task LoadAsync()
    {
        if (context is null || !context.ApiClient.IsAuthenticated)
        {
            return;
        }

        // Sans le droit de lecture, chaque appel de cet ecran repondrait 403 : mieux vaut le
        // dire une fois que d'enchainer quatre erreurs reseau. Le refus reste celui du serveur.
        if (!canRead)
        {
            SetStatus(
                "Permission requise : inventory.read. Votre profil ne peut pas consulter les stocks.",
                isError: true);
            return;
        }

        await context.RunAsync(LoadEverythingAsync);
    }

    /// <summary>Vide grilles, formulaires et compteurs (appelee a la deconnexion).</summary>
    public void ResetState()
    {
        suspendFilterReload = true;

        try
        {
            StockDataGrid.ItemsSource = null;
            MovementsDataGrid.ItemsSource = null;
            ItemsDataGrid.ItemsSource = null;
            WarehousesDataGrid.ItemsSource = null;
            CountsDataGrid.ItemsSource = null;

            countLineDrafts.Clear();
            selectedCount = null;

            StockWarehouseComboBox.ItemsSource = null;
            MovementWarehouseComboBox.ItemsSource = null;
            MovementDestinationComboBox.ItemsSource = null;
            MovementItemComboBox.ItemsSource = null;
            MovementFilterWarehouseComboBox.ItemsSource = null;
            MovementFilterItemComboBox.ItemsSource = null;
            WarehouseUnitComboBox.ItemsSource = null;
            CountWarehouseComboBox.ItemsSource = null;
            CountLineItemComboBox.ItemsSource = null;

            StockTotalValueTextBlock.Text = "—";
            StockBelowMinimumTextBlock.Text = "—";
            LowStockBanner.Visibility = Visibility.Collapsed;
            LowStockBannerText.Text = string.Empty;

            ResetItemForm();
            ResetWarehouseForm();
            ResetMovementForm();
            ResetCountDetail();

            ItemSearchTextBox.Text = string.Empty;
            ItemIncludeInactiveCheckBox.IsChecked = false;

            MovementFilterFromDatePicker.SelectedDate = DateTime.Today.AddDays(-30);
            MovementFilterToDatePicker.SelectedDate = DateTime.Today;

            InventoryTabs.SelectedIndex = 0;
        }
        finally
        {
            suspendFilterReload = false;
        }
    }

    // ============================== Initialisation ==============================

    private void InitializeDefaults()
    {
        MovementDatePicker.SelectedDate = DateTime.Today;
        MovementFilterFromDatePicker.SelectedDate = DateTime.Today.AddDays(-30);
        MovementFilterToDatePicker.SelectedDate = DateTime.Today;
        CountDatePicker.SelectedDate = DateTime.Today;

        MovementKindComboBox.ItemsSource = new[]
        {
            new InventoryCaptureOption(InventoryCaptureKind.PurchaseEntry, "Entrée (achat / réception)"),
            new InventoryCaptureOption(InventoryCaptureKind.Consumption, "Sortie (consommation)"),
            new InventoryCaptureOption(InventoryCaptureKind.Transfer, "Transfert entre magasins"),
            new InventoryCaptureOption(InventoryCaptureKind.InventoryAdjustment, "Ajustement manuel")
        };
        MovementKindComboBox.SelectedIndex = 0;

        MovementDirectionComboBox.ItemsSource = new[]
        {
            new InventoryDirectionOption(true, "Augmentation de stock"),
            new InventoryDirectionOption(false, "Diminution de stock")
        };
        MovementDirectionComboBox.SelectedIndex = 0;

        MovementFilterKindComboBox.ItemsSource = BuildKindFilterOptions();
        MovementFilterKindComboBox.SelectedIndex = 0;

        ItemCategoryComboBox.ItemsSource = Enum.GetValues<StockItemCategory>()
            .Select(category => new InventoryCategoryOption(category, InventoryLabels.Category(category)))
            .ToArray();
        ItemCategoryComboBox.SelectedIndex = 0;

        CountFilterStatusComboBox.ItemsSource = new[]
        {
            new InventoryCountStatusOption(null, "Tous les statuts"),
            new InventoryCountStatusOption(InventoryCountStatus.Draft, "Brouillon"),
            new InventoryCountStatusOption(InventoryCountStatus.Validated, "Validé")
        };
        CountFilterStatusComboBox.SelectedIndex = 0;

        CountLinesDataGrid.ItemsSource = countLineDrafts;

        UpdateMovementFormForKind();
    }

    private static InventoryKindOption[] BuildKindFilterOptions()
    {
        var options = new List<InventoryKindOption> { new(null, "Toutes les natures") };

        options.AddRange(Enum.GetValues<StockMovementKind>()
            .Select(kind => new InventoryKindOption(kind, InventoryLabels.Kind(kind))));

        return options.ToArray();
    }

    // ================================ Chargements ================================

    private async Task LoadEverythingAsync()
    {
        // LoadReferentialAsync recharge lui-meme l'etat du stock une fois les listes
        // deroulantes reconstruites : l'appeler une seconde fois ici doublerait l'appel
        // serveur pour un resultat identique.
        await LoadReferentialAsync();
        await LoadMovementsAsync();
        await LoadCountsAsync();
        await LoadLowStockAsync();
    }

    /// <summary>
    /// Charge le referentiel commun aux quatre onglets : magasins, articles et unites
    /// hotelieres. Les listes deroulantes ne proposent que des elements ACTIFS - le serveur
    /// refuse un mouvement sur un magasin ou un article desactive, l'ecran ne le propose donc
    /// pas - tandis que les grilles d'administration peuvent, elles, montrer les desactives.
    /// </summary>
    private async Task LoadReferentialAsync()
    {
        // Reconstruire les listes deroulantes declenche mecaniquement
        // StockWarehouseComboBox_SelectionChanged (une fois a l'affectation de l'ItemsSource,
        // une fois au SelectCode qui restaure la selection). Ce gestionnaire relancerait un
        // context.RunAsync IMBRIQUE dans celui du chargement en cours : RunApiActionAsync n'est
        // pas reentrant, son finally interieur reactiverait l'onglet et effacerait le curseur
        // d'attente alors que l'appel exterieur tourne encore, ce qui ferait tomber la garantie
        // anti-double-soumission. Le drapeau neutralise donc le rechargement par evenement, et
        // le stock est recharge une seule fois, explicitement, apres la reconstruction.
        suspendFilterReload = true;

        try
        {
            await LoadReferentialCoreAsync();
        }
        finally
        {
            suspendFilterReload = false;
        }

        await LoadStockAsync();
    }

    private async Task LoadReferentialCoreAsync()
    {
        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        var warehouses = await moduleContext.ApiClient.GetWarehousesAsync(moduleContext.ApiBaseUrl, includeInactive: true);
        var items = await moduleContext.ApiClient.GetStockItemsAsync(
            moduleContext.ApiBaseUrl,
            NullIfBlank(ItemSearchTextBox.Text),
            ItemIncludeInactiveCheckBox.IsChecked == true);
        var units = await moduleContext.ApiClient.GetHotelUnitsAsync(moduleContext.ApiBaseUrl, includeInactive: false);

        var previousStockWarehouse = SelectedCode(StockWarehouseComboBox);
        var previousMovementWarehouse = SelectedCode(MovementWarehouseComboBox);
        var previousCountWarehouse = SelectedCode(CountWarehouseComboBox);

        // Cles stables des lignes selectionnees, pour retrouver la selection apres rechargement.
        var previousItemCode = (ItemsDataGrid.SelectedItem as StockItemResponse)?.Code ?? editingItemCode;
        var previousWarehouseSelection = (WarehousesDataGrid.SelectedItem as WarehouseResponse)?.Code ?? editingWarehouseCode;

        WarehousesDataGrid.ItemsSource = warehouses;
        ItemsDataGrid.ItemsSource = items;

        // Selection restauree par sa cle stable ; si la ligne a disparu du filtre, le formulaire
        // repart proprement en creation plutot que de rester sur un objet fantome.
        var restoredItem = previousItemCode is null
            ? null
            : items.FirstOrDefault(item => string.Equals(item.Code, previousItemCode, StringComparison.OrdinalIgnoreCase));

        if (restoredItem is not null)
        {
            ItemsDataGrid.SelectedItem = restoredItem;
        }
        else
        {
            ResetItemForm();
        }

        var restoredWarehouse = previousWarehouseSelection is null
            ? null
            : warehouses.FirstOrDefault(warehouse => string.Equals(warehouse.Code, previousWarehouseSelection, StringComparison.OrdinalIgnoreCase));

        if (restoredWarehouse is not null)
        {
            WarehousesDataGrid.SelectedItem = restoredWarehouse;
        }
        else
        {
            ResetWarehouseForm();
        }

        var activeWarehouses = warehouses
            .Where(warehouse => warehouse.IsActive)
            .Select(warehouse => new InventoryCodeOption(warehouse.Code, $"{warehouse.Code} — {warehouse.Label}"))
            .ToArray();

        StockWarehouseComboBox.ItemsSource = activeWarehouses;
        MovementWarehouseComboBox.ItemsSource = activeWarehouses;
        MovementDestinationComboBox.ItemsSource = activeWarehouses;
        CountWarehouseComboBox.ItemsSource = activeWarehouses;

        MovementFilterWarehouseComboBox.ItemsSource = WithAllOption(activeWarehouses, "Tous les magasins");

        // Les articles proposes a la saisie sont toujours les actifs, meme si la grille
        // d'administration affiche aussi les desactives a la demande.
        var activeItems = await moduleContext.ApiClient.GetStockItemsAsync(
            moduleContext.ApiBaseUrl,
            search: null,
            includeInactive: false);

        var itemOptions = activeItems
            .Select(item => new InventoryCodeOption(item.Code, $"{item.Code} — {item.Designation}"))
            .ToArray();

        MovementItemComboBox.ItemsSource = itemOptions;
        CountLineItemComboBox.ItemsSource = itemOptions;
        MovementFilterItemComboBox.ItemsSource = WithAllOption(itemOptions, "Tous les articles");

        WarehouseUnitComboBox.ItemsSource = units
            .Select(unit => new InventoryCodeOption(unit.Code, $"{unit.Code} — {unit.Name}"))
            .ToArray();

        SelectCode(StockWarehouseComboBox, previousStockWarehouse ?? activeWarehouses.FirstOrDefault()?.Code);
        SelectCode(MovementWarehouseComboBox, previousMovementWarehouse ?? activeWarehouses.FirstOrDefault()?.Code);
        SelectCode(CountWarehouseComboBox, previousCountWarehouse ?? activeWarehouses.FirstOrDefault()?.Code);
        SelectCode(MovementFilterWarehouseComboBox, null);
        SelectCode(MovementFilterItemComboBox, null);

        UpdateItemActionState();
        UpdateWarehouseActionState();
    }

    private async Task LoadStockAsync()
    {
        var moduleContext = RequireContext();
        var warehouseCode = SelectedCode(StockWarehouseComboBox);

        if (moduleContext is null || warehouseCode is null)
        {
            StockDataGrid.ItemsSource = null;
            StockTotalValueTextBlock.Text = "—";
            StockBelowMinimumTextBlock.Text = "—";
            return;
        }

        var stock = await moduleContext.ApiClient.GetWarehouseStockAsync(moduleContext.ApiBaseUrl, warehouseCode);

        StockDataGrid.ItemsSource = stock.Rows;

        // Valorisation affichee telle que renvoyee par le serveur : jamais recalculee ici.
        StockTotalValueTextBlock.Text = stock.TotalValue.ToString("N2", CultureInfo.CurrentCulture);
        StockBelowMinimumTextBlock.Text = stock.Rows
            .Count(row => row.IsBelowMinimum)
            .ToString(CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Alertes de seuil, TOUS magasins confondus. Le bandeau le dit explicitement, parce que le
    /// chiffre qu'il porte ne repond pas au filtre "magasin" visible juste au-dessus de lui.
    /// </summary>
    private async Task LoadLowStockAsync()
    {
        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        var alerts = await moduleContext.ApiClient.GetLowStockAsync(moduleContext.ApiBaseUrl);

        if (alerts.Count == 0)
        {
            LowStockBanner.Visibility = Visibility.Collapsed;
            LowStockBannerText.Text = string.Empty;
            return;
        }

        var warehouseCount = alerts.Select(alert => alert.WarehouseCode).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        LowStockBannerText.Text = string.Format(
            CultureInfo.CurrentCulture,
            alerts.Count == 1
                ? "{0} article passe sous son seuil d'alerte, dans {1} magasin — tous magasins confondus, indépendamment du magasin filtré ci-dessus."
                : "{0} articles passent sous leur seuil d'alerte, dans {1} magasin(s) — tous magasins confondus, indépendamment du magasin filtré ci-dessus.",
            alerts.Count,
            warehouseCount);

        LowStockBanner.Visibility = Visibility.Visible;
    }

    private async Task LoadMovementsAsync()
    {
        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        var movements = await moduleContext.ApiClient.GetStockMovementsAsync(
            moduleContext.ApiBaseUrl,
            SelectedDate(MovementFilterFromDatePicker),
            SelectedDate(MovementFilterToDatePicker),
            SelectedCode(MovementFilterWarehouseComboBox),
            SelectedCode(MovementFilterItemComboBox),
            (MovementFilterKindComboBox.SelectedItem as InventoryKindOption)?.Value);

        MovementsDataGrid.ItemsSource = movements;
    }

    private async Task LoadCountsAsync()
    {
        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        var counts = await moduleContext.ApiClient.GetInventoryCountsAsync(
            moduleContext.ApiBaseUrl,
            warehouseCode: null,
            (CountFilterStatusComboBox.SelectedItem as InventoryCountStatusOption)?.Value);

        var previousId = selectedCount?.Id;

        CountsDataGrid.ItemsSource = counts;

        // Selection restauree par sa cle stable ; si l'inventaire a disparu du filtre, le
        // panneau de detail repart proprement a vide.
        var restored = previousId.HasValue
            ? counts.FirstOrDefault(count => count.Id == previousId.Value)
            : null;

        if (restored is not null)
        {
            CountsDataGrid.SelectedItem = restored;
        }
        else
        {
            CountsDataGrid.SelectedItem = null;
            ResetCountDetail();
        }
    }

    // ============================= Onglet Stock courant =============================

    private async void RefreshAllButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await LoadEverythingAsync();
            SetStatus("Stocks rechargés.");
        });
    }

    private async void RefreshStockButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await LoadStockAsync();
            await LoadLowStockAsync();
            SetStatus("Stock du magasin rechargé.");
        });
    }

    private async void StockWarehouseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suspendFilterReload || context is null || !context.ApiClient.IsAuthenticated)
        {
            return;
        }

        await context.RunAsync(LoadStockAsync);
    }

    // ============================== Onglet Mouvements ==============================

    private void MovementKindComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateMovementFormForKind();
    }

    /// <summary>
    /// Adapte le formulaire a la nature choisie et rappelle a l'ecran la regle du serveur qui
    /// s'y applique. Aucune de ces regles n'est appliquee ici : elles sont affichees pour que
    /// l'utilisateur les connaisse avant de saisir, le refus restant celui du serveur.
    /// </summary>
    private void UpdateMovementFormForKind()
    {
        var kind = SelectedCaptureKind();

        MovementDestinationPanel.Visibility = kind == InventoryCaptureKind.Transfer
            ? Visibility.Visible
            : Visibility.Collapsed;

        MovementDirectionPanel.Visibility = kind == InventoryCaptureKind.InventoryAdjustment
            ? Visibility.Visible
            : Visibility.Collapsed;

        var costRequired = kind == InventoryCaptureKind.PurchaseEntry;

        MovementUnitCostPanel.Visibility = kind == InventoryCaptureKind.InventoryAdjustment
            ? Visibility.Collapsed
            : Visibility.Visible;
        MovementUnitCostLabel.Text = costRequired ? "Coût unitaire *" : "Coût unitaire";

        var lotRelevant = kind is InventoryCaptureKind.PurchaseEntry or InventoryCaptureKind.Transfer;
        MovementLotPanel.Visibility = lotRelevant ? Visibility.Visible : Visibility.Collapsed;
        MovementExpiryPanel.Visibility = lotRelevant ? Visibility.Visible : Visibility.Collapsed;

        MovementWarehouseLabel.Text = kind == InventoryCaptureKind.Transfer
            ? "Magasin d'origine *"
            : "Magasin *";

        MovementRuleTextBlock.Text = kind switch
        {
            InventoryCaptureKind.PurchaseEntry =>
                "Entrée : le coût unitaire est obligatoire, c'est lui qui alimente le coût moyen pondéré de l'article.",
            InventoryCaptureKind.Consumption =>
                "Sortie : le serveur refuse toute sortie qui rendrait le stock négatif — le stock ne peut jamais descendre en dessous de zéro.",
            InventoryCaptureKind.Transfer =>
                "Transfert : deux mouvements liés créés en une seule opération. Les deux magasins doivent être distincts, et le stock du magasin d'origine ne peut pas devenir négatif.",
            InventoryCaptureKind.InventoryAdjustment =>
                "Ajustement manuel : le sens est obligatoire. Une diminution est refusée si elle rendrait le stock négatif. Pour un inventaire complet, préférez l'onglet « Inventaires ».",
            _ => string.Empty
        };
    }

    private InventoryCaptureKind SelectedCaptureKind()
    {
        return (MovementKindComboBox.SelectedItem as InventoryCaptureOption)?.Value
            ?? InventoryCaptureKind.PurchaseEntry;
    }

    private void ClearMovementButton_Click(object sender, RoutedEventArgs e)
    {
        ResetMovementForm();
        SetStatus("Formulaire de saisie vidé.");
    }

    private async void SaveMovementButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        var kind = SelectedCaptureKind();

        var warehouseCode = SelectedCode(MovementWarehouseComboBox);

        if (warehouseCode is null)
        {
            SetStatus("Le magasin est obligatoire.", isError: true);
            return;
        }

        var itemCode = SelectedCode(MovementItemComboBox);

        if (itemCode is null)
        {
            SetStatus("L'article est obligatoire.", isError: true);
            return;
        }

        var movementDate = SelectedDate(MovementDatePicker);

        if (movementDate is null)
        {
            SetStatus("La date du mouvement est obligatoire.", isError: true);
            return;
        }

        if (!TryReadQuantity(MovementQuantityTextBox, "La quantité", requireStrictlyPositive: true, out var quantity))
        {
            return;
        }

        var reference = MovementReferenceTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(reference))
        {
            SetStatus("La référence est obligatoire : c'est la pièce qui justifie le mouvement.", isError: true);
            MovementReferenceTextBox.Focus();
            return;
        }

        var lotNumber = NullIfBlank(MovementLotTextBox.Text);
        var expiryDate = SelectedDate(MovementExpiryDatePicker);
        var notes = NullIfBlank(MovementNotesTextBox.Text);

        if (kind == InventoryCaptureKind.Transfer)
        {
            var destinationCode = SelectedCode(MovementDestinationComboBox);

            if (destinationCode is null)
            {
                SetStatus("Le magasin destinataire est obligatoire pour un transfert.", isError: true);
                return;
            }

            if (string.Equals(destinationCode, warehouseCode, StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("Un transfert exige deux magasins distincts.", isError: true);
                return;
            }

            var transferRequest = new CreateStockTransferRequest(
                warehouseCode,
                destinationCode,
                itemCode,
                movementDate.Value,
                quantity,
                reference,
                lotNumber,
                expiryDate,
                notes);

            await moduleContext.RunAsync(async () =>
            {
                await moduleContext.ApiClient.CreateStockTransferAsync(moduleContext.ApiBaseUrl, transferRequest);
                ResetMovementForm();
                await LoadMovementsAsync();
                await LoadStockAsync();
                await LoadLowStockAsync();
                SetStatus("Transfert enregistré : les deux mouvements liés ont été créés.");
            });

            return;
        }

        decimal? unitCost = null;

        if (kind != InventoryCaptureKind.InventoryAdjustment && !string.IsNullOrWhiteSpace(MovementUnitCostTextBox.Text))
        {
            if (!TryReadMoney(MovementUnitCostTextBox, "Le coût unitaire", out var cost))
            {
                return;
            }

            unitCost = cost;
        }

        if (kind == InventoryCaptureKind.PurchaseEntry && unitCost is null)
        {
            SetStatus(
                "Le coût unitaire est obligatoire pour une entrée : c'est lui qui alimente le coût moyen pondéré.",
                isError: true);
            MovementUnitCostTextBox.Focus();
            return;
        }

        var movementKind = kind switch
        {
            InventoryCaptureKind.PurchaseEntry => StockMovementKind.PurchaseEntry,
            InventoryCaptureKind.Consumption => StockMovementKind.Consumption,
            _ => StockMovementKind.InventoryAdjustment
        };

        bool? adjustmentIsIncrease = kind == InventoryCaptureKind.InventoryAdjustment
            ? (MovementDirectionComboBox.SelectedItem as InventoryDirectionOption)?.IsIncrease
            : null;

        if (kind == InventoryCaptureKind.InventoryAdjustment && adjustmentIsIncrease is null)
        {
            SetStatus("Le sens de l'ajustement est obligatoire.", isError: true);
            return;
        }

        var request = new CreateStockMovementRequest(
            warehouseCode,
            itemCode,
            movementDate.Value,
            movementKind,
            quantity,
            unitCost,
            reference,
            lotNumber,
            expiryDate,
            notes,
            adjustmentIsIncrease);

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.CreateStockMovementAsync(moduleContext.ApiBaseUrl, request);
            ResetMovementForm();
            await LoadMovementsAsync();
            await LoadStockAsync();
            await LoadLowStockAsync();
            SetStatus("Mouvement enregistré.");
        });
    }

    private async void ApplyMovementFilterButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        var from = SelectedDate(MovementFilterFromDatePicker);
        var to = SelectedDate(MovementFilterToDatePicker);

        if (from.HasValue && to.HasValue && from > to)
        {
            SetStatus("La date de début ne peut pas être postérieure à la date de fin.", isError: true);
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await LoadMovementsAsync();
            SetStatus("Registre des mouvements rechargé.");
        });
    }

    private void ResetMovementForm()
    {
        MovementQuantityTextBox.Text = string.Empty;
        MovementUnitCostTextBox.Text = string.Empty;
        MovementReferenceTextBox.Text = string.Empty;
        MovementLotTextBox.Text = string.Empty;
        MovementNotesTextBox.Text = string.Empty;
        MovementExpiryDatePicker.SelectedDate = null;
        MovementDatePicker.SelectedDate = DateTime.Today;

        if (MovementDirectionComboBox.Items.Count > 0)
        {
            MovementDirectionComboBox.SelectedIndex = 0;
        }

        UpdateMovementFormForKind();
    }

    // ========================= Onglet Articles & magasins =========================

    private async void SearchItemsButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await LoadReferentialAsync();
            SetStatus("Liste des articles rechargée.");
        });
    }

    private void NewItemButton_Click(object sender, RoutedEventArgs e)
    {
        ItemsDataGrid.SelectedItem = null;
        ResetItemForm();
    }

    private void ItemsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ItemsDataGrid.SelectedItem is not StockItemResponse selected)
        {
            UpdateItemActionState();
            return;
        }

        editingItemCode = selected.Code;

        ItemCodeTextBox.Text = selected.Code;
        ItemCodeTextBox.IsEnabled = false;
        ItemDesignationTextBox.Text = selected.Designation;
        ItemUnitTextBox.Text = selected.UnitOfMeasure;
        ItemMinimumTextBox.Text = selected.MinimumQuantity.ToString("0.###", CultureInfo.CurrentCulture);

        ItemCategoryComboBox.SelectedItem = ItemCategoryComboBox.Items
            .OfType<InventoryCategoryOption>()
            .FirstOrDefault(option => option.Value == selected.Category);

        SaveItemButton.Content = "Enregistrer les modifications";
        ItemFormModeTextBlock.Text = string.Format(CultureInfo.CurrentCulture, "Modifier {0}", selected.Code);

        UpdateItemActionState();
    }

    private async void SaveItemButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        var designation = ItemDesignationTextBox.Text.Trim();
        var unit = ItemUnitTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(designation))
        {
            SetStatus("La désignation de l'article est obligatoire.", isError: true);
            ItemDesignationTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            SetStatus("L'unité de mesure est obligatoire.", isError: true);
            ItemUnitTextBox.Focus();
            return;
        }

        var minimum = 0m;

        if (!string.IsNullOrWhiteSpace(ItemMinimumTextBox.Text)
            && !TryReadQuantity(ItemMinimumTextBox, "Le seuil d'alerte", requireStrictlyPositive: false, out minimum))
        {
            return;
        }

        var category = (ItemCategoryComboBox.SelectedItem as InventoryCategoryOption)?.Value ?? StockItemCategory.Autre;

        if (editingItemCode is null)
        {
            var code = ItemCodeTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(code))
            {
                SetStatus("Le code de l'article est obligatoire.", isError: true);
                ItemCodeTextBox.Focus();
                return;
            }

            var createRequest = new CreateStockItemRequest(code, designation, unit, category, minimum);

            await moduleContext.RunAsync(async () =>
            {
                await moduleContext.ApiClient.CreateStockItemAsync(moduleContext.ApiBaseUrl, createRequest);
                ResetItemForm();
                await LoadReferentialAsync();
                SetStatus(string.Format(CultureInfo.CurrentCulture, "Article {0} créé.", code.ToUpperInvariant()));
            });

            return;
        }

        var updateRequest = new UpdateStockItemRequest(designation, unit, category, minimum);
        var editedCode = editingItemCode;

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.UpdateStockItemAsync(moduleContext.ApiBaseUrl, editedCode, updateRequest);
            await LoadReferentialAsync();
            SetStatus(string.Format(CultureInfo.CurrentCulture, "Article {0} enregistré.", editedCode));
        });
    }

    private async void ToggleItemActiveButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null || ItemsDataGrid.SelectedItem is not StockItemResponse selected)
        {
            return;
        }

        var activate = !selected.IsActive;

        if (!activate)
        {
            var question = string.Format(
                CultureInfo.CurrentCulture,
                "Désactiver l'article {0} ({1}) ?{2}{2}Il ne sera plus proposé à la saisie des mouvements ni des comptages d'inventaire. Son historique de mouvements et le stock qui en découle restent intacts.",
                selected.Code,
                selected.Designation,
                Environment.NewLine);

            if (!Confirm(question, "Désactivation d'un article"))
            {
                return;
            }
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.SetStockItemActiveAsync(moduleContext.ApiBaseUrl, selected.Code, activate);
            await LoadReferentialAsync();
            SetStatus(activate ? "Article activé." : "Article désactivé.");
        });
    }

    private void ResetItemForm()
    {
        editingItemCode = null;

        ItemCodeTextBox.Text = string.Empty;
        ItemCodeTextBox.IsEnabled = true;
        ItemDesignationTextBox.Text = string.Empty;
        ItemUnitTextBox.Text = string.Empty;
        ItemMinimumTextBox.Text = string.Empty;

        if (ItemCategoryComboBox.Items.Count > 0)
        {
            ItemCategoryComboBox.SelectedIndex = 0;
        }

        SaveItemButton.Content = "Créer l'article";
        ItemFormModeTextBlock.Text = "Nouvel article";

        UpdateItemActionState();
    }

    private void NewWarehouseButton_Click(object sender, RoutedEventArgs e)
    {
        WarehousesDataGrid.SelectedItem = null;
        ResetWarehouseForm();
    }

    private void WarehousesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WarehousesDataGrid.SelectedItem is not WarehouseResponse selected)
        {
            UpdateWarehouseActionState();
            return;
        }

        editingWarehouseCode = selected.Code;

        WarehouseCodeTextBox.Text = selected.Code;
        WarehouseCodeTextBox.IsEnabled = false;
        WarehouseLabelTextBox.Text = selected.Label;
        SelectCode(WarehouseUnitComboBox, selected.HotelUnitCode);

        SaveWarehouseButton.Content = "Enregistrer les modifications";
        WarehouseFormModeTextBlock.Text = string.Format(CultureInfo.CurrentCulture, "Modifier {0}", selected.Code);

        UpdateWarehouseActionState();
    }

    private async void SaveWarehouseButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        var label = WarehouseLabelTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(label))
        {
            SetStatus("Le libellé du magasin est obligatoire.", isError: true);
            WarehouseLabelTextBox.Focus();
            return;
        }

        var unitCode = SelectedCode(WarehouseUnitComboBox);

        if (unitCode is null)
        {
            SetStatus("L'unité hôtelière de rattachement est obligatoire.", isError: true);
            return;
        }

        if (editingWarehouseCode is null)
        {
            var code = WarehouseCodeTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(code))
            {
                SetStatus("Le code du magasin est obligatoire.", isError: true);
                WarehouseCodeTextBox.Focus();
                return;
            }

            var createRequest = new CreateWarehouseRequest(code, label, unitCode);

            await moduleContext.RunAsync(async () =>
            {
                await moduleContext.ApiClient.CreateWarehouseAsync(moduleContext.ApiBaseUrl, createRequest);
                ResetWarehouseForm();
                await LoadReferentialAsync();
                SetStatus(string.Format(CultureInfo.CurrentCulture, "Magasin {0} créé.", code.ToUpperInvariant()));
            });

            return;
        }

        var updateRequest = new UpdateWarehouseRequest(label, unitCode);
        var editedCode = editingWarehouseCode;

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.UpdateWarehouseAsync(moduleContext.ApiBaseUrl, editedCode, updateRequest);
            await LoadReferentialAsync();
            SetStatus(string.Format(CultureInfo.CurrentCulture, "Magasin {0} enregistré.", editedCode));
        });
    }

    private async void ToggleWarehouseActiveButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null || WarehousesDataGrid.SelectedItem is not WarehouseResponse selected)
        {
            return;
        }

        var activate = !selected.IsActive;

        if (!activate)
        {
            var question = string.Format(
                CultureInfo.CurrentCulture,
                "Désactiver le magasin {0} ({1}) ?{2}{2}Il n'acceptera plus aucun mouvement ni inventaire et ne sera plus proposé à la saisie. Son registre de mouvements, donc le stock qu'il porte, reste intact et consultable.",
                selected.Code,
                selected.Label,
                Environment.NewLine);

            if (!Confirm(question, "Désactivation d'un magasin"))
            {
                return;
            }
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.SetWarehouseActiveAsync(moduleContext.ApiBaseUrl, selected.Code, activate);
            await LoadReferentialAsync();
            SetStatus(activate ? "Magasin activé." : "Magasin désactivé.");
        });
    }

    private void ResetWarehouseForm()
    {
        editingWarehouseCode = null;

        WarehouseCodeTextBox.Text = string.Empty;
        WarehouseCodeTextBox.IsEnabled = true;
        WarehouseLabelTextBox.Text = string.Empty;
        SelectCode(WarehouseUnitComboBox, null);

        SaveWarehouseButton.Content = "Créer le magasin";
        WarehouseFormModeTextBlock.Text = "Nouveau magasin";

        UpdateWarehouseActionState();
    }

    // ============================= Onglet Inventaires =============================

    private async void RefreshCountsButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await LoadCountsAsync();
            SetStatus("Inventaires rechargés.");
        });
    }

    private async void CreateCountButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        var warehouseCode = SelectedCode(CountWarehouseComboBox);

        if (warehouseCode is null)
        {
            SetStatus("Le magasin est obligatoire pour ouvrir un inventaire.", isError: true);
            return;
        }

        var countDate = SelectedDate(CountDatePicker);

        if (countDate is null)
        {
            SetStatus("La date de comptage est obligatoire.", isError: true);
            return;
        }

        var request = new CreateInventoryCountRequest(warehouseCode, countDate.Value);

        await moduleContext.RunAsync(async () =>
        {
            var created = await moduleContext.ApiClient.CreateInventoryCountAsync(moduleContext.ApiBaseUrl, request);
            selectedCount = created;
            await LoadCountsAsync();
            SetStatus(string.Format(
                CultureInfo.CurrentCulture,
                "Inventaire du {0:dd/MM/yyyy} ouvert pour le magasin {1}.",
                created.CountDate,
                created.WarehouseCode));
        });
    }

    private void CountsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CountsDataGrid.SelectedItem is not InventoryCountResponse selected)
        {
            ResetCountDetail();
            return;
        }

        selectedCount = selected;

        countLineDrafts.Clear();

        foreach (var line in selected.Lines.OrderBy(line => line.LineNumber))
        {
            countLineDrafts.Add(new InventoryCountLineDraft(
                line.ItemCode,
                line.Designation ?? line.ItemCode,
                line.CountedQuantity));
        }

        CountDetailTitleTextBlock.Text = string.Format(
            CultureInfo.CurrentCulture,
            "Comptages — {0} du {1:dd/MM/yyyy}",
            selected.WarehouseCode,
            selected.CountDate);

        CountDetailHintTextBlock.Text = selected.CanEdit
            ? "Saisissez les quantités trouvées en rayon, enregistrez le comptage, puis validez l'inventaire."
            : string.Format(
                CultureInfo.CurrentCulture,
                "Inventaire validé le {0:dd/MM/yyyy HH:mm} par {1} : il est immuable, car il est la preuve des ajustements qu'il a générés.",
                selected.ValidatedAt?.ToLocalTime() ?? DateTimeOffset.Now,
                selected.ValidatedBy ?? "—");

        UpdateCountActionState();
    }

    private void AddCountLineButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedCount is null || !selectedCount.CanEdit)
        {
            SetStatus("Sélectionnez un inventaire en brouillon pour saisir des comptages.", isError: true);
            return;
        }

        var itemOption = CountLineItemComboBox.SelectedItem as InventoryCodeOption;

        if (itemOption?.Code is null)
        {
            SetStatus("L'article est obligatoire.", isError: true);
            return;
        }

        // Zero est un comptage valide ("il ne reste rien") : la quantite comptee n'a pas a etre
        // strictement positive, seulement non negative - meme regle que le domaine.
        if (!TryReadQuantity(CountLineQuantityTextBox, "La quantité comptée", requireStrictlyPositive: false, out var quantity))
        {
            return;
        }

        var existing = countLineDrafts.FirstOrDefault(line =>
            string.Equals(line.ItemCode, itemOption.Code, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            // Un article compte deux fois rendrait l'ajustement ambigu : le domaine le refuse,
            // l'ecran remplace donc la ligne plutot que d'en ajouter une seconde.
            countLineDrafts[countLineDrafts.IndexOf(existing)] =
                new InventoryCountLineDraft(existing.ItemCode, existing.Designation, quantity);
        }
        else
        {
            countLineDrafts.Add(new InventoryCountLineDraft(
                itemOption.Code,
                itemOption.Label,
                quantity));
        }

        CountLineQuantityTextBox.Text = string.Empty;
        CountLineQuantityTextBox.Focus();

        UpdateCountActionState();
    }

    private void RemoveCountLineButton_Click(object sender, RoutedEventArgs e)
    {
        if (CountLinesDataGrid.SelectedItem is not InventoryCountLineDraft selected)
        {
            return;
        }

        countLineDrafts.Remove(selected);
        UpdateCountActionState();
    }

    private void CountLinesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateCountActionState();
    }

    private async void SaveCountLinesButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null || selectedCount is null)
        {
            return;
        }

        var countId = selectedCount.Id;

        var request = new ReplaceInventoryCountLinesRequest(
            countLineDrafts
                .Select(line => new InventoryCountLineRequest(line.ItemCode, line.CountedQuantity))
                .ToArray());

        await moduleContext.RunAsync(async () =>
        {
            var updated = await moduleContext.ApiClient.ReplaceInventoryCountLinesAsync(
                moduleContext.ApiBaseUrl,
                countId,
                request);

            selectedCount = updated;
            await LoadCountsAsync();
            SetStatus(string.Format(
                CultureInfo.CurrentCulture,
                "Comptage enregistré : {0} ligne(s).",
                updated.Lines.Count));
        });
    }

    private async void ValidateCountButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null || selectedCount is null)
        {
            return;
        }

        var countToValidate = selectedCount;

        // Acte engageant : la confirmation dit precisement ce qui va etre genere et ce qui
        // devient irreversible, et nomme l'inventaire concerne.
        var question = string.Format(
            CultureInfo.CurrentCulture,
            "Valider l'inventaire du magasin {0} au {1:dd/MM/yyyy} ({2} ligne(s) comptée(s)) ?{3}{3}" +
            "Le serveur va générer un mouvement d'ajustement pour chaque article dont la quantité comptée diffère du stock théorique — en plus comme en moins — puis figer cet inventaire.{3}{3}" +
            "Un inventaire validé est IMMUABLE : il est la preuve documentaire des ajustements qu'il a générés. Cette action est irréversible.",
            countToValidate.WarehouseCode,
            countToValidate.CountDate,
            countToValidate.Lines.Count,
            Environment.NewLine);

        if (!Confirm(question, "Validation d'un inventaire"))
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var validation = await moduleContext.ApiClient.ValidateInventoryCountAsync(
                moduleContext.ApiBaseUrl,
                countToValidate.Id);

            selectedCount = validation.Count;

            await LoadCountsAsync();
            await LoadMovementsAsync();
            await LoadStockAsync();
            await LoadLowStockAsync();

            // Nombre d'ajustements affiche tel que renvoye par le serveur, jamais recompte ici.
            SetStatus(string.Format(
                CultureInfo.CurrentCulture,
                validation.AdjustmentCount == 0
                    ? "Inventaire validé : aucun écart, aucun ajustement généré."
                    : "Inventaire validé : {0} mouvement(s) d'ajustement généré(s).",
                validation.AdjustmentCount));
        });
    }

    private void ResetCountDetail()
    {
        selectedCount = null;
        countLineDrafts.Clear();

        CountDetailTitleTextBlock.Text = "Comptages";
        CountDetailHintTextBlock.Text = "Sélectionnez un inventaire pour saisir ses comptages.";
        CountLineQuantityTextBox.Text = string.Empty;

        UpdateCountActionState();
    }

    // =========================== Permissions et etats ===========================

    /// <summary>
    /// Pose le message d'explication quand le droit manque, et RESTAURE l'info-bulle d'origine
    /// du bouton quand il est present : l'affectation doit etre symetrique, sinon un message
    /// pose pour un profil restreint survit a la reconnexion d'un profil qui, lui, a le droit.
    /// </summary>
    private void ApplyPermissionHint(Button button, bool allowed, string hint)
    {
        if (!originalToolTips.ContainsKey(button))
        {
            originalToolTips[button] = button.ToolTip;
        }

        button.ToolTip = allowed ? originalToolTips[button] : hint;
    }

    private void UpdateMovementActionState()
    {
        SaveMovementButton.IsEnabled = canWrite;
        ApplyPermissionHint(SaveMovementButton, canWrite, WritePermissionHint);
    }

    private void UpdateItemActionState()
    {
        var hasSelection = ItemsDataGrid.SelectedItem is StockItemResponse;

        SaveItemButton.IsEnabled = canWrite;
        ApplyPermissionHint(SaveItemButton, canWrite, WritePermissionHint);

        ToggleItemActiveButton.IsEnabled = canWrite && hasSelection;
        ApplyPermissionHint(ToggleItemActiveButton, canWrite, WritePermissionHint);

        ToggleItemActiveButton.Content = ItemsDataGrid.SelectedItem is StockItemResponse { IsActive: false }
            ? "Activer"
            : "Désactiver";
    }

    private void UpdateWarehouseActionState()
    {
        var hasSelection = WarehousesDataGrid.SelectedItem is WarehouseResponse;

        SaveWarehouseButton.IsEnabled = canWrite;
        ApplyPermissionHint(SaveWarehouseButton, canWrite, WritePermissionHint);

        ToggleWarehouseActiveButton.IsEnabled = canWrite && hasSelection;
        ApplyPermissionHint(ToggleWarehouseActiveButton, canWrite, WritePermissionHint);

        ToggleWarehouseActiveButton.Content = WarehousesDataGrid.SelectedItem is WarehouseResponse { IsActive: false }
            ? "Activer"
            : "Désactiver";
    }

    private void UpdateCountActionState()
    {
        // CanEdit vient du serveur (statut Brouillon) : l'ecran n'en deduit jamais lui-meme
        // qu'un inventaire est encore modifiable.
        var editable = selectedCount?.CanEdit == true;

        CreateCountButton.IsEnabled = canWrite;
        ApplyPermissionHint(CreateCountButton, canWrite, WritePermissionHint);

        AddCountLineButton.IsEnabled = canWrite && editable;
        ApplyPermissionHint(AddCountLineButton, canWrite, WritePermissionHint);

        RemoveCountLineButton.IsEnabled = canWrite && editable && CountLinesDataGrid.SelectedItem is InventoryCountLineDraft;
        ApplyPermissionHint(RemoveCountLineButton, canWrite, WritePermissionHint);

        SaveCountLinesButton.IsEnabled = canWrite && editable;
        ApplyPermissionHint(SaveCountLinesButton, canWrite, WritePermissionHint);

        ValidateCountButton.IsEnabled = canValidate && editable && countLineDrafts.Count > 0;
        ApplyPermissionHint(ValidateCountButton, canValidate, ValidatePermissionHint);
    }

    // ================================= Utilitaires ===============================

    private ModuleViewContext? RequireContext()
    {
        if (context is null || !context.ApiClient.IsAuthenticated)
        {
            return null;
        }

        return context;
    }

    private void SetStatus(string message, bool isError = false)
    {
        context?.SetStatus(message, isError);
    }

    // Confirmation des actes engageants : fenetre proprietaire, icone d'avertissement, et
    // bouton par defaut sur "Non" - une frappe Entree ne doit jamais suffire a declencher
    // l'action.
    private bool Confirm(string question, string title)
    {
        var owner = Window.GetWindow(this);

        var result = owner is null
            ? MessageBox.Show(question, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(owner, question, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    /// <summary>
    /// Lecture d'une quantite : virgule (culture courante) comme point (culture invariante)
    /// acceptes, et les DEUX bornes du domaine verifiees avant l'envoi plutot qu'apres
    /// l'aller-retour - signe, et 3 decimales au maximum (colonnes numeric(18,3)).
    /// </summary>
    private bool TryReadQuantity(TextBox textBox, string label, bool requireStrictlyPositive, out decimal value)
    {
        var text = textBox.Text.Trim();

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value) &&
            !decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            SetStatus($"{label} doit être une quantité valide.", isError: true);
            textBox.Focus();
            return false;
        }

        if (requireStrictlyPositive && value <= 0m)
        {
            SetStatus($"{label} doit être strictement positive.", isError: true);
            textBox.Focus();
            return false;
        }

        if (!requireStrictlyPositive && value < 0m)
        {
            SetStatus($"{label} ne peut pas être négative.", isError: true);
            textBox.Focus();
            return false;
        }

        if (decimal.Round(value, 3) != value)
        {
            SetStatus($"{label} ne peut pas avoir plus de 3 décimales.", isError: true);
            textBox.Focus();
            return false;
        }

        return true;
    }

    /// <summary>
    /// Lecture d'un cout : meme tolerance de separateur decimal, borne a 2 decimales
    /// (colonnes numeric(18,2)) et jamais negatif.
    /// </summary>
    private bool TryReadMoney(TextBox textBox, string label, out decimal value)
    {
        var text = textBox.Text.Trim();

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value) &&
            !decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            SetStatus($"{label} doit être un montant valide.", isError: true);
            textBox.Focus();
            return false;
        }

        if (value < 0m)
        {
            SetStatus($"{label} ne peut pas être négatif.", isError: true);
            textBox.Focus();
            return false;
        }

        if (decimal.Round(value, 2) != value)
        {
            SetStatus($"{label} ne peut pas avoir plus de 2 décimales.", isError: true);
            textBox.Focus();
            return false;
        }

        return true;
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DateOnly? SelectedDate(DatePicker picker)
    {
        return picker.SelectedDate.HasValue
            ? DateOnly.FromDateTime(picker.SelectedDate.Value)
            : null;
    }

    private static string? SelectedCode(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as InventoryCodeOption)?.Code;
    }

    // Selectionne l'entree portant ce code ; un code nul designe l'entree neutre
    // ("Tous les magasins", "Tous les articles") quand elle existe.
    private static void SelectCode(ComboBox comboBox, string? code)
    {
        comboBox.SelectedItem = comboBox.Items
            .OfType<InventoryCodeOption>()
            .FirstOrDefault(option => string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    private static InventoryCodeOption[] WithAllOption(InventoryCodeOption[] options, string allLabel)
    {
        var result = new List<InventoryCodeOption> { new(null, allLabel) };
        result.AddRange(options);
        return result.ToArray();
    }
}

/// <summary>Nature du mouvement propose a la saisie ; le transfert n'est pas un kind du domaine
/// mais l'operation qui en cree DEUX, d'ou cette enumeration d'ecran distincte.</summary>
public enum InventoryCaptureKind
{
    PurchaseEntry,
    Consumption,
    Transfer,
    InventoryAdjustment
}

/// <summary>Entree de liste deroulante identifiee par un code (magasin, article, unite).</summary>
public sealed class InventoryCodeOption(string? code, string label)
{
    public string? Code { get; } = code;

    public string Label { get; } = label;
}

/// <summary>Entree de liste deroulante pour une categorie d'article.</summary>
public sealed class InventoryCategoryOption(StockItemCategory value, string label)
{
    public StockItemCategory Value { get; } = value;

    public string Label { get; } = label;
}

/// <summary>Entree de liste deroulante pour une nature de mouvement (valeur nulle = toutes).</summary>
public sealed class InventoryKindOption(StockMovementKind? value, string label)
{
    public StockMovementKind? Value { get; } = value;

    public string Label { get; } = label;
}

/// <summary>Entree de liste deroulante pour la nature saisie.</summary>
public sealed class InventoryCaptureOption(InventoryCaptureKind value, string label)
{
    public InventoryCaptureKind Value { get; } = value;

    public string Label { get; } = label;
}

/// <summary>Entree de liste deroulante pour le sens d'un ajustement.</summary>
public sealed class InventoryDirectionOption(bool isIncrease, string label)
{
    public bool IsIncrease { get; } = isIncrease;

    public string Label { get; } = label;
}

/// <summary>Entree de liste deroulante pour un statut d'inventaire (valeur nulle = tous).</summary>
public sealed class InventoryCountStatusOption(InventoryCountStatus? value, string label)
{
    public InventoryCountStatus? Value { get; } = value;

    public string Label { get; } = label;
}

/// <summary>Ligne de comptage en cours de saisie, avant envoi en bloc a l'API.</summary>
public sealed class InventoryCountLineDraft(string itemCode, string designation, decimal countedQuantity)
{
    public string ItemCode { get; } = itemCode;

    public string Designation { get; } = designation;

    public decimal CountedQuantity { get; } = countedQuantity;
}

/// <summary>
/// Libelles francais du module, a source unique : la grille, les listes deroulantes et les
/// messages rendent le meme mot pour une meme valeur du domaine.
/// </summary>
public static class InventoryLabels
{
    public static string Category(StockItemCategory category)
    {
        return category switch
        {
            StockItemCategory.Alimentaire => "Alimentaire",
            StockItemCategory.Boisson => "Boisson",
            StockItemCategory.Entretien => "Entretien",
            StockItemCategory.Equipement => "Équipement",
            StockItemCategory.Autre => "Autre",
            _ => category.ToString()
        };
    }

    public static string Kind(StockMovementKind kind)
    {
        return kind switch
        {
            StockMovementKind.PurchaseEntry => "Entrée d'achat",
            StockMovementKind.Consumption => "Consommation",
            StockMovementKind.TransferOut => "Transfert sortant",
            StockMovementKind.TransferIn => "Transfert entrant",
            StockMovementKind.InventoryAdjustment => "Ajustement d'inventaire",
            _ => kind.ToString()
        };
    }
}

/// <summary>Affiche la categorie d'un article en francais dans les grilles.</summary>
public sealed class StockItemCategoryLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is StockItemCategory category ? InventoryLabels.Category(category) : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>Affiche la nature d'un mouvement en francais dans les grilles.</summary>
public sealed class StockMovementKindLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is StockMovementKind kind ? InventoryLabels.Kind(kind) : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
