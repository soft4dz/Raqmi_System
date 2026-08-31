using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using RaqmiSystem.Application.Housekeeping;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Domain.Housekeeping;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module Housekeeping et chambres : tableau des chambres, planning des equipes,
/// controle des chambres et minibar. Vue autonome : elle ne connait que le
/// ModuleViewContext que la fenetre lui prete, jamais MainWindow ni une autre vue.
///
/// Les deux axes que l'ecran croise n'ont pas le meme proprietaire, et c'est
/// volontaire : l'ETAT DE PROPRETE appartient a ce module, l'OCCUPATION est
/// deduite des reservations par le serveur. L'ecran n'en recalcule aucun des deux,
/// il affiche ce que le serveur lui repond - un compteur recalcule localement
/// finirait par contredire la grille qu'il surmonte.
///
/// L'ecran ne connait pas non plus le module Hebergement : la liste des sejours
/// facturables au minibar sort du tableau des chambres, pas d'un appel a
/// /lodging/reservations. Un profil housekeeping n'a aucune raison de detenir
/// lodging.read pour relever un minibar.
/// </summary>
public partial class HousekeepingView : UserControl
{
    private const string ReadPermission = PermissionCatalog.HousekeepingRead;

    private const string WritePermission = PermissionCatalog.HousekeepingWrite;

    // Le controle est une permission A PART : l'agent qui a nettoye la chambre ne
    // signe pas lui-meme son travail. C'est ce qui fait de l'inspection un controle
    // et non une auto-declaration.
    private const string InspectPermission = PermissionCatalog.HousekeepingInspect;

    private const string ReadPermissionHint =
        "Permission housekeeping.read requise : votre profil ne peut pas consulter le housekeeping.";

    private const string WritePermissionHint =
        "Permission housekeeping.write requise : votre profil ne peut pas modifier le housekeeping.";

    private const string InspectPermissionHint =
        "Permission housekeeping.inspect requise : seul un profil habilité contrôle une chambre.";

    private ModuleViewContext? context;

    private bool canRead = true;

    private bool canWrite = true;

    private bool canInspect = true;

    // Info-bulles d'origine des boutons, capturees avant toute substitution par le
    // message de permission : l'affectation doit rester symetrique (les vues
    // survivent a la deconnexion et resservent au profil suivant).
    private readonly Dictionary<Button, object?> originalToolTips = [];

    private IReadOnlyCollection<HotelUnitResponse> hotelUnits = [];

    // Dernieres reponses BRUTES du serveur : les actions travaillent sur elles, pas
    // sur les lignes formatees pour l'affichage.
    private RoomBoardResponse? board;

    private HousekeepingDaySheetResponse? daySheet;

    private IReadOnlyCollection<MinibarItemResponse> minibarItems = [];

    public HousekeepingView()
    {
        InitializeComponent();

        // Les StringFormat XAML doivent suivre la meme culture que le code.
        Language = XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);

