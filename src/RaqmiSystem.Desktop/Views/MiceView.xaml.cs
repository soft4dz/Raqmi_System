using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Mice;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Mice;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module 10.6 - Groupes et MICE, volet evenementiel.
///
/// PERIMETRE PARTIEL ET DIT A L'ECRAN : salles, evenements, devis, BEO et facturation. Les
/// allotements et les rooming lists portent sur les CHAMBRES et ne sont pas ici - un bloc de
/// chambres doit etre retire de la disponibilite, sans quoi l'hotel survendrait sans s'en
/// apercevoir. L'encart de bas de page l'annonce plutot que de le taire.
///
/// Regle de vocabulaire tenue partout : une salle est occupee MONTAGE ET DEMONTAGE COMPRIS. La
/// colonne "Occupation salle" montre cette fenetre reelle, qui differe des horaires invites et qui
/// est la seule que le serveur compare pour detecter un conflit.
/// Vue autonome : elle ne connait que le ModuleViewContext que la fenetre lui prete.
/// </summary>
public partial class MiceView : UserControl
{
    private const string WritePermission = PermissionCatalog.MiceWrite;

    private const string WritePermissionHint =
        "Permission mice.write requise pour agir sur les salles et les événements.";

    private const string InvoicePermissionHint =
        "Permissions mice.write ET invoices.write requises : facturer un événement crée une facture réelle.";

    private ModuleViewContext? context;

    private bool canWrite;

    private bool canInvoice;

    private readonly ObservableCollection<QuoteLineRow> quoteLines = [];

    private readonly ObservableCollection<BeoRow> beoRows = [];

    private IReadOnlyList<HotelUnitResponse> units = [];

    private IReadOnlyList<CustomerResponse> customers = [];

    private IReadOnlyList<FunctionSpaceResponse> spaces = [];

    // Types de chambre : le volet groupes en a besoin pour poser un bloc sur un type donne.
    private IReadOnlyList<RoomTypeResponse> roomTypes = [];

    private EventBookingResponse? selectedEvent;

    // Info-bulles d'origine, capturees avant toute substitution par le message de permission :
    // l'affectation doit rester symetrique (charte UI).
    private readonly Dictionary<Button, object?> originalToolTips = [];

    public MiceView()
    {
        InitializeComponent();

        QuoteLinesDataGrid.ItemsSource = quoteLines;
        BeoDataGrid.ItemsSource = beoRows;

        EventSetupStyleComboBox.ItemsSource = Enum.GetValues<EventSetupStyle>()
            .Select(style => new SetupStyleOption(style, DescribeSetupStyle(style)))
            .ToList();

        EventSetupStyleComboBox.DisplayMemberPath = nameof(SetupStyleOption.Label);
        EventSetupStyleComboBox.SelectedIndex = 0;

        UpdateActionButtons();
    }

    /// <summary>Memorise le contexte fourni par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext context)
    {
        this.context = context;
        canWrite = context.HasPermission(WritePermission);

        // Facturer ecrit une facture reelle : le serveur exige les deux droits, l'ecran grise donc
        // le bouton selon la meme regle plutot que de laisser l'utilisateur decouvrir un 403.
        canInvoice = canWrite && context.HasPermission(PermissionCatalog.InvoicesWrite);

        UpdateActionButtons();
    }

