using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Hebergement &amp; occupation : referentiel des types de chambre et des
/// chambres (consultation), reservations et cycle comptoir (check-in,
/// check-out, annulation, no-show), folio de la reservation selectionnee et
/// taux d'occupation d'une unite.
///
/// Vue de module autonome : elle ne connait ni MainWindow ni les autres vues,
/// tout passe par le <see cref="ModuleViewContext"/> recu dans Initialize().
/// </summary>
public partial class LodgingView : UserControl
{
    private const string WritePermissionHint =
        "Permission requise : lodging.write. Votre profil ne peut que consulter l'hébergement.";

    private const string CheckinPermissionHint =
        "Permission requise : lodging.checkin. Votre profil ne peut pas effectuer les opérations du comptoir.";

    private const string TariffsReadPermissionHint =
        "Permission requise : tariffs.read. Votre profil ne peut pas consulter la résolution des tarifs.";

    private const string DefaultFolioCaption =
        "Sélectionnez une réservation arrivée ou terminée pour consulter son folio.";

    // Libelles francais des natures de ligne qu'un operateur peut ajouter a la
    // main : les nuits (Night) sont generees automatiquement au check-in et ne
    // sont donc pas proposees ici.
    private static readonly ChargeKindOption[] ChargeKindOptions =
    [
        new(ChargeKind.Extra, "Extra / consommation"),
        new(ChargeKind.Settlement, "Règlement (négatif)"),
        new(ChargeKind.Adjustment, "Ajustement")
    ];

    private ModuleViewContext? context;

    // Info-bulles d'origine des boutons, capturees avant toute substitution par un
    // message de permission : l'affectation doit rester symetrique (voir
    // ApplyPermissionHint), les vues survivant a la deconnexion.
    private readonly Dictionary<Button, object?> originalToolTips = [];

    // Droits du profil connecte, releves a l'ouverture de session. Le serveur
    // reste la seule autorite : ceci n'est qu'un confort d'interface.
    private bool canWrite = true;
    private bool canCheckin = true;
    private bool canReadTariffs = true;

    // Chambres actives de l'unite retenue pour une nouvelle reservation :
    // rechargees a chaque changement d'unite dans le formulaire de creation.
    private IReadOnlyCollection<RoomResponse> newReservationRooms = Array.Empty<RoomResponse>();

    public LodgingView()
    {
        InitializeComponent();
        InitializeDefaults();
    }

    /// <summary>
    /// Memorise le contexte prete par la fenetre et releve les permissions du
    /// profil. Aucun appel reseau ici : le premier chargement est declenche par
    /// LoadAsync().
    /// </summary>
    public void Initialize(ModuleViewContext moduleViewContext)
    {
        context = moduleViewContext;
        canWrite = moduleViewContext.HasPermission(PermissionCatalog.LodgingWrite);
        canCheckin = moduleViewContext.HasPermission(PermissionCatalog.LodgingCheckin);

        // L'apercu du tarif passe par /api/v1/tariffs/resolve, qui exige
        // tariffs.read (et non lodging.read) : le bouton est grise quand le
        // profil ne detient pas ce droit-la.
        canReadTariffs = moduleViewContext.HasPermission(PermissionCatalog.TariffsRead);
        UpdateActionStates();
    }