        DatePicker.SelectedDate = DateTime.Today;
        UpdateActionButtons();
    }

    /// <summary>Memorise le contexte fourni par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext context)
    {
        this.context = context;
        canRead = context.HasPermission(ReadPermission);
        canWrite = context.HasPermission(WritePermission);
        canInspect = context.HasPermission(InspectPermission);

        UpdateActionButtons();
    }

    /// <summary>
    /// (Re)charge les unites puis les donnees de l'unite courante. Sort
    /// silencieusement tant qu'aucun contexte n'est disponible ou qu'aucune session
    /// n'est ouverte.
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
            await ReloadUnitsAsync();
            await ReloadAllAsync();
        });
    }

    /// <summary>Vide tout l'ecran (appelee a la deconnexion).</summary>
    public void ResetState()
    {
        hotelUnits = [];
        board = null;
        daySheet = null;
        minibarItems = [];

        UnitComboBox.ItemsSource = null;
        BoardDataGrid.ItemsSource = null;
        TasksDataGrid.ItemsSource = null;
        AttendantsDataGrid.ItemsSource = null;
        ItemsDataGrid.ItemsSource = null;
        ConsumptionsDataGrid.ItemsSource = null;
        InHouseComboBox.ItemsSource = null;

        SelectedRoomTextBlock.Text = "aucune";
        SheetSummaryTextBlock.Text = string.Empty;
        TaskCountTextBlock.Text = string.Empty;
        UnassignedTextBlock.Text = string.Empty;
        OutOfOrderReasonTextBox.Text = string.Empty;
        AttendantTextBox.Text = string.Empty;
        TaskNotesTextBox.Text = string.Empty;
        ItemCodeTextBox.Text = string.Empty;
        ItemLabelTextBox.Text = string.Empty;
        ItemPriceTextBox.Text = string.Empty;
        ConsumptionQuantityTextBox.Text = "1";

        ClearCounters();
        UpdateActionButtons();
    }

    // ---------------------------------------------------------------------- chargement

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await ReloadUnitsAsync();
            await ReloadAllAsync();
            moduleContext.SetStatus("Housekeeping actualisé.");
        });
    }

    // Une seule signature pour les deux filtres : ComboBox.SelectionChanged
    // (SelectionChangedEventHandler) et DatePicker.SelectedDateChanged
    // (EventHandler<SelectionChangedEventArgs>) acceptent la meme methode.
    private async void Filters_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Declenche pendant InitializeComponent (avant qu'un contexte existe) : sortir
        // sans rien tenter est la seule reponse correcte a ce stade.
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(ReloadAllAsync);
    }

    private async void HousekeepingTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged est un evenement route : ignorer ceux qui remontent des
        // DataGrid/ComboBox internes.
        if (!ReferenceEquals(e.OriginalSource, HousekeepingTabs))
        {
            return;
        }

        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(ReloadAllAsync);
    }

    private async Task ReloadUnitsAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        // Le code d'unite est la cle stable d'un rechargement a l'autre : la selection
        // est restauree apres coup, sinon un simple "Actualiser" ramenerait la
        // gouvernante sur un autre etablissement.
        var selectedCode = SelectedUnitCode();

        hotelUnits = await moduleContext.ApiClient.GetHotelUnitsAsync(moduleContext.ApiBaseUrl, includeInactive: false);

        var options = hotelUnits
            .Select(unit => new UnitOption(unit.Code, $"{unit.Code} — {unit.Name}"))
            .ToArray();

        UnitComboBox.ItemsSource = options;

        var restored = selectedCode is null
            ? null
            : options.FirstOrDefault(option => string.Equals(option.Code, selectedCode, StringComparison.OrdinalIgnoreCase));

        UnitComboBox.SelectedItem = restored ?? options.FirstOrDefault();
    }

    private async Task ReloadAllAsync()
    {
        await ReloadBoardAsync();
        await ReloadDaySheetAsync();
        await ReloadMinibarAsync();
        UpdateActionButtons();
    }

    private async Task ReloadBoardAsync()
    {
        var moduleContext = context;

        if (moduleContext is null || SelectedUnitCode() is not { } unitCode || SelectedDate() is not { } date)
        {
            board = null;
            BoardDataGrid.ItemsSource = null;
            ClearCounters();
            return;
        }

        board = await moduleContext.ApiClient.GetHousekeepingBoardAsync(moduleContext.ApiBaseUrl, unitCode, date);

        var selectedRoomId = (BoardDataGrid.SelectedItem as BoardRowView)?.RoomId;

        BoardDataGrid.ItemsSource = board.Rows.Select(ToBoardRow).ToArray();

        if (selectedRoomId is { } roomId &&
            BoardDataGrid.ItemsSource is IEnumerable<BoardRowView> rows &&
            rows.FirstOrDefault(row => row.RoomId == roomId) is { } restored)
        {
            BoardDataGrid.SelectedItem = restored;
        }

        // Les compteurs viennent du serveur, calcules sur les lignes qu'il renvoie :
        // ils ne peuvent donc pas contredire la grille qui les suit.
        TotalRoomsTextBlock.Text = FormatCount(board.TotalRooms);
        DirtyRoomsTextBlock.Text = FormatCount(board.DirtyRooms);
        CleanRoomsTextBlock.Text = FormatCount(board.CleanRooms);
        InspectedRoomsTextBlock.Text = FormatCount(board.InspectedRooms);
        OutOfOrderRoomsTextBlock.Text = FormatCount(board.OutOfOrderRooms);
        DeparturesTextBlock.Text = FormatCount(board.Departures + board.Turnovers);
        TurnoversTextBlock.Text = FormatCount(board.Turnovers);

        UpdateSelectedRoomLabel();
        RefreshInHouseOptions();
    }

    private async Task ReloadDaySheetAsync()
    {
        var moduleContext = context;

        if (moduleContext is null || SelectedUnitCode() is not { } unitCode || SelectedDate() is not { } date)
        {
            daySheet = null;
            TasksDataGrid.ItemsSource = null;
            AttendantsDataGrid.ItemsSource = null;
            TaskCountTextBlock.Text = string.Empty;
            UnassignedTextBlock.Text = string.Empty;
            return;
        }

        daySheet = await moduleContext.ApiClient.GetHousekeepingDaySheetAsync(moduleContext.ApiBaseUrl, unitCode, date);

        var selectedTaskId = (TasksDataGrid.SelectedItem as TaskRowView)?.Id;

        TasksDataGrid.ItemsSource = daySheet.Tasks.Select(ToTaskRow).ToArray();

        if (selectedTaskId is { } taskId &&
            TasksDataGrid.ItemsSource is IEnumerable<TaskRowView> rows &&
            rows.FirstOrDefault(row => row.Id == taskId) is { } restored)
        {
            TasksDataGrid.SelectedItem = restored;
        }

        AttendantsDataGrid.ItemsSource = daySheet.Attendants.Select(ToAttendantRow).ToArray();

        TaskCountTextBlock.Text = daySheet.TotalTasks == 1
            ? "1 tâche"
            : $"{FormatCount(daySheet.TotalTasks)} tâches";

        UnassignedTextBlock.Text = daySheet.UnassignedTasks == 0
            ? "tout est affecté"
            : $"{FormatCount(daySheet.UnassignedTasks)} non affectée(s)";
    }

    private async Task ReloadMinibarAsync()
    {
        var moduleContext = context;

        if (moduleContext is null || SelectedUnitCode() is not { } unitCode || SelectedDate() is not { } date)
        {
            minibarItems = [];
            ItemsDataGrid.ItemsSource = null;
            ConsumptionsDataGrid.ItemsSource = null;
            return;
        }

        var selectedItemId = (ItemsDataGrid.SelectedItem as ItemRowView)?.Id;

        // includeInactive: un produit retire de la carte reste visible pour pouvoir
        // etre remis, et pour expliquer les consommations passees qui le citent.
        minibarItems = await moduleContext.ApiClient.GetMinibarItemsAsync(
            moduleContext.ApiBaseUrl,
            unitCode,
            includeInactive: true);

        ItemsDataGrid.ItemsSource = minibarItems.Select(ToItemRow).ToArray();

        if (selectedItemId is { } itemId &&
            ItemsDataGrid.ItemsSource is IEnumerable<ItemRowView> rows &&
            rows.FirstOrDefault(row => row.Id == itemId) is { } restored)
        {
            ItemsDataGrid.SelectedItem = restored;
        }

        var consumptions = await moduleContext.ApiClient.GetMinibarConsumptionsAsync(
            moduleContext.ApiBaseUrl,
            date,
            date,
            unitCode,
            reservationId: null);

        ConsumptionsDataGrid.ItemsSource = consumptions.Select(ToConsumptionRow).ToArray();
    }

    // ------------------------------------------------------------------ etat des chambres

    private void BoardDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectedRoomLabel();
        UpdateActionButtons();
    }

    private async void MarkDirtyButton_Click(object sender, RoutedEventArgs e)
    {
        await SetConditionAsync(RoomConditionStatus.Dirty);
    }

    private async void MarkCleanButton_Click(object sender, RoutedEventArgs e)
    {
        await SetConditionAsync(RoomConditionStatus.Clean);
    }

    private async void MarkOutOfOrderButton_Click(object sender, RoutedEventArgs e)
    {
        await SetConditionAsync(RoomConditionStatus.OutOfOrder);
    }

    private async Task SetConditionAsync(RoomConditionStatus status)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        if (BoardDataGrid.SelectedItem is not BoardRowView row)
        {
            moduleContext.SetStatus("Sélectionnez une chambre dans le tableau.", isError: true);
            return;
        }

        var reason = OutOfOrderReasonTextBox.Text?.Trim();

        // Verification miroir de la regle serveur : un message clair ici evite un
        // aller-retour previsible, le serveur restant seul juge de la validite.
        if (status == RoomConditionStatus.OutOfOrder && string.IsNullOrWhiteSpace(reason))
        {
            moduleContext.SetStatus("Le motif est obligatoire pour mettre une chambre hors service.", isError: true);
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.SetRoomConditionAsync(
                moduleContext.ApiBaseUrl,
                row.RoomId,
                new SetRoomConditionRequest(
                    status,
                    status == RoomConditionStatus.OutOfOrder ? reason : null));

            OutOfOrderReasonTextBox.Text = string.Empty;

            await ReloadBoardAsync();
            moduleContext.SetStatus($"Chambre {row.RoomNumber} : {ConditionLabel(status).ToLowerInvariant()}.");
        });
    }

    // ------------------------------------------------------------------- planning et taches

    private void TasksDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Pre-remplir le champ agent avec l'affectation courante : reaffecter une
        // tache est frequent, la retaper ne l'est pas.
        if (TasksDataGrid.SelectedItem is TaskRowView row && !string.IsNullOrWhiteSpace(row.AssignedTo))
        {
            AttendantTextBox.Text = row.AssignedTo;
        }

        UpdateActionButtons();
    }

    private async void GenerateSheetButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        if (SelectedUnitCode() is not { } unitCode || SelectedDate() is not { } date)
        {
            moduleContext.SetStatus("Choisissez une unité et une date.", isError: true);
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var result = await moduleContext.ApiClient.GenerateHousekeepingTasksAsync(
                moduleContext.ApiBaseUrl,
                new GenerateHousekeepingTasksRequest(unitCode, date));

            // Le detail est affiche tel que le serveur le renvoie : ce qui a ete
            // preserve compte autant que ce qui a ete cree, sinon relancer la
            // generation ressemble a une reconstruction.
            SheetSummaryTextBlock.Text = string.Format(
                CultureInfo.CurrentCulture,
                "{0} tâche(s) créée(s), {1} déjà présente(s), {2} chambre(s) hors service ignorée(s).",
                result.Created,
                result.SkippedExisting,
                result.SkippedOutOfOrder);

            await ReloadBoardAsync();
            await ReloadDaySheetAsync();

            moduleContext.SetStatus(result.Created == 0
                ? "Feuille du jour déjà à jour : aucune tâche à créer."
                : $"Feuille du jour générée : {result.Created} tâche(s) créée(s).");
        });
    }

    private async void AssignTaskButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || SelectedTask() is not { } task)
        {
            context?.SetStatus("Sélectionnez une tâche dans la liste.", isError: true);
            return;
        }

        var attendant = AttendantTextBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(attendant))
        {
            moduleContext.SetStatus("Saisissez le nom de l'agent à qui affecter la tâche.", isError: true);
            return;
        }

        await RunTaskActionAsync(
            () => moduleContext.ApiClient.AssignHousekeepingTaskAsync(
                moduleContext.ApiBaseUrl,
                task.Id,
                new AssignHousekeepingTaskRequest(attendant)),
            $"Chambre {task.RoomNumber} affectée à {attendant}.");
    }

    private async void StartTaskButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || SelectedTask() is not { } task)
        {
            context?.SetStatus("Sélectionnez une tâche dans la liste.", isError: true);
            return;
        }

        await RunTaskActionAsync(
            () => moduleContext.ApiClient.StartHousekeepingTaskAsync(moduleContext.ApiBaseUrl, task.Id),
            $"Chambre {task.RoomNumber} : nettoyage démarré.");
    }

    private async void CompleteTaskButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || SelectedTask() is not { } task)
        {
            context?.SetStatus("Sélectionnez une tâche dans la liste.", isError: true);
            return;
        }

        var notes = NullIfBlank(TaskNotesTextBox.Text);

        await RunTaskActionAsync(
            () => moduleContext.ApiClient.CompleteHousekeepingTaskAsync(
                moduleContext.ApiBaseUrl,
                task.Id,
                new CompleteHousekeepingTaskRequest(notes)),
            $"Chambre {task.RoomNumber} terminée : elle attend le contrôle.");
    }

    private async void AcceptTaskButton_Click(object sender, RoutedEventArgs e)
    {
        await InspectAsync(accepted: true);
    }

    private async void RejectTaskButton_Click(object sender, RoutedEventArgs e)
    {
        await InspectAsync(accepted: false);
    }

    private async Task InspectAsync(bool accepted)
    {
        var moduleContext = context;

        if (moduleContext is null || SelectedTask() is not { } task)
        {
            context?.SetStatus("Sélectionnez une tâche dans la liste.", isError: true);
            return;
        }

        var notes = NullIfBlank(TaskNotesTextBox.Text);

        if (!accepted && notes is null)
        {
            moduleContext.SetStatus("Le motif est obligatoire pour refuser une chambre.", isError: true);
            return;
        }

        await RunTaskActionAsync(
            () => moduleContext.ApiClient.InspectHousekeepingTaskAsync(
                moduleContext.ApiBaseUrl,
                task.Id,
                new InspectHousekeepingTaskRequest(accepted, notes)),
            accepted
                ? $"Chambre {task.RoomNumber} contrôlée et acceptée."
                : $"Chambre {task.RoomNumber} refusée : elle repasse au nettoyage.");
    }

    private async void CancelTaskButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || SelectedTask() is not { } task)
        {
            context?.SetStatus("Sélectionnez une tâche dans la liste.", isError: true);
            return;
        }

        var reason = NullIfBlank(TaskNotesTextBox.Text);

        if (reason is null)
        {
            moduleContext.SetStatus(
                "Saisissez le motif d'annulation dans le champ Observation.",
                isError: true);
            return;
        }

        await RunTaskActionAsync(
            () => moduleContext.ApiClient.CancelHousekeepingTaskAsync(
                moduleContext.ApiBaseUrl,
                task.Id,
                new CancelHousekeepingTaskRequest(reason)),
            $"Tâche de la chambre {task.RoomNumber} annulée.");
    }

    /// <summary>
    /// Execute une action de cycle de tache puis recharge la feuille ET le tableau :
    /// une transition change aussi l'etat de la chambre, les deux ecrans doivent dire
    /// la meme chose au meme moment.
    /// </summary>
    private async Task RunTaskActionAsync(Func<Task> action, string successMessage)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await action();

            TaskNotesTextBox.Text = string.Empty;

            await ReloadDaySheetAsync();
            await ReloadBoardAsync();

            moduleContext.SetStatus(successMessage);
        });
    }

    // ---------------------------------------------------------------------------- minibar

    private void ItemsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ItemsDataGrid.SelectedItem is ItemRowView row)
        {
            ItemCodeTextBox.Text = row.Code;
            ItemLabelTextBox.Text = row.Label;
            ItemPriceTextBox.Text = row.UnitPrice.ToString("0.##", CultureInfo.CurrentCulture);
        }

        UpdateActionButtons();
    }

    private async void CreateItemButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        if (SelectedUnitCode() is not { } unitCode)
        {
            moduleContext.SetStatus("Choisissez une unité hôtelière.", isError: true);
            return;
        }

        var code = NullIfBlank(ItemCodeTextBox.Text);
        var label = NullIfBlank(ItemLabelTextBox.Text);

        if (code is null || label is null)
        {
            moduleContext.SetStatus("Le code et le libellé du produit sont requis.", isError: true);
            return;
        }

        if (!TryReadPrice(out var price))
        {
            moduleContext.SetStatus("Le prix unitaire doit être un montant strictement positif.", isError: true);
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.CreateMinibarItemAsync(
                moduleContext.ApiBaseUrl,
                new CreateMinibarItemRequest(unitCode, code, label, price));

            await ReloadMinibarAsync();
            moduleContext.SetStatus($"Produit {code} ajouté à la carte.");
        });
    }

    private async void UpdateItemButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || ItemsDataGrid.SelectedItem is not ItemRowView row)
        {
            context?.SetStatus("Sélectionnez un produit de la carte.", isError: true);
            return;
        }

        var label = NullIfBlank(ItemLabelTextBox.Text);

        if (label is null)
        {
            moduleContext.SetStatus("Le libellé du produit est requis.", isError: true);
            return;
        }

        if (!TryReadPrice(out var price))
        {
            moduleContext.SetStatus("Le prix unitaire doit être un montant strictement positif.", isError: true);
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.UpdateMinibarItemAsync(
                moduleContext.ApiBaseUrl,
                row.Id,
                new UpdateMinibarItemRequest(label, price));

            await ReloadMinibarAsync();

            // Dit explicitement ce que la revision NE fait PAS : les consommations
            // deja enregistrees gardent le prix fige au moment de leur saisie.
            moduleContext.SetStatus(
                $"Produit {row.Code} modifié. Les consommations déjà enregistrées gardent leur prix d'origine.");
        });
    }

    private async void ToggleItemButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || ItemsDataGrid.SelectedItem is not ItemRowView row)
        {
            context?.SetStatus("Sélectionnez un produit de la carte.", isError: true);
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.SetMinibarItemActiveAsync(
                moduleContext.ApiBaseUrl,
                row.Id,
                !row.IsActive);

            await ReloadMinibarAsync();

            moduleContext.SetStatus(row.IsActive
                ? $"Produit {row.Code} retiré de la carte."
                : $"Produit {row.Code} remis à la carte.");
        });
    }

    private async void RecordConsumptionButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        if (ItemsDataGrid.SelectedItem is not ItemRowView item)
        {
            moduleContext.SetStatus("Sélectionnez le produit consommé dans la carte.", isError: true);
            return;
        }

        if (InHouseComboBox.SelectedItem is not InHouseOption stay)
        {
            moduleContext.SetStatus("Sélectionnez la chambre occupée à facturer.", isError: true);
            return;
        }

        if (!int.TryParse(
                ConsumptionQuantityTextBox.Text?.Trim(),
                NumberStyles.Integer,
                CultureInfo.CurrentCulture,
                out var quantity) || quantity <= 0)
        {
            moduleContext.SetStatus("La quantité doit être un entier strictement positif.", isError: true);
            return;
        }

        var date = SelectedDate();

        await moduleContext.RunAsync(async () =>
        {
            var consumption = await moduleContext.ApiClient.RecordMinibarConsumptionAsync(
                moduleContext.ApiBaseUrl,
                new RecordMinibarConsumptionRequest(
                    stay.ReservationId,
                    item.Code,
                    quantity,
                    date));

            ConsumptionQuantityTextBox.Text = "1";

            await ReloadMinibarAsync();

            moduleContext.SetStatus(string.Format(
                CultureInfo.CurrentCulture,
                "Consommation portée au folio de la chambre {0} : {1:N2}.",
                consumption.RoomNumber,
                consumption.TotalAmount));
        });
    }

    /// <summary>
    /// Les sejours facturables sortent du TABLEAU deja charge, pas d'un appel au
    /// module Hebergement : une chambre occupee ou liberee dans la journee a un
    /// sejour, et le serveur refusera clairement celui qui n'est pas en cours.
    /// </summary>
    private void RefreshInHouseOptions()
    {
        var selectedReservationId = (InHouseComboBox.SelectedItem as InHouseOption)?.ReservationId;

        var options = board is null
            ? []
            : board.Rows
                .Where(row => row.ReservationId is not null
                    && row.OccupancyState is RoomOccupancyState.Occupied
                        or RoomOccupancyState.Departure
                        or RoomOccupancyState.Turnover)
                .Select(row => new InHouseOption(
                    row.ReservationId!.Value,
                    row.RoomNumber,
                    $"{row.RoomNumber} — {row.CustomerCode}"))
                .ToArray();

        InHouseComboBox.ItemsSource = options;

        var restored = selectedReservationId is { } reservationId
            ? options.FirstOrDefault(option => option.ReservationId == reservationId)
            : null;

        InHouseComboBox.SelectedItem = restored ?? options.FirstOrDefault();
    }

    // ------------------------------------------------------------------------- etat UI

    private void UpdateActionButtons()
    {
        var hasRoom = BoardDataGrid.SelectedItem is BoardRowView;
        var task = SelectedTask();
        var hasItem = ItemsDataGrid.SelectedItem is ItemRowView;
        var hasUnit = SelectedUnitCode() is not null;

        MarkDirtyButton.IsEnabled = canWrite && hasRoom;
        MarkCleanButton.IsEnabled = canWrite && hasRoom;
        MarkOutOfOrderButton.IsEnabled = canWrite && hasRoom;

        GenerateSheetButton.IsEnabled = canWrite && hasUnit;

        // Les actions de cycle suivent l'etat de la tache : proposer "Terminer" sur une
        // tache non demarree ferait decouvrir le refus apres coup.
        AssignTaskButton.IsEnabled = canWrite && task is { IsClosed: false };
        StartTaskButton.IsEnabled = canWrite && task is { CanStart: true };
        CompleteTaskButton.IsEnabled = canWrite && task is { CanComplete: true };
        CancelTaskButton.IsEnabled = canWrite && task is { IsClosed: false };
        AcceptTaskButton.IsEnabled = canInspect && task is { CanInspect: true };
        RejectTaskButton.IsEnabled = canInspect && task is { CanInspect: true };

        CreateItemButton.IsEnabled = canWrite && hasUnit;
        UpdateItemButton.IsEnabled = canWrite && hasItem;
        ToggleItemButton.IsEnabled = canWrite && hasItem;
        RecordConsumptionButton.IsEnabled = canWrite && hasItem && InHouseComboBox.SelectedItem is InHouseOption;

        var writeHint = canRead ? WritePermissionHint : ReadPermissionHint;

        ApplyPermissionHint(MarkDirtyButton, canWrite, writeHint);
        ApplyPermissionHint(MarkCleanButton, canWrite, writeHint);
        ApplyPermissionHint(MarkOutOfOrderButton, canWrite, writeHint);
        ApplyPermissionHint(GenerateSheetButton, canWrite, writeHint);
        ApplyPermissionHint(AssignTaskButton, canWrite, writeHint);
        ApplyPermissionHint(StartTaskButton, canWrite, writeHint);
        ApplyPermissionHint(CompleteTaskButton, canWrite, writeHint);
        ApplyPermissionHint(CancelTaskButton, canWrite, writeHint);
        ApplyPermissionHint(CreateItemButton, canWrite, writeHint);
        ApplyPermissionHint(UpdateItemButton, canWrite, writeHint);
        ApplyPermissionHint(ToggleItemButton, canWrite, writeHint);
        ApplyPermissionHint(RecordConsumptionButton, canWrite, writeHint);
        ApplyPermissionHint(AcceptTaskButton, canInspect, InspectPermissionHint);
        ApplyPermissionHint(RejectTaskButton, canInspect, InspectPermissionHint);
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

    private void UpdateSelectedRoomLabel()
    {
        SelectedRoomTextBlock.Text = BoardDataGrid.SelectedItem is BoardRowView row
            ? $"{row.RoomNumber} — {row.ConditionLabel}"
            : "aucune";
    }

    private void ClearCounters()
    {
        TotalRoomsTextBlock.Text = "—";
        DirtyRoomsTextBlock.Text = "—";
        CleanRoomsTextBlock.Text = "—";
        InspectedRoomsTextBlock.Text = "—";
        OutOfOrderRoomsTextBlock.Text = "—";
        DeparturesTextBlock.Text = "—";
        TurnoversTextBlock.Text = "—";
    }

    private string? SelectedUnitCode() => (UnitComboBox.SelectedItem as UnitOption)?.Code;

    private DateOnly? SelectedDate() => DatePicker.SelectedDate is { } date
        ? DateOnly.FromDateTime(date)
        : null;

    private TaskRowView? SelectedTask() => TasksDataGrid.SelectedItem as TaskRowView;

    private bool TryReadPrice(out decimal price)
    {
        return decimal.TryParse(
                ItemPriceTextBox.Text?.Trim(),
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out price)
            && price > 0;
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string FormatCount(int value) => value.ToString("#,0", CultureInfo.CurrentCulture);

    // ------------------------------------------------------------------------ projections

    private static BoardRowView ToBoardRow(RoomBoardRow row)
    {
        return new BoardRowView(
            row.RoomId,
            row.RoomNumber,
            row.RoomTypeCode,
            row.Floor,
            // Une chambre dont personne n'a jamais rien declare est PRESUMEE propre :
            // le libelle le dit, plutot que d'afficher un service qui n'a pas eu lieu.
            row.ConditionRecorded ? ConditionLabel(row.ConditionStatus) : "Propre (présumée)",
            OccupancyLabel(row.OccupancyState),
            row.CustomerCode,
            row.TaskType is { } taskType
                ? $"{TaskTypeLabel(taskType)} — {TaskStatusLabel(row.TaskStatus!.Value)}"
                : string.Empty,
            row.TaskAssignedTo,
            row.LastCleanedAt is { } cleanedAt
                ? cleanedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture)
                : string.Empty);
    }

    private static TaskRowView ToTaskRow(HousekeepingTaskResponse task)
    {
        return new TaskRowView(
            task.Id,
            task.RoomNumber,
            TaskTypeLabel(task.TaskType),
            TaskStatusLabel(task.Status),
            task.AssignedTo,
            task.DurationMinutes?.ToString("#,0", CultureInfo.CurrentCulture) ?? string.Empty,
            BuildInspectionLabel(task),
            task.Status is HousekeepingTaskStatus.Inspected or HousekeepingTaskStatus.Cancelled,
            // Demarrer exige une affectation : c'est la regle du domaine, l'ecran ne
            // fait que ne pas proposer ce que le serveur refuserait.
            task.Status is HousekeepingTaskStatus.Pending or HousekeepingTaskStatus.Rejected
                && !string.IsNullOrWhiteSpace(task.AssignedTo),
            task.Status == HousekeepingTaskStatus.InProgress,
            task.Status == HousekeepingTaskStatus.Cleaned);
    }

    private static string BuildInspectionLabel(HousekeepingTaskResponse task)
    {
        if (task.Status == HousekeepingTaskStatus.Cancelled)
        {
            return task.CancelReason ?? string.Empty;
        }

        if (task.InspectedAt is not { } inspectedAt)
        {
            return string.Empty;
        }

        var stamp = inspectedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);

        return string.IsNullOrWhiteSpace(task.InspectionNotes)
            ? stamp
            : $"{stamp} — {task.InspectionNotes}";
    }

    private static AttendantRowView ToAttendantRow(HousekeepingAttendantLoad load)
    {
        return new AttendantRowView(
            load.AssignedTo,
            load.TaskCount,
            // "Reste" = ce qui n'est pas encore signe : le chiffre qu'une gouvernante
            // regarde pour rebasculer une chambre d'un agent a un autre.
            load.Pending + load.InProgress + load.AwaitingInspection + load.Rejected,
            load.TotalMinutes);
    }

    private static ItemRowView ToItemRow(MinibarItemResponse item)
    {
        return new ItemRowView(
            item.Id,
            item.Code,
            item.Label,
            item.UnitPrice,
            item.UnitPrice.ToString("N2", CultureInfo.CurrentCulture),
            item.IsActive,
            item.IsActive ? "À la carte" : "Retiré");
    }

    private static ConsumptionRowView ToConsumptionRow(MinibarConsumptionResponse consumption)
    {
        return new ConsumptionRowView(
            consumption.ConsumedOn.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture),
            consumption.RoomNumber,
            consumption.ItemLabel,
            consumption.Quantity,
            consumption.UnitPrice.ToString("N2", CultureInfo.CurrentCulture),
            consumption.TotalAmount.ToString("N2", CultureInfo.CurrentCulture),
            consumption.CreatedBy);
    }

    private static string ConditionLabel(RoomConditionStatus status) => status switch
    {
        RoomConditionStatus.Clean => "Propre",
        RoomConditionStatus.Dirty => "À nettoyer",
        RoomConditionStatus.Inspected => "Contrôlée",
        _ => "Hors service"
    };

    private static string OccupancyLabel(RoomOccupancyState state) => state switch
    {
        RoomOccupancyState.Occupied => "Occupée",
        RoomOccupancyState.Departure => "Départ",
        RoomOccupancyState.Arrival => "Arrivée",
        RoomOccupancyState.Turnover => "Départ + arrivée",
        _ => "Libre"
    };

    private static string TaskTypeLabel(HousekeepingTaskType type) => type switch
    {
        HousekeepingTaskType.Departure => "Recouche à blanc",
        HousekeepingTaskType.Stayover => "Recouche",
        HousekeepingTaskType.Vacant => "Rafraîchissement",
        _ => "Nettoyage à fond"
    };

    private static string TaskStatusLabel(HousekeepingTaskStatus status) => status switch
    {
        HousekeepingTaskStatus.Pending => "À faire",
        HousekeepingTaskStatus.InProgress => "En cours",
        HousekeepingTaskStatus.Cleaned => "À contrôler",
        HousekeepingTaskStatus.Inspected => "Contrôlée",
        HousekeepingTaskStatus.Rejected => "Refusée",
        _ => "Annulée"
    };

    private sealed record UnitOption(string Code, string Label);

    private sealed record InHouseOption(Guid ReservationId, string RoomNumber, string Label);

    private sealed record BoardRowView(
        Guid RoomId,
        string RoomNumber,
        string RoomTypeCode,
        string? Floor,
        string ConditionLabel,
        string OccupancyLabel,
        string? CustomerCode,
        string TaskLabel,
        string? TaskAssignedTo,
        string LastCleanedLabel);

    private sealed record TaskRowView(
        Guid Id,
        string RoomNumber,
        string TaskTypeLabel,
        string StatusLabel,
        string? AssignedTo,
        string DurationLabel,
        string InspectionLabel,
        bool IsClosed,
        bool CanStart,
        bool CanComplete,
        bool CanInspect);

    private sealed record AttendantRowView(
        string AssignedTo,
        int TaskCount,
        int Remaining,
        int TotalMinutes);

    private sealed record ItemRowView(
        Guid Id,
        string Code,
        string Label,
        decimal UnitPrice,
        string PriceLabel,
        bool IsActive,
        string StatusLabel);

    private sealed record ConsumptionRowView(
        string ConsumedOnLabel,
        string RoomNumber,
        string ItemLabel,
        int Quantity,
        string UnitPriceLabel,
        string TotalLabel,
        string CreatedBy);
}