    /// <summary>(Re)charge referentiels et evenements. Silencieux hors session.</summary>
    public async Task LoadAsync()
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await LoadReferenceDataAsync();
            await ReloadAsync();
        });
    }

    /// <summary>Vide tout (appelee a la deconnexion).</summary>
    public void ResetState()
    {
        units = [];
        customers = [];
        spaces = [];
        roomTypes = [];
        allotments = [];
        selectedEvent = null;
        selectedAllotment = null;

        UnitComboBox.ItemsSource = null;
        EventCustomerComboBox.ItemsSource = null;
        EventSpaceComboBox.ItemsSource = null;
        SpacesDataGrid.ItemsSource = null;
        EventsDataGrid.ItemsSource = null;
        AllotmentsDataGrid.ItemsSource = null;
        RoomingListDataGrid.ItemsSource = null;

        quoteLines.Clear();
        beoRows.Clear();

        EventCountTextBlock.Text = "—";
        DraftCountTextBlock.Text = "—";
        ConfirmedAmountTextBlock.Text = "—";
        SpaceCountTextBlock.Text = "—";
        EventsCaptionTextBlock.Text = string.Empty;
        QuoteTotalsTextBlock.Text = string.Empty;

        DetailTitleTextBlock.Text = "Détail de l'événement";
        DetailSubtitleTextBlock.Text = "Sélectionnez un événement pour ouvrir son devis et son BEO.";

        UpdateActionButtons();
    }

    // ------------------------------------ Chargement ------------------------------------

    private async Task LoadReferenceDataAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        units = (await moduleContext.ApiClient.GetHotelUnitsAsync(moduleContext.ApiBaseUrl, includeInactive: false))
            .ToList();

        customers = (await moduleContext.ApiClient.GetCustomersAsync(moduleContext.ApiBaseUrl, search: null, includeInactive: false))
            .ToList();

        roomTypes = (await moduleContext.ApiClient.GetRoomTypesAsync(moduleContext.ApiBaseUrl, hotelUnitCode: null, includeInactive: false))
            .ToList();

        var previousUnit = SelectedUnitCode;

        UnitComboBox.ItemsSource = units;
        EventCustomerComboBox.ItemsSource = customers;

        if (units.Count > 0)
        {
            var index = previousUnit is null
                ? 0
                : Math.Max(0, units.ToList().FindIndex(unit => unit.Code == previousUnit));

            UnitComboBox.SelectedIndex = index;
        }

        FromDatePicker.SelectedDate ??= DateTime.Today;
        ToDatePicker.SelectedDate ??= DateTime.Today.AddMonths(3);
    }

    private async Task ReloadAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var unitCode = SelectedUnitCode;

        spaces = (await moduleContext.ApiClient.GetFunctionSpacesAsync(
            moduleContext.ApiBaseUrl,
            unitCode,
            includeInactive: true)).ToList();

        var events = await moduleContext.ApiClient.GetEventsAsync(
            moduleContext.ApiBaseUrl,
            unitCode,
            FromDatePicker.SelectedDate is { } from ? DateOnly.FromDateTime(from) : null,
            ToDatePicker.SelectedDate is { } to ? DateOnly.FromDateTime(to) : null,
            functionSpaceCode: null,
            includeCancelled: IncludeCancelledCheckBox.IsChecked == true);

        RenderSpaces();
        RenderEvents(events);

        // Volet groupes : voir MiceView.Allotments.cs.
        await ReloadAllotmentsAsync();
    }

    private void RenderSpaces()
    {
        SpacesDataGrid.ItemsSource = spaces.Select(space => new SpaceRow(space)).ToList();

        // Le formulaire de creation d'evenement ne propose que les salles ACTIVES : une salle
        // archivee reste visible dans la liste mais n'accepte plus de nouvel evenement.
        EventSpaceComboBox.ItemsSource = spaces.Where(space => space.IsActive).ToList();

        var active = spaces.Count(space => space.IsActive);
        SpaceCountTextBlock.Text = $"{active} / {spaces.Count}";
    }

    private void RenderEvents(IReadOnlyCollection<EventBookingResponse> events)
    {
        var rows = events.Select(item => new EventRow(item)).ToList();

        EventsDataGrid.ItemsSource = rows;

        EventCountTextBlock.Text = rows.Count.ToString(CultureInfo.CurrentCulture);

        DraftCountTextBlock.Text = events
            .Count(item => item.Status == nameof(EventBookingStatus.Draft))
            .ToString(CultureInfo.CurrentCulture);

        var confirmed = events
            .Where(item => item.Status == nameof(EventBookingStatus.Confirmed))
            .Sum(item => item.TotalExclVat);

        ConfirmedAmountTextBlock.Text = confirmed.ToString("N2", CultureInfo.CurrentCulture);

        EventsCaptionTextBlock.Text = rows.Count == 0
            ? string.Empty
            : $"{rows.Count} événement(s)";

        // La selection precedente est retrouvee par identifiant : recharger apres une action ne
        // doit pas refermer le detail que l'utilisateur avait ouvert.
        if (selectedEvent is { } previous)
        {
            var match = events.FirstOrDefault(item => item.Id == previous.Id);

            if (match is not null)
            {
                EventsDataGrid.SelectedItem = rows.FirstOrDefault(row => row.Id == match.Id);
                ShowDetail(match);
                return;
            }
        }

        ShowDetail(null);
    }

    private void ShowDetail(EventBookingResponse? booking)
    {
        selectedEvent = booking;

        quoteLines.Clear();
        beoRows.Clear();

        if (booking is null)
        {
            DetailTitleTextBlock.Text = "Détail de l'événement";
            DetailSubtitleTextBlock.Text = "Sélectionnez un événement pour ouvrir son devis et son BEO.";
            QuoteTotalsTextBlock.Text = string.Empty;
            UpdateActionButtons();
            return;
        }

        DetailTitleTextBlock.Text = $"{booking.Reference} — {booking.Title}";

        var invoiceNote = booking.InvoiceId is null
            ? "non facturé"
            : $"facturé ({booking.InvoiceNumber ?? "brouillon"})";

        DetailSubtitleTextBlock.Text =
            $"{booking.FunctionSpaceLabel} · {booking.CustomerName} · "
            + $"{booking.EventDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)} "
            + $"{booking.StartTime:HH\\:mm} · {booking.ExpectedAttendance} pers. · "
            + $"occupation {booking.OccupiedFrom:HH\\:mm}–{booking.OccupiedTo:HH\\:mm} · {invoiceNote}";

        foreach (var line in booking.Lines)
        {
            quoteLines.Add(new QuoteLineRow
            {
                Designation = line.Designation,
                Quantity = line.Quantity.ToString("0.##", CultureInfo.CurrentCulture),
                UnitPrice = line.UnitPrice.ToString("0.##", CultureInfo.CurrentCulture),
                VatRate = line.VatRate.ToString("0.##", CultureInfo.CurrentCulture)
            });
        }

        foreach (var item in booking.Schedule)
        {
            beoRows.Add(new BeoRow
            {
                StartTime = item.StartTime.ToString("HH\\:mm", CultureInfo.CurrentCulture),
                Description = item.Description,
                Department = item.Department ?? string.Empty
            });
        }

        QuoteTotalsTextBlock.Text =
            $"HT {booking.TotalExclVat:N2} · TVA {booking.TotalVat:N2} · TTC {booking.TotalInclVat:N2}";

        UpdateActionButtons();
    }

    // ------------------------------------ Evenements ------------------------------------

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await LoadReferenceDataAsync();
            await ReloadAsync();
            moduleContext.SetStatus("Événementiel actualisé.");
        });
    }

    private async void Filter_Changed(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(ReloadAsync);
    }

    private void EventsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EventsDataGrid.SelectedItem is EventRow row)
        {
            ShowDetail(row.Source);
        }
    }

    private void SpacesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SpacesDataGrid.SelectedItem is not SpaceRow row)
        {
            return;
        }

        SpaceCodeTextBox.Text = row.Code;
        SpaceLabelTextBox.Text = row.Label;
        SpaceCapacityTextBox.Text = row.MaxAttendance.ToString(CultureInfo.CurrentCulture);
    }

    private async void CreateEventButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !canWrite)
        {
            return;
        }

        if (SelectedUnitCode is not { } unitCode)
        {
            moduleContext.SetStatus("Sélectionnez une unité.", isError: true);
            return;
        }

        if (EventSpaceComboBox.SelectedItem is not FunctionSpaceResponse space)
        {
            moduleContext.SetStatus("Sélectionnez une salle.", isError: true);
            return;
        }

        if (EventCustomerComboBox.SelectedItem is not CustomerResponse customer)
        {
            moduleContext.SetStatus("Sélectionnez un client.", isError: true);
            return;
        }

        if (EventDatePicker.SelectedDate is not { } eventDate)
        {
            moduleContext.SetStatus("Sélectionnez la date de l'événement.", isError: true);
            return;
        }

        if (!TryReadTime(EventStartTextBox.Text, out var startTime))
        {
            moduleContext.SetStatus("Heure de début invalide : utilisez le format HH:mm.", isError: true);
            return;
        }

        if (!TryReadPositiveInt(EventDurationTextBox.Text, out var duration)
            || !TryReadNonNegativeInt(EventSetupTextBox.Text, out var setup)
            || !TryReadNonNegativeInt(EventTeardownTextBox.Text, out var teardown)
            || !TryReadPositiveInt(EventAttendanceTextBox.Text, out var attendance))
        {
            moduleContext.SetStatus(
                "Durée, montage, démontage et effectif doivent être des nombres de minutes ou de personnes.",
                isError: true);
            return;
        }

        var style = EventSetupStyleComboBox.SelectedItem is SetupStyleOption option
            ? option.Style
            : EventSetupStyle.Theatre;

        await moduleContext.RunAsync(async () =>
        {
            var created = await moduleContext.ApiClient.CreateEventAsync(
                moduleContext.ApiBaseUrl,
                new CreateEventBookingRequest(
                    unitCode,
                    EventReferenceTextBox.Text,
                    space.Code,
                    customer.Code,
                    EventTitleTextBox.Text,
                    DateOnly.FromDateTime(eventDate),
                    startTime,
                    duration,
                    setup,
                    teardown,
                    style.ToString(),
                    attendance,
                    null));

            selectedEvent = created;

            EventReferenceTextBox.Clear();
            EventTitleTextBox.Clear();

            await ReloadAsync();
            moduleContext.SetStatus($"Événement {created.Reference} créé (devis, la salle est tenue).");
        });
    }

    private async void ConfirmEventButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !canWrite || selectedEvent is not { } booking)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.ConfirmEventAsync(moduleContext.ApiBaseUrl, booking.Id);
            await ReloadAsync();
            moduleContext.SetStatus($"Événement {booking.Reference} confirmé.");
        });
    }

    private async void CancelEventButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !canWrite || selectedEvent is not { } booking)
        {
            return;
        }

        // Acte engageant : confirmation dans la fenetre proprietaire, defaut sur Non.
        var confirmed = Confirm(
            $"Annuler définitivement l'événement {booking.Reference} ({booking.Title}) ?\n"
            + "La salle sera libérée pour d'autres ventes.",
            "Annuler l'événement");

        if (!confirmed)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.CancelEventAsync(
                moduleContext.ApiBaseUrl,
                booking.Id,
                new CancelEventBookingRequest("Annulé depuis l'écran événementiel"));

            await ReloadAsync();
            moduleContext.SetStatus($"Événement {booking.Reference} annulé, la salle est libérée.");
        });
    }

    private async void InvoiceEventButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !canInvoice || selectedEvent is not { } booking)
        {
            return;
        }

        var confirmed = Confirm(
            $"Générer la facture de l'événement {booking.Reference} ?\n"
            + $"Total HT {booking.TotalExclVat:N2} · TTC {booking.TotalInclVat:N2}.\n"
            + "Le devis sera figé : les lignes ne pourront plus être modifiées.",
            "Facturer l'événement");

        if (!confirmed)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var invoiced = await moduleContext.ApiClient.InvoiceEventAsync(moduleContext.ApiBaseUrl, booking.Id);
            await ReloadAsync();

            moduleContext.SetStatus(
                $"Facture brouillon créée pour {invoiced.Reference}. Émettez-la depuis le module Facturation.");
        });
    }

    // ------------------------------------ Devis et BEO ------------------------------------

    private void AddQuoteLineButton_Click(object sender, RoutedEventArgs e)
    {
        if (!canWrite)
        {
            return;
        }

        quoteLines.Add(new QuoteLineRow
        {
            Designation = string.Empty,
            Quantity = "1",
            UnitPrice = "0",
            VatRate = "19"
        });
    }

    private void RemoveQuoteLineButton_Click(object sender, RoutedEventArgs e)
    {
        if (!canWrite || QuoteLinesDataGrid.SelectedItem is not QuoteLineRow row)
        {
            return;
        }

        quoteLines.Remove(row);
    }

    private async void SaveQuoteButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !canWrite || selectedEvent is not { } booking)
        {
            return;
        }

        QuoteLinesDataGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

        var payload = new List<EventBookingLineRequest>(quoteLines.Count);

        foreach (var row in quoteLines)
        {
            if (string.IsNullOrWhiteSpace(row.Designation))
            {
                moduleContext.SetStatus("Chaque ligne du devis doit porter une désignation.", isError: true);
                return;
            }

            if (!TryReadDecimal(row.Quantity, out var quantity)
                || !TryReadDecimal(row.UnitPrice, out var unitPrice)
                || !TryReadDecimal(row.VatRate, out var vatRate))
            {
                moduleContext.SetStatus(
                    $"Ligne « {row.Designation} » : quantité, prix et TVA doivent être numériques.",
                    isError: true);
                return;
            }

            payload.Add(new EventBookingLineRequest(row.Designation, quantity, unitPrice, vatRate));
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.ReplaceEventLinesAsync(moduleContext.ApiBaseUrl, booking.Id, payload);
            await ReloadAsync();
            moduleContext.SetStatus("Devis enregistré.");
        });
    }

    private void AddBeoLineButton_Click(object sender, RoutedEventArgs e)
    {
        if (!canWrite)
        {
            return;
        }

        beoRows.Add(new BeoRow
        {
            StartTime = "08:00",
            Description = string.Empty,
            Department = string.Empty
        });
    }

    private void RemoveBeoLineButton_Click(object sender, RoutedEventArgs e)
    {
        if (!canWrite || BeoDataGrid.SelectedItem is not BeoRow row)
        {
            return;
        }

        beoRows.Remove(row);
    }

    private async void SaveBeoButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !canWrite || selectedEvent is not { } booking)
        {
            return;
        }

        BeoDataGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

        var payload = new List<EventScheduleItemRequest>(beoRows.Count);

        foreach (var row in beoRows)
        {
            if (!TryReadTime(row.StartTime, out var time))
            {
                moduleContext.SetStatus($"Heure invalide : « {row.StartTime} ». Format attendu HH:mm.", isError: true);
                return;
            }

            if (string.IsNullOrWhiteSpace(row.Description))
            {
                moduleContext.SetStatus("Chaque étape du BEO doit porter une description.", isError: true);
                return;
            }

            payload.Add(new EventScheduleItemRequest(
                time,
                row.Description,
                string.IsNullOrWhiteSpace(row.Department) ? null : row.Department));
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.ReplaceEventScheduleAsync(moduleContext.ApiBaseUrl, booking.Id, payload);
            await ReloadAsync();
            moduleContext.SetStatus("Déroulé BEO enregistré.");
        });
    }

    // ------------------------------------ Salles ------------------------------------

    private async void CreateSpaceButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveSpaceAsync(isCreation: true);
    }

    private async void UpdateSpaceButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveSpaceAsync(isCreation: false);
    }

    private async Task SaveSpaceAsync(bool isCreation)
    {
        var moduleContext = context;

        if (moduleContext is null || !canWrite)
        {
            return;
        }

        if (SelectedUnitCode is not { } unitCode)
        {
            moduleContext.SetStatus("Sélectionnez une unité.", isError: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(SpaceCodeTextBox.Text) || string.IsNullOrWhiteSpace(SpaceLabelTextBox.Text))
        {
            moduleContext.SetStatus("Le code et le nom de la salle sont requis.", isError: true);
            return;
        }

        if (!TryReadPositiveInt(SpaceCapacityTextBox.Text, out var capacity))
        {
            moduleContext.SetStatus("La capacité doit être un nombre de personnes strictement positif.", isError: true);
            return;
        }

        var request = new SaveFunctionSpaceRequest(SpaceLabelTextBox.Text, capacity, null, null);
        var code = SpaceCodeTextBox.Text;

        await moduleContext.RunAsync(async () =>
        {
            if (isCreation)
            {
                await moduleContext.ApiClient.CreateFunctionSpaceAsync(moduleContext.ApiBaseUrl, unitCode, code, request);
            }
            else
            {
                await moduleContext.ApiClient.UpdateFunctionSpaceAsync(moduleContext.ApiBaseUrl, unitCode, code, request);
            }

            await ReloadAsync();
            moduleContext.SetStatus(isCreation ? $"Salle {code} créée." : $"Salle {code} modifiée.");
        });
    }

    private async void ToggleSpaceButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !canWrite || SpacesDataGrid.SelectedItem is not SpaceRow row)
        {
            return;
        }

        var target = !row.IsActive;

        // Desactiver une salle ne touche PAS aux evenements deja places : le message le dit, pour
        // que personne ne croie avoir annule des ventes.
        if (!target)
        {
            var confirmed = Confirm(
                $"Désactiver la salle {row.Code} ({row.Label}) ?\n"
                + "Elle n'acceptera plus de nouvel événement. Les événements déjà placés sont conservés.",
                "Désactiver la salle");

            if (!confirmed)
            {
                return;
            }
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.SetFunctionSpaceActiveAsync(
                moduleContext.ApiBaseUrl,
                row.HotelUnitCode,
                row.Code,
                target);

            await ReloadAsync();
            moduleContext.SetStatus(target ? $"Salle {row.Code} activée." : $"Salle {row.Code} désactivée.");
        });
    }

    // ------------------------------------ Etats et outils ------------------------------------

    private string? SelectedUnitCode =>
        UnitComboBox.SelectedItem is HotelUnitResponse unit ? unit.Code : null;

    /// <summary>
    /// Source unique de l'etat des boutons d'ecriture. Symetrique par construction : chaque bouton
    /// recoit soit son info-bulle d'origine, soit le message de permission - jamais un etat mixte.
    /// </summary>
    private void UpdateActionButtons()
    {
        var hasSelection = selectedEvent is not null;
        var isCancelled = selectedEvent?.Status == nameof(EventBookingStatus.Cancelled);
        var isInvoiced = selectedEvent?.InvoiceId is not null;
        var isConfirmed = selectedEvent?.Status == nameof(EventBookingStatus.Confirmed);

        ApplyPermissionHint(CreateSpaceButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(UpdateSpaceButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(ToggleSpaceButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(CreateEventButton, canWrite, WritePermissionHint);

        ApplyPermissionHint(AddQuoteLineButton, canWrite && hasSelection && !isCancelled && !isInvoiced, WritePermissionHint);
        ApplyPermissionHint(RemoveQuoteLineButton, canWrite && hasSelection && !isCancelled && !isInvoiced, WritePermissionHint);
        ApplyPermissionHint(SaveQuoteButton, canWrite && hasSelection && !isCancelled && !isInvoiced, WritePermissionHint);

        // Le BEO reste modifiable APRES facturation : le document commercial est fige, pas
        // l'operation. Il se ferme seulement a l'annulation.
        ApplyPermissionHint(AddBeoLineButton, canWrite && hasSelection && !isCancelled, WritePermissionHint);
        ApplyPermissionHint(RemoveBeoLineButton, canWrite && hasSelection && !isCancelled, WritePermissionHint);
        ApplyPermissionHint(SaveBeoButton, canWrite && hasSelection && !isCancelled, WritePermissionHint);

        ApplyPermissionHint(ConfirmEventButton, canWrite && hasSelection && !isCancelled && !isConfirmed, WritePermissionHint);
        ApplyPermissionHint(CancelEventButton, canWrite && hasSelection && !isCancelled && !isInvoiced, WritePermissionHint);
        ApplyPermissionHint(InvoiceEventButton, canInvoice && hasSelection && isConfirmed && !isInvoiced, InvoicePermissionHint);

        UpdateAllotmentButtons();
    }

    private void ApplyPermissionHint(Button button, bool isEnabled, string hint)
    {
        if (!originalToolTips.ContainsKey(button))
        {
            originalToolTips[button] = button.ToolTip;
        }

        button.IsEnabled = isEnabled;
        button.ToolTip = isEnabled ? originalToolTips[button] : hint;
    }

    private bool Confirm(string message, string caption)
    {
        var owner = Window.GetWindow(this);

        var result = owner is null
            ? MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            : MessageBox.Show(owner, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    private static bool TryReadTime(string? value, out TimeOnly time)
    {
        return TimeOnly.TryParseExact(
            value?.Trim(),
            "HH\\:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out time);
    }

    private static bool TryReadPositiveInt(string? value, out int result)
    {
        return int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out result) && result > 0;
    }

    private static bool TryReadNonNegativeInt(string? value, out int result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = 0;
            return true;
        }

        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out result) && result >= 0;
    }

    private static bool TryReadDecimal(string? value, out decimal result)
    {
        var text = value?.Trim().Replace(',', '.');

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    private static string DescribeSetupStyle(EventSetupStyle style) => style switch
    {
        EventSetupStyle.Theatre => "Théâtre",
        EventSetupStyle.Classroom => "Classe",
        EventSetupStyle.UShape => "En U",
        EventSetupStyle.Boardroom => "Conseil",
        EventSetupStyle.Banquet => "Banquet",
        EventSetupStyle.Cocktail => "Cocktail",
        _ => "Autre"
    };

    private sealed record SetupStyleOption(EventSetupStyle Style, string Label);

    /// <summary>Ligne de la liste des salles.</summary>
    private sealed class SpaceRow(FunctionSpaceResponse space)
    {
        public string HotelUnitCode { get; } = space.HotelUnitCode;

        public string Code { get; } = space.Code;

        public string Label { get; } = space.Label;

        public int MaxAttendance { get; } = space.MaxAttendance;

        public int UpcomingEventCount { get; } = space.UpcomingEventCount;

        public bool IsActive { get; } = space.IsActive;

        public string StateLabel { get; } = space.IsActive ? "Active" : "Archivée";
    }

    /// <summary>Ligne de la liste des evenements.</summary>
    private sealed class EventRow(EventBookingResponse booking)
    {
        public EventBookingResponse Source { get; } = booking;

        public Guid Id { get; } = booking.Id;

        public string Reference { get; } = booking.Reference;

        public DateOnly EventDate { get; } = booking.EventDate;

        public string DateLabel { get; } = booking.EventDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);

        public string GuestHoursLabel { get; } =
            $"{booking.StartTime:HH\\:mm}–{booking.StartTime.AddMinutes(booking.DurationMinutes):HH\\:mm}";

        // La fenetre REELLE, montage et demontage compris : c'est elle qui cree les conflits.
        public string OccupancyLabel { get; } =
            $"{booking.OccupiedFrom:HH\\:mm}–{booking.OccupiedTo:HH\\:mm}";

        public string FunctionSpaceLabel { get; } = booking.FunctionSpaceLabel;

        public string CustomerName { get; } = booking.CustomerName;

        public int ExpectedAttendance { get; } = booking.ExpectedAttendance;

        public string Status { get; } = booking.Status;

        public string StatusLabel { get; } = booking.Status switch
        {
            nameof(EventBookingStatus.Draft) => "Devis",
            nameof(EventBookingStatus.Confirmed) => "Confirmé",
            _ => "Annulé"
        };

        public decimal TotalExclVat { get; } = booking.TotalExclVat;

        public string InvoiceLabel { get; } = booking.InvoiceId is null
            ? "—"
            : booking.InvoiceNumber ?? "brouillon";
    }

    /// <summary>Ligne editable du devis. Les montants restent en texte tant qu'ils sont saisis.</summary>
    private sealed class QuoteLineRow
    {
        public string Designation { get; set; } = string.Empty;

        public string Quantity { get; set; } = "1";

        public string UnitPrice { get; set; } = "0";

        public string VatRate { get; set; } = "19";
    }

    /// <summary>Ligne editable du deroule BEO.</summary>
    private sealed class BeoRow
    {
        public string StartTime { get; set; } = "08:00";

        public string Description { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;
    }
}