    /// <summary>
    /// (Re)charge les unites, le referentiel chambres et les reservations de la
    /// periode filtree. Sort silencieusement tant qu'aucun contexte n'est fourni
    /// ou que l'utilisateur n'est pas connecte.
    /// </summary>
    public async Task LoadAsync()
    {
        var current = context;

        if (current is null || !current.ApiClient.IsAuthenticated)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await LoadHotelUnitsAsync(current);
            await LoadRoomsReferentialAsync(current);
            await LoadReservationsAsync(current);
        });
    }

    /// <summary>
    /// Vide grilles et formulaires : appele a la deconnexion pour ne jamais
    /// laisser les donnees d'un utilisateur a l'ecran.
    /// </summary>
    public void ResetState()
    {
        ReservationsDataGrid.ItemsSource = null;
        FolioChargesDataGrid.ItemsSource = null;
        RoomTypesDataGrid.ItemsSource = null;
        RoomsDataGrid.ItemsSource = null;
        OccupancyDataGrid.ItemsSource = null;
        FilterUnitComboBox.ItemsSource = null;
        NewReservationUnitComboBox.ItemsSource = null;
        NewReservationRoomComboBox.ItemsSource = null;
        OccupancyUnitComboBox.ItemsSource = null;
        newReservationRooms = Array.Empty<RoomResponse>();
        FilterCustomerTextBox.Text = string.Empty;
        NewReservationCustomerTextBox.Text = string.Empty;
        NewReservationGuestsTextBox.Text = "1";
        RatePreviewTextBlock.Text = string.Empty;
        CancelReasonTextBox.Text = string.Empty;
        IncludeInactiveRoomsCheckBox.IsChecked = false;
        ResetFolioPanel();
        ResetChargeForm();
        InitializeDefaults();
    }

    // Periode par defaut des reservations : le mois courant. Occupation : la
    // semaine a venir. Sejour propose : ce soir, une nuit.
    private void InitializeDefaults()
    {
        var today = DateTime.Today;

        ReservationsFromDatePicker.SelectedDate = new DateTime(today.Year, today.Month, 1);
        ReservationsToDatePicker.SelectedDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        NewReservationArrivalDatePicker.SelectedDate = today;
        NewReservationDepartureDatePicker.SelectedDate = today.AddDays(1);
        OccupancyFromDatePicker.SelectedDate = today;
        OccupancyToDatePicker.SelectedDate = today.AddDays(6);
        ChargeDatePicker.SelectedDate = today;

        StatusFilterComboBox.ItemsSource = BuildStatusFilterOptions();
        StatusFilterComboBox.SelectedIndex = 0;
        ChargeKindComboBox.ItemsSource = ChargeKindOptions;
        ChargeKindComboBox.SelectedIndex = 0;

        UpdateActionStates();
    }

    private static StatusFilterOption[] BuildStatusFilterOptions() =>
    [
        new(null, "Tous les statuts"),
        new(ReservationStatus.Booked, "Réservée"),
        new(ReservationStatus.CheckedIn, "En séjour"),
        new(ReservationStatus.CheckedOut, "Terminée"),
        new(ReservationStatus.Cancelled, "Annulée"),
        new(ReservationStatus.NoShow, "No-show")
    ];

    // ================================ Chargements ================================

    private async Task LoadHotelUnitsAsync(ModuleViewContext current)
    {
        var units = (await current.ApiClient.GetHotelUnitsAsync(current.ApiBaseUrl, includeInactive: false))
            .Where(unit => unit.IsActive)
            .OrderBy(unit => unit.DisplayOrder)
            .ThenBy(unit => unit.Name)
            .ToArray();

        // Le filtre conserve sa selection d'un rechargement a l'autre.
        var previousFilterCode = (FilterUnitComboBox.SelectedItem as UnitFilterOption)?.Code;
        var options = new List<UnitFilterOption> { new(null, "Toutes les unités") };
        options.AddRange(units.Select(unit => new UnitFilterOption(unit.Code, $"{unit.Code} — {unit.Name}")));

        FilterUnitComboBox.ItemsSource = options;
        var filterIndex = options.FindIndex(option => option.Code == previousFilterCode);
        FilterUnitComboBox.SelectedIndex = filterIndex >= 0 ? filterIndex : 0;

        RebindUnitComboBox(NewReservationUnitComboBox, units);
        RebindUnitComboBox(OccupancyUnitComboBox, units);
    }

    // Restaure la selection si possible ; a defaut, preselectionne l'unite quand
    // il n'y en a qu'une, sinon laisse le choix explicite (-1).
    private static void RebindUnitComboBox(ComboBox comboBox, HotelUnitResponse[] units)
    {
        var previousCode = (comboBox.SelectedItem as HotelUnitResponse)?.Code;

        comboBox.ItemsSource = units;

        var index = Array.FindIndex(units, unit => unit.Code == previousCode);
        comboBox.SelectedIndex = index >= 0 ? index : (units.Length == 1 ? 0 : -1);
    }

    private async Task LoadRoomsReferentialAsync(ModuleViewContext current)
    {
        var includeInactive = IncludeInactiveRoomsCheckBox.IsChecked == true;

        var roomTypes = await current.ApiClient.GetRoomTypesAsync(current.ApiBaseUrl, hotelUnitCode: null, includeInactive);
        var rooms = await current.ApiClient.GetRoomsAsync(current.ApiBaseUrl, hotelUnitCode: null, includeInactive);

        RoomTypesDataGrid.ItemsSource = roomTypes
            .OrderBy(roomType => roomType.HotelUnitCode)
            .ThenBy(roomType => roomType.Code)
            .ToArray();

        RoomsDataGrid.ItemsSource = rooms
            .OrderBy(room => room.HotelUnitCode)
            .ThenBy(room => room.Number)
            .ToArray();

        RebindNewReservationRooms(rooms);
    }

    // La liste des chambres du formulaire de creation suit l'unite selectionnee ;
    // la selection est conservee quand la chambre existe toujours.
    private void RebindNewReservationRooms(IReadOnlyCollection<RoomResponse> allRooms)
    {
        newReservationRooms = allRooms;

        var unitCode = (NewReservationUnitComboBox.SelectedItem as HotelUnitResponse)?.Code;
        var previousRoomId = (NewReservationRoomComboBox.SelectedItem as RoomResponse)?.Id;

        var rooms = allRooms
            .Where(room => room.IsActive && (unitCode is null || room.HotelUnitCode == unitCode))
            .OrderBy(room => room.Number)
            .ToArray();

        NewReservationRoomComboBox.ItemsSource = rooms;

        var index = Array.FindIndex(rooms, room => room.Id == previousRoomId);
        NewReservationRoomComboBox.SelectedIndex = index >= 0 ? index : (rooms.Length == 1 ? 0 : -1);
    }

    private void NewReservationUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RebindNewReservationRooms(newReservationRooms);
        RatePreviewTextBlock.Text = string.Empty;
    }

    private async Task LoadReservationsAsync(ModuleViewContext current)
    {
        var from = ReservationsFromDatePicker.SelectedDate.HasValue
            ? DateOnly.FromDateTime(ReservationsFromDatePicker.SelectedDate.Value)
            : (DateOnly?)null;

        var to = ReservationsToDatePicker.SelectedDate.HasValue
            ? DateOnly.FromDateTime(ReservationsToDatePicker.SelectedDate.Value)
            : (DateOnly?)null;

        if (from.HasValue && to.HasValue && from > to)
        {
            current.SetStatus("La date de début ne peut pas être postérieure à la date de fin.", isError: true);
            return;
        }

        var unitCode = (FilterUnitComboBox.SelectedItem as UnitFilterOption)?.Code;
        var status = (StatusFilterComboBox.SelectedItem as StatusFilterOption)?.Status;
        var customerCode = FilterCustomerTextBox.Text.Trim();

        var reservations = await current.ApiClient.GetReservationsAsync(
            current.ApiBaseUrl,
            from,
            to,
            unitCode,
            status,
            string.IsNullOrEmpty(customerCode) ? null : customerCode);

        ReservationsDataGrid.ItemsSource = reservations
            .OrderBy(reservation => reservation.ArrivalDate)
            .ThenBy(reservation => reservation.HotelUnitCode)
            .ToArray();

        // La selection precedente ne survit pas au rechargement : le folio ne
        // doit pas rester affiche pour une ligne qui n'existe plus a l'ecran.
        ResetFolioPanel();
        UpdateActionStates();
    }

    private async void RefreshLodgingButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || !current.ApiClient.IsAuthenticated)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await LoadHotelUnitsAsync(current);
            await LoadRoomsReferentialAsync(current);
            await LoadReservationsAsync(current);
            current.SetStatus("Hébergement actualisé.");
        });
    }

    // ============================ Nouvelle reservation ===========================

    private async void PreviewRateButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        if (NewReservationUnitComboBox.SelectedItem is not HotelUnitResponse unit ||
            NewReservationRoomComboBox.SelectedItem is not RoomResponse room)
        {
            current.SetStatus("Sélectionnez l'unité et la chambre pour estimer le tarif.", isError: true);
            return;
        }

        if (NewReservationArrivalDatePicker.SelectedDate is not DateTime arrival)
        {
            current.SetStatus("Sélectionnez la date d'arrivée.", isError: true);
            return;
        }

        var customerCode = NewReservationCustomerTextBox.Text.Trim();

        await current.RunAsync(async () =>
        {
            var resolved = await current.ApiClient.ResolveNightlyRateAsync(
                current.ApiBaseUrl,
                unit.Code,
                room.RoomTypeCode,
                DateOnly.FromDateTime(arrival),
                string.IsNullOrEmpty(customerCode) ? null : customerCode);

            var amountText = resolved.Amount.ToString("N2", CultureInfo.CurrentCulture);
            var conventionText = resolved.ConventionCustomerCode is null
                ? "sans convention"
                : $"convention du client {resolved.ConventionCustomerCode}";

            // Apercu indicatif : le montant qui fait foi reste celui fige par le
            // serveur a la creation de la reservation.
            RatePreviewTextBlock.Text =
                $"Aperçu : {amountText} la nuitée — plan {resolved.RatePlanCode} ({conventionText}). " +
                "Le tarif définitif est figé par le serveur à la création.";
            current.SetStatus("Tarif estimé.");
        });
    }

    private async void CreateReservationButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            if (NewReservationUnitComboBox.SelectedItem is not HotelUnitResponse unit)
            {
                current.SetStatus("Sélectionnez l'unité hôtelière du séjour.", isError: true);
                return;
            }

            if (NewReservationRoomComboBox.SelectedItem is not RoomResponse room)
            {
                current.SetStatus("Sélectionnez la chambre réservée.", isError: true);
                return;
            }

            var customerCode = NewReservationCustomerTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(customerCode))
            {
                current.SetStatus("Le code client est requis.", isError: true);
                return;
            }

            if (NewReservationArrivalDatePicker.SelectedDate is not DateTime arrival ||
                NewReservationDepartureDatePicker.SelectedDate is not DateTime departure)
            {
                current.SetStatus("Les dates d'arrivée et de départ sont requises.", isError: true);
                return;
            }

            if (departure <= arrival)
            {
                current.SetStatus("La date de départ doit être postérieure à la date d'arrivée.", isError: true);
                return;
            }

            if (!int.TryParse(NewReservationGuestsTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var guestCount) ||
                guestCount <= 0)
            {
                current.SetStatus("Le nombre de personnes doit être un entier strictement positif.", isError: true);
                return;
            }

            var created = await current.ApiClient.CreateReservationAsync(
                current.ApiBaseUrl,
                new CreateReservationRequest(
                    unit.Code,
                    room.Id,
                    customerCode,
                    DateOnly.FromDateTime(arrival),
                    DateOnly.FromDateTime(departure),
                    guestCount));

            NewReservationCustomerTextBox.Text = string.Empty;
            RatePreviewTextBlock.Text = string.Empty;

            await LoadReservationsAsync(current);
            current.SetStatus(
                $"Réservation créée : chambre {room.Number}, du " +
                $"{created.ArrivalDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)} au " +
                $"{created.DepartureDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)}, " +
                $"nuitée figée à {created.NightlyRateSnapshot.ToString("N2", CultureInfo.CurrentCulture)}.");
        });
    }

    // ============================== Cycle du sejour ==============================

    private async void ReservationsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateActionStates();

        if (ReservationsDataGrid.SelectedItem is not ReservationResponse selected)
        {
            ResetFolioPanel();
            return;
        }

        // Le folio n'existe qu'a partir du check-in : pour une reservation encore
        // reservee, annulee ou no-show, le panneau reste explicite plutot que de
        // provoquer un 404 previsible.
        if (selected.Status is not (ReservationStatus.CheckedIn or ReservationStatus.CheckedOut))
        {
            ResetFolioPanel();
            FolioCaptionTextBlock.Text = "Le folio s'ouvre au check-in : cette réservation n'en a pas encore.";
            return;
        }

        var current = context;

        if (current is null || !current.ApiClient.IsAuthenticated)
        {
            return;
        }

        await current.RunAsync(() => LoadFolioAsync(current, selected));
    }

    private async Task LoadFolioAsync(ModuleViewContext current, ReservationResponse reservation)
    {
        var folio = await current.ApiClient.GetReservationFolioAsync(current.ApiBaseUrl, reservation.Id);

        FolioChargesDataGrid.ItemsSource = folio.Charges
            .OrderBy(charge => charge.LineNumber)
            .Select(ToFolioChargeRow)
            .ToArray();

        FolioBalanceTextBlock.Text = folio.Balance.ToString("N2", CultureInfo.CurrentCulture);
        FolioCaptionTextBlock.Text =
            $"Folio de la chambre {reservation.RoomNumber} — client {reservation.CustomerCode} " +
            $"(séjour du {reservation.ArrivalDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)} au " +
            $"{reservation.DepartureDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)}).";
    }

    private static FolioChargeRow ToFolioChargeRow(FolioChargeResponse charge) => new(
        charge.LineNumber,
        charge.ChargeDate,
        charge.Label,
        ChargeKindLabel(charge.Kind),
        charge.Amount,
        charge.Reference);

    private static string ChargeKindLabel(ChargeKind kind) => kind switch
    {
        ChargeKind.Night => "Nuitée",
        ChargeKind.Extra => "Extra",
        ChargeKind.Settlement => "Règlement",
        _ => "Ajustement"
    };

    private void ResetFolioPanel()
    {
        FolioChargesDataGrid.ItemsSource = null;
        FolioBalanceTextBlock.Text = "—";
        FolioCaptionTextBlock.Text = DefaultFolioCaption;
    }

    private async void CheckInButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        if (ReservationsDataGrid.SelectedItem is not ReservationResponse selected ||
            selected.Status != ReservationStatus.Booked)
        {
            current.SetStatus("Sélectionnez une réservation encore réservée pour enregistrer l'arrivée.", isError: true);
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.CheckInReservationAsync(current.ApiBaseUrl, selected.Id);
            await LoadReservationsAsync(current);
            current.SetStatus($"Check-in enregistré pour la chambre {selected.RoomNumber} — le folio du séjour est ouvert.");
        });
    }

    private async void CheckOutButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        if (ReservationsDataGrid.SelectedItem is not ReservationResponse selected ||
            selected.Status != ReservationStatus.CheckedIn)
        {
            current.SetStatus("Sélectionnez une réservation en séjour pour enregistrer le départ.", isError: true);
            return;
        }

        // Acte engageant : le check-out est la transition TERMINALE du sejour (aucune
        // reouverture, folio fige) - c'est le seul acte irreversible du comptoir, il est
        // confirme comme l'annulation et le no-show.
        var confirmed = Confirm(
            $"Enregistrer le départ du client {selected.CustomerCode} " +
            $"(chambre {selected.RoomNumber}, séjour du " +
            $"{selected.ArrivalDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)} au " +
            $"{selected.DepartureDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)}) ?\n\n" +
            "Le check-out est définitif : le séjour sera clos et son folio figé, sans réouverture possible.",
            "Check-out d'une réservation");

        if (!confirmed)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.CheckOutReservationAsync(current.ApiBaseUrl, selected.Id);
            await LoadReservationsAsync(current);
            current.SetStatus($"Check-out enregistré pour la chambre {selected.RoomNumber}.");
        });
    }

    private async void NoShowButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        if (ReservationsDataGrid.SelectedItem is not ReservationResponse selected ||
            selected.Status != ReservationStatus.Booked)
        {
            current.SetStatus("Sélectionnez une réservation encore réservée pour constater un no-show.", isError: true);
            return;
        }

        // Acte engageant : le no-show fige la reservation (irreversible).
        var confirmed = Confirm(
            $"Constater le no-show de la réservation du client {selected.CustomerCode} " +
            $"(chambre {selected.RoomNumber}, arrivée prévue le " +
            $"{selected.ArrivalDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)}) ?\n\n" +
            "La réservation sera définitivement close et la chambre libérée.",
            "No-show d'une réservation");

        if (!confirmed)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.MarkReservationNoShowAsync(current.ApiBaseUrl, selected.Id);
            await LoadReservationsAsync(current);
            current.SetStatus($"No-show constaté pour le client {selected.CustomerCode}.");
        });
    }

    private async void CancelReservationButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        if (ReservationsDataGrid.SelectedItem is not ReservationResponse selected ||
            selected.Status != ReservationStatus.Booked)
        {
            current.SetStatus("Sélectionnez une réservation encore réservée pour l'annuler.", isError: true);
            return;
        }

        var reason = CancelReasonTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(reason))
        {
            current.SetStatus("Le motif d'annulation est requis.", isError: true);
            return;
        }

        // Acte engageant : l'annulation est definitive et tracee.
        var confirmed = Confirm(
            $"Annuler la réservation du client {selected.CustomerCode} " +
            $"(chambre {selected.RoomNumber}, arrivée le " +
            $"{selected.ArrivalDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)}) ?\n\n" +
            $"Motif : {reason}",
            "Annulation d'une réservation");

        if (!confirmed)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.CancelReservationAsync(
                current.ApiBaseUrl,
                selected.Id,
                new CancelReservationRequest(reason));

            CancelReasonTextBox.Text = string.Empty;

            await LoadReservationsAsync(current);
            current.SetStatus($"Réservation du client {selected.CustomerCode} annulée.");
        });
    }

    // ================================== Folio ====================================

    private async void AddChargeButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        if (ReservationsDataGrid.SelectedItem is not ReservationResponse selected ||
            selected.Status != ReservationStatus.CheckedIn)
        {
            current.SetStatus("Sélectionnez une réservation en séjour pour compléter son folio.", isError: true);
            return;
        }

        await current.RunAsync(async () =>
        {
            if (ChargeDatePicker.SelectedDate is not DateTime chargeDate)
            {
                current.SetStatus("La date de la ligne de folio est requise.", isError: true);
                return;
            }

            var label = ChargeLabelTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(label))
            {
                current.SetStatus("Le libellé de la ligne de folio est requis.", isError: true);
                return;
            }

            if (ChargeKindComboBox.SelectedItem is not ChargeKindOption kindOption)
            {
                current.SetStatus("Sélectionnez la nature de la ligne.", isError: true);
                return;
            }

            if (!TryReadAmount(ChargeAmountTextBox.Text, out var amount) || amount == 0)
            {
                current.SetStatus("Le montant doit être un montant non nul.", isError: true);
                return;
            }

            // Regle de signe du domaine (FolioCharge) : seule une ligne de
            // reglement ou d'ajustement peut etre negative.
            if (amount < 0 && kindOption.Kind is not (ChargeKind.Settlement or ChargeKind.Adjustment))
            {
                current.SetStatus("Seuls un règlement ou un ajustement peuvent porter un montant négatif.", isError: true);
                return;
            }

            var reference = ChargeReferenceTextBox.Text.Trim();

            var folio = await current.ApiClient.AddFolioChargeAsync(
                current.ApiBaseUrl,
                selected.Id,
                new AddFolioChargeRequest(
                    DateOnly.FromDateTime(chargeDate),
                    label,
                    amount,
                    kindOption.Kind,
                    string.IsNullOrEmpty(reference) ? null : reference));

            ResetChargeForm();

            FolioChargesDataGrid.ItemsSource = folio.Charges
                .OrderBy(charge => charge.LineNumber)
                .Select(ToFolioChargeRow)
                .ToArray();
            FolioBalanceTextBlock.Text = folio.Balance.ToString("N2", CultureInfo.CurrentCulture);

            current.SetStatus(
                $"Ligne ajoutée au folio — nouveau solde : {folio.Balance.ToString("N2", CultureInfo.CurrentCulture)}.");
        });
    }

    private void ResetChargeForm()
    {
        ChargeDatePicker.SelectedDate = DateTime.Today;
        ChargeLabelTextBox.Text = string.Empty;
        ChargeAmountTextBox.Text = string.Empty;
        ChargeReferenceTextBox.Text = string.Empty;

        if (ChargeKindComboBox.Items.Count > 0)
        {
            ChargeKindComboBox.SelectedIndex = 0;
        }
    }

    // ================================ Occupation =================================

    private async void LoadOccupancyButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        if (OccupancyUnitComboBox.SelectedItem is not HotelUnitResponse unit)
        {
            current.SetStatus("Sélectionnez l'unité hôtelière dont l'occupation est calculée.", isError: true);
            return;
        }

        if (OccupancyFromDatePicker.SelectedDate is not DateTime fromDate ||
            OccupancyToDatePicker.SelectedDate is not DateTime toDate)
        {
            current.SetStatus("Les dates de début et de fin de la période sont requises.", isError: true);
            return;
        }

        if (fromDate > toDate)
        {
            current.SetStatus("La date de début ne peut pas être postérieure à la date de fin.", isError: true);
            return;
        }

        await current.RunAsync(async () =>
        {
            var occupancy = await current.ApiClient.GetOccupancyAsync(
                current.ApiBaseUrl,
                unit.Code,
                DateOnly.FromDateTime(fromDate),
                DateOnly.FromDateTime(toDate));

            OccupancyDataGrid.ItemsSource = occupancy.Days
                .OrderBy(day => day.Date)
                .ToArray();

            current.SetStatus($"Occupation de l'unité {unit.Code} calculée.");
        });
    }

    // ================================== Etats ====================================

    // Une action indisponible est grisee plutot que de laisser l'utilisateur
    // declencher une erreur API previsible. L'etat metier de la reservation est
    // croise avec les droits lodging.write / lodging.checkin du profil.
    private void UpdateActionStates()
    {
        var selected = ReservationsDataGrid.SelectedItem as ReservationResponse;

        CreateReservationButton.IsEnabled = canWrite;
        CancelReservationButton.IsEnabled = canWrite && selected?.Status == ReservationStatus.Booked;
        NoShowButton.IsEnabled = canWrite && selected?.Status == ReservationStatus.Booked;
        CheckInButton.IsEnabled = canCheckin && selected?.Status == ReservationStatus.Booked;
        CheckOutButton.IsEnabled = canCheckin && selected?.Status == ReservationStatus.CheckedIn;
        AddChargeButton.IsEnabled = canCheckin && selected?.Status == ReservationStatus.CheckedIn;
        PreviewRateButton.IsEnabled = canReadTariffs;

        ApplyPermissionHint(CreateReservationButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(CancelReservationButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(NoShowButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(CheckInButton, canCheckin, CheckinPermissionHint);
        ApplyPermissionHint(CheckOutButton, canCheckin, CheckinPermissionHint);
        ApplyPermissionHint(AddChargeButton, canCheckin, CheckinPermissionHint);
        ApplyPermissionHint(PreviewRateButton, canReadTariffs, TariffsReadPermissionHint);
    }

    // Pose le message d'explication quand le droit manque, et RESTAURE l'info-bulle
    // d'origine du bouton quand il est present : l'affectation doit etre symetrique
    // (meme motif ApplyPermissionHint que ClosingView).
    private void ApplyPermissionHint(Button button, bool allowed, string hint)
    {
        if (!originalToolTips.ContainsKey(button))
        {
            originalToolTips[button] = button.ToolTip;
        }

        button.ToolTip = allowed ? originalToolTips[button] : hint;
    }

    // Montants : culture du poste d'abord, repli invariant (meme tolerance de
    // saisie que TryReadMoney dans MainWindow). AllowLeadingSign : un reglement
    // se saisit en negatif.
    private static bool TryReadAmount(string text, out decimal value)
    {
        var trimmed = text.Trim();

        return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            || decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    // Gabarit de confirmation des actes engageants : fenetre proprietaire, icone
    // d'avertissement, defaut sur Non.
    private bool Confirm(string message, string caption)
    {
        var owner = Window.GetWindow(this);

        var result = owner is null
            ? MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(owner, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    /// <summary>
    /// Entree du filtre d'unite : un code nul represente "Toutes les unités".
    /// </summary>
    private sealed record UnitFilterOption(string? Code, string Label);

    /// <summary>
    /// Entree du filtre de statut : un statut nul represente "Tous les statuts".
    /// </summary>
    private sealed record StatusFilterOption(ReservationStatus? Status, string Label);

    /// <summary>Nature de ligne de folio proposee a la saisie manuelle.</summary>
    private sealed record ChargeKindOption(ChargeKind Kind, string Label);

    /// <summary>
    /// Projection d'affichage d'une ligne de folio : la nature est traduite en
    /// francais plutot que d'exposer le nom du membre de l'enum.
    /// </summary>
    private sealed record FolioChargeRow(
        int LineNumber,
        DateOnly ChargeDate,
        string Label,
        string KindLabel,
        decimal Amount,
        string? Reference);
}
