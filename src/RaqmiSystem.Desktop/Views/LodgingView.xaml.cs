using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Hebergement &amp; occupation : le poste de travail hotelier complet, en cinq
/// sous-onglets - Reception (KPI du jour, retards, arrivees/departs avec actions
/// directes), Nouvelle reservation (flux disponibilite-d'abord), Planning (tape
/// chart 14 jours x chambres avec carte de sejour), Folio et le referentiel
/// Chambres &amp; occupation.
///
/// Vue de module autonome : elle ne connait ni MainWindow ni les autres vues,
/// tout passe par le <see cref="ModuleViewContext"/> recu dans Initialize().
/// Tous les chiffres financiers (tarifs, totaux, soldes) viennent du serveur -
/// la vue n'additionne jamais elle-meme.
/// </summary>
public partial class LodgingView : UserControl
{
    private const string WritePermissionHint =
        "Permission requise : lodging.write. Votre profil ne peut que consulter l'hébergement.";

    private const string CheckinPermissionHint =
        "Permission requise : lodging.checkin. Votre profil ne peut pas effectuer les opérations du comptoir.";

    private const string CustomersReadPermissionHint =
        "Permission requise : customers.read. Votre profil ne peut pas consulter le fichier clients.";

    private const string DefaultFolioCaption =
        "Sélectionnez un séjour depuis la Réception ou le Planning pour consulter son folio.";

    private const string BalanceDuePathMessage =
        "Le folio n'est pas soldé : encaissez d'abord le règlement en trésorerie, puis ajoutez la ligne de règlement correspondante au folio (onglet Folio), avant le check-out.";

    // Index des sous-onglets du module (ordre du XAML).
    private const int FrontDeskTabIndex = 0;
    private const int PlanningTabIndex = 2;
    private const int FolioTabIndex = 3;

    // Geometrie du tape chart : 14 jours glissants, colonnes de largeur fixe
    // pour que les barres restent lisibles quel que soit l'ecran.
    private const int PlanningDays = 14;
    private const double PlanningDayWidth = 86;
    private const double PlanningRoomColumnWidth = 150;
    private const double PlanningHeaderHeight = 48;
    private const double PlanningRowHeight = 42;

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
    private bool canReadCustomers = true;

    private IReadOnlyList<HotelUnitResponse> hotelUnits = Array.Empty<HotelUnitResponse>();

    // Evite que la re-liaison des combos d'unite (rechargement, ResetState) ne
    // declenche les chargements attaches a SelectionChanged.
    private bool suppressUnitSelectionEvents;

    // Derniere recherche de disponibilites : parametres retenus pour la creation
    // (les tarifs affiches ont ete resolus avec CE client-la).
    private AvailabilityResponse? currentAvailability;
    private string? availabilitySearchCustomerCode;

    // Fenetre du tape chart et donnees affichees.
    private DateOnly planningStart = DateOnly.FromDateTime(DateTime.Today);
    private IReadOnlyList<RoomResponse> planningRooms = Array.Empty<RoomResponse>();
    private IReadOnlyList<ReservationResponse> planningReservations = Array.Empty<ReservationResponse>();
    private bool planningLoaded;
    private Border? selectedPlanningBar;

    // Sejour selectionne (depuis la Reception ou le Planning) : la carte de
    // sejour et l'onglet Folio s'y rattachent. Le solde vient du serveur au
    // chargement du folio - jamais recalcule ici.
    private StaySelection? selectedStay;
    private decimal? selectedStayBalance;

    public LodgingView()
    {
        InitializeComponent();

        // Les StringFormat du XAML (dates, N2) doivent suivre la culture du
        // poste, comme les chaines formatees dans le code.
        var languageTag = CultureInfo.CurrentCulture.IetfLanguageTag;

        if (!string.IsNullOrEmpty(languageTag))
        {
            Language = XmlLanguage.GetLanguage(languageTag);
        }

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

        // La recherche de client interroge /api/v1/customers, qui exige
        // customers.read (et non lodging.read).
        canReadCustomers = moduleViewContext.HasPermission(PermissionCatalog.CustomersRead);
        UpdateActionStates();
    }

    /// <summary>
    /// Charge les unites, le referentiel chambres et l'instantane de reception du
    /// jour. Sort silencieusement tant qu'aucun contexte n'est fourni ou que
    /// l'utilisateur n'est pas connecte.
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
            await LoadFrontDeskAsync(current);
        });
    }

    /// <summary>
    /// Vide tout : KPI, listes de reception, recherche de disponibilites, tape
    /// chart, carte de sejour, folio et referentiel. Appele a la deconnexion pour
    /// ne jamais laisser les donnees d'un utilisateur a l'ecran.
    /// </summary>
    public void ResetState()
    {
        suppressUnitSelectionEvents = true;

        try
        {
            // Reception.
            ArrivalsItemsControl.ItemsSource = null;
            DeparturesItemsControl.ItemsSource = null;
            OverdueArrivalsItemsControl.ItemsSource = null;
            OverdueDeparturesItemsControl.ItemsSource = null;
            OverdueSectionBorder.Visibility = Visibility.Collapsed;
            OverdueCancelReasonTextBox.Text = string.Empty;
            KpiArrivalsTextBlock.Text = "—";
            KpiDeparturesTextBlock.Text = "—";
            KpiInHouseTextBlock.Text = "—";
            KpiOccupancyTextBlock.Text = "—";

            // Nouvelle reservation.
            AvailabilityRoomsItemsControl.ItemsSource = null;
            AvailabilitySummaryTextBlock.Text = string.Empty;
            AvailabilityEmptyTitleTextBlock.Text = "Aucune recherche lancée";
            AvailabilityEmptyHintTextBlock.Text =
                "Renseignez le séjour recherché ci-dessus puis cliquez sur « Rechercher les disponibilités ».";
            CustomerSearchTextBox.Text = string.Empty;
            CustomerResultsComboBox.ItemsSource = null;
            AvailabilityGuestsTextBox.Text = "1";
            BookingSuccessBorder.Visibility = Visibility.Collapsed;
            BookingSuccessTextBlock.Text = string.Empty;
            currentAvailability = null;
            availabilitySearchCustomerCode = null;

            // Planning.
            planningRooms = Array.Empty<RoomResponse>();
            planningReservations = Array.Empty<ReservationResponse>();
            planningLoaded = false;
            selectedPlanningBar = null;
            planningStart = DateOnly.FromDateTime(DateTime.Today);
            PlanningGrid.Children.Clear();
            PlanningGrid.RowDefinitions.Clear();
            PlanningGrid.ColumnDefinitions.Clear();
            PlanningRangeTextBlock.Text = "—";
            PlanningEmptyStatePanel.Visibility = Visibility.Visible;
            PlanningEmptyTitleTextBlock.Text = "Aucun planning affiché";
            PlanningEmptyHintTextBlock.Text =
                "Choisissez une unité hôtelière pour afficher ses chambres sur 14 jours.";
            StayCancelReasonTextBox.Text = string.Empty;
            SetSelectedStay(null);

            // Folio.
            ResetFolioPanel();
            ResetChargeForm();

            // Referentiel et occupation.
            RoomTypesDataGrid.ItemsSource = null;
            RoomsDataGrid.ItemsSource = null;
            OccupancyDataGrid.ItemsSource = null;
            IncludeInactiveRoomsCheckBox.IsChecked = false;

            // Unites.
            hotelUnits = Array.Empty<HotelUnitResponse>();
            FrontDeskUnitComboBox.ItemsSource = null;
            AvailabilityUnitComboBox.ItemsSource = null;
            PlanningUnitComboBox.ItemsSource = null;
            OccupancyUnitComboBox.ItemsSource = null;

            LodgingTabs.SelectedIndex = FrontDeskTabIndex;
            InitializeDefaults();
        }
        finally
        {
            suppressUnitSelectionEvents = false;
        }
    }

    // Valeurs par defaut du poste : la journee du jour a la reception, un sejour
    // d'une nuit ce soir pour la recherche, la semaine a venir pour l'occupation.
    private void InitializeDefaults()
    {
        var today = DateTime.Today;

        FrontDeskDatePicker.SelectedDate = today;
        AvailabilityFromDatePicker.SelectedDate = today;
        AvailabilityToDatePicker.SelectedDate = today.AddDays(1);
        OccupancyFromDatePicker.SelectedDate = today;
        OccupancyToDatePicker.SelectedDate = today.AddDays(6);
        ChargeDatePicker.SelectedDate = today;

        ChargeKindComboBox.ItemsSource = ChargeKindOptions;
        ChargeKindComboBox.SelectedIndex = 0;

        UpdateActionStates();
    }

    // ================================ Chargements ================================

    private async Task LoadHotelUnitsAsync(ModuleViewContext current)
    {
        var units = (await current.ApiClient.GetHotelUnitsAsync(current.ApiBaseUrl, includeInactive: false))
            .Where(unit => unit.IsActive)
            .OrderBy(unit => unit.DisplayOrder)
            .ThenBy(unit => unit.Name)
            .ToArray();

        hotelUnits = units;

        suppressUnitSelectionEvents = true;

        try
        {
            RebindUnitComboBox(FrontDeskUnitComboBox, units);
            RebindUnitComboBox(AvailabilityUnitComboBox, units);
            RebindUnitComboBox(PlanningUnitComboBox, units);
            RebindUnitComboBox(OccupancyUnitComboBox, units);
        }
        finally
        {
            suppressUnitSelectionEvents = false;
        }
    }

    // Restaure la selection quand l'unite existe toujours ; a defaut retient la
    // premiere : le comptoir doit s'ouvrir pret a travailler, pas sur un choix vide.
    private static void RebindUnitComboBox(ComboBox comboBox, HotelUnitResponse[] units)
    {
        var previousCode = (comboBox.SelectedItem as HotelUnitResponse)?.Code;

        comboBox.ItemsSource = units;

        var index = Array.FindIndex(units, unit => unit.Code == previousCode);
        comboBox.SelectedIndex = index >= 0 ? index : (units.Length > 0 ? 0 : -1);
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

        // Le volet de parametrage vit dans LodgingView.RoomSetup.cs : il se recale sur le
        // referentiel qui vient d'etre charge.
        OnRoomsReferentialLoaded(roomTypes);
    }

    // =============================== 1. Reception ================================

    private async void RefreshFrontDeskButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || !current.ApiClient.IsAuthenticated)
        {
            return;
        }

        if (FrontDeskUnitComboBox.SelectedItem is not HotelUnitResponse)
        {
            current.SetStatus("Sélectionnez l'unité hôtelière de la réception.", isError: true);
            return;
        }

        await current.RunAsync(async () =>
        {
            await LoadFrontDeskAsync(current);
            current.SetStatus("Réception actualisée.");
        });
    }

    private async void FrontDeskUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressUnitSelectionEvents)
        {
            return;
        }

        var current = context;

        if (current is null || !current.ApiClient.IsAuthenticated)
        {
            return;
        }

        await current.RunAsync(() => LoadFrontDeskAsync(current));
    }

    // Charge l'instantane du comptoir (arrivees, departs, retards, presents,
    // occupation) en un appel serveur, et le projette en lignes actionnables.
    private async Task LoadFrontDeskAsync(ModuleViewContext current)
    {
        if (FrontDeskUnitComboBox.SelectedItem is not HotelUnitResponse unit)
        {
            return;
        }

        var date = FrontDeskDatePicker.SelectedDate.HasValue
            ? DateOnly.FromDateTime(FrontDeskDatePicker.SelectedDate.Value)
            : DateOnly.FromDateTime(DateTime.Today);

        var frontDesk = await current.ApiClient.GetFrontDeskAsync(current.ApiBaseUrl, unit.Code, date);

        KpiArrivalsTextBlock.Text = frontDesk.Arrivals.Count.ToString(CultureInfo.CurrentCulture);
        KpiDeparturesTextBlock.Text = frontDesk.Departures.Count.ToString(CultureInfo.CurrentCulture);
        KpiInHouseTextBlock.Text = frontDesk.InHouseCount.ToString(CultureInfo.CurrentCulture);
        KpiOccupancyTextBlock.Text =
            frontDesk.Occupancy.OccupancyRatePercent.ToString("N2", CultureInfo.CurrentCulture) + " %";

        ArrivalsItemsControl.ItemsSource = frontDesk.Arrivals
            .OrderBy(arrival => arrival.RoomNumber)
            .Select(arrival => ToArrivalRow(arrival, date))
            .ToArray();

        DeparturesItemsControl.ItemsSource = frontDesk.Departures
            .OrderBy(departure => departure.RoomNumber)
            .Select(ToDepartureRow)
            .ToArray();

        OverdueArrivalsItemsControl.ItemsSource = frontDesk.OverdueArrivals
            .OrderBy(arrival => arrival.ArrivalDate)
            .Select(arrival => ToArrivalRow(arrival, date))
            .ToArray();

        OverdueDeparturesItemsControl.ItemsSource = frontDesk.OverdueDepartures
            .OrderBy(departure => departure.DepartureDate)
            .Select(ToDepartureRow)
            .ToArray();

        // La section des retards n'existe a l'ecran que quand il y en a : c'est
        // la liste a traiter en premier, pas un bandeau permanent.
        var overdueCount = frontDesk.OverdueArrivals.Count + frontDesk.OverdueDepartures.Count;
        OverdueSectionBorder.Visibility = overdueCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        OverdueTitleTextBlock.Text = overdueCount == 1
            ? "1 retard à traiter"
            : $"{overdueCount} retards à traiter";
    }

    private ArrivalRow ToArrivalRow(FrontDeskArrivalResponse arrival, DateOnly frontDeskDate)
    {
        var daysLate = frontDeskDate.DayNumber - arrival.ArrivalDate.DayNumber;

        return new ArrivalRow(
            arrival.ReservationId,
            arrival.RoomNumber ?? "?",
            FormatCustomer(arrival.CustomerCode, arrival.CustomerName),
            arrival.CustomerCode,
            $"Séjour du {FormatDate(arrival.ArrivalDate)} au {FormatDate(arrival.DepartureDate)}",
            $"{FormatNights(arrival.Nights)} · {FormatGuests(arrival.GuestCount)}",
            $"{FormatAmount(arrival.NightlyRateSnapshot)} / nuit — plan {arrival.RatePlanCodeSnapshot}",
            FormatAmount(arrival.TotalStayAmount),
            daysLate <= 0 ? string.Empty : (daysLate == 1 ? "En retard de 1 jour" : $"En retard de {daysLate} jours"),
            arrival.ArrivalDate,
            arrival.DepartureDate,
            CanCheckIn: canCheckin,
            CheckInToolTip: canCheckin
                ? "Enregistrer l'arrivée : ouvre le folio avec une ligne par nuit au tarif figé"
                : CheckinPermissionHint,
            CanDecide: canWrite,
            DecideToolTip: canWrite
                ? "Constater que le client n'est jamais arrivé (la réservation sera close)"
                : WritePermissionHint,
            CancelToolTip: canWrite
                ? "Annuler la réservation (motif requis dans le champ ci-dessus)"
                : WritePermissionHint);
    }

    private DepartureRow ToDepartureRow(FrontDeskDepartureResponse departure)
    {
        var balanceDue = departure.FolioBalance is not 0m;

        return new DepartureRow(
            departure.ReservationId,
            departure.RoomNumber ?? "?",
            FormatCustomer(departure.CustomerCode, departure.CustomerName),
            departure.CustomerCode,
            $"Séjour du {FormatDate(departure.ArrivalDate)} au {FormatDate(departure.DepartureDate)} · {FormatNights(departure.Nights)}",
            departure.ArrivalDate,
            departure.DepartureDate,
            departure.FolioBalance,
            departure.FolioBalance is null ? "Folio introuvable" : FormatAmount(departure.FolioBalance.Value),
            IsBalanceDue: balanceDue,
            CanCheckOut: canCheckin,
            CheckOutToolTip: !canCheckin
                ? CheckinPermissionHint
                : balanceDue
                    ? BalanceDuePathMessage
                    : "Enregistrer le départ : le séjour sera clos et son folio figé");
    }

    private async void ArrivalCheckInButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || (sender as Button)?.DataContext is not ArrivalRow row)
        {
            return;
        }

        // Confirmation legere : le check-in ouvre le folio, il engage la
        // facturation du sejour.
        var confirmed = Confirm(
            $"Enregistrer l'arrivée du client {row.CustomerDisplay} " +
            $"(chambre {row.RoomNumber}, du {FormatDate(row.ArrivalDate)} au {FormatDate(row.DepartureDate)}) ?\n\n" +
            "Le folio du séjour sera ouvert avec une ligne par nuit au tarif figé à la réservation.",
            "Check-in d'une arrivée");

        if (!confirmed)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.CheckInReservationAsync(current.ApiBaseUrl, row.ReservationId);
            await LoadFrontDeskAsync(current);
            current.SetStatus($"Check-in enregistré pour la chambre {row.RoomNumber} — le folio du séjour est ouvert.");
        });
    }

    private async void DepartureCheckOutButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || (sender as Button)?.DataContext is not DepartureRow row)
        {
            return;
        }

        if (row.Balance is null)
        {
            current.SetStatus(
                "Le folio de ce séjour est introuvable — situation anormale, contactez l'administrateur.",
                isError: true);
            return;
        }

        // Refus local a solde non nul, avec le chemin a suivre : le serveur
        // applique la meme regle, l'ecran ne fait que l'anticiper.
        if (row.Balance.Value != 0m)
        {
            current.SetStatus(
                $"Check-out refusé pour la chambre {row.RoomNumber} : solde de {FormatAmount(row.Balance.Value)}. " +
                BalanceDuePathMessage,
                isError: true);
            return;
        }

        // Acte engageant : le check-out est la transition TERMINALE du sejour
        // (aucune reouverture, folio fige).
        var confirmed = Confirm(
            $"Enregistrer le départ du client {row.CustomerDisplay} " +
            $"(chambre {row.RoomNumber}, {row.StayText.ToLower(CultureInfo.CurrentCulture)}) ?\n\n" +
            "Le check-out est définitif : le séjour sera clos et son folio figé, sans réouverture possible.",
            "Check-out d'un départ");

        if (!confirmed)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.CheckOutReservationAsync(current.ApiBaseUrl, row.ReservationId);
            await LoadFrontDeskAsync(current);
            current.SetStatus($"Check-out enregistré pour la chambre {row.RoomNumber}.");
        });
    }

    private async void OverdueNoShowButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || (sender as Button)?.DataContext is not ArrivalRow row)
        {
            return;
        }

        // Acte engageant : le no-show fige la reservation (irreversible).
        var confirmed = Confirm(
            $"Constater le no-show du client {row.CustomerDisplay} " +
            $"(chambre {row.RoomNumber}, arrivée prévue le {FormatDate(row.ArrivalDate)}) ?\n\n" +
            "La réservation sera définitivement close et la chambre libérée.",
            "No-show d'une arrivée non honorée");

        if (!confirmed)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.MarkReservationNoShowAsync(current.ApiBaseUrl, row.ReservationId);
            await LoadFrontDeskAsync(current);
            current.SetStatus($"No-show constaté pour le client {row.CustomerDisplay}.");
        });
    }

    private async void OverdueCancelButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || (sender as Button)?.DataContext is not ArrivalRow row)
        {
            return;
        }

        var reason = OverdueCancelReasonTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(reason))
        {
            current.SetStatus("Le motif d'annulation est requis (champ en tête de la section Retards).", isError: true);
            return;
        }

        // Acte engageant : l'annulation est definitive et tracee avec son motif.
        var confirmed = Confirm(
            $"Annuler la réservation du client {row.CustomerDisplay} " +
            $"(chambre {row.RoomNumber}, arrivée prévue le {FormatDate(row.ArrivalDate)}) ?\n\n" +
            $"Motif : {reason}\n\nL'annulation est définitive.",
            "Annulation d'une arrivée non honorée");

        if (!confirmed)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.CancelReservationAsync(
                current.ApiBaseUrl,
                row.ReservationId,
                new CancelReservationRequest(reason));

            OverdueCancelReasonTextBox.Text = string.Empty;

            await LoadFrontDeskAsync(current);
            current.SetStatus($"Réservation du client {row.CustomerDisplay} annulée.");
        });
    }

    private async void DepartureFolioButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || (sender as Button)?.DataContext is not DepartureRow row)
        {
            return;
        }

        // Un depart du jour est par construction un sejour en cours (CheckedIn).
        SetSelectedStay(new StaySelection(
            row.ReservationId,
            row.RoomNumber,
            row.CustomerDisplay,
            row.CustomerCode,
            row.ArrivalDate,
            row.DepartureDate,
            Nights: Math.Max(1, row.DepartureDate.DayNumber - row.ArrivalDate.DayNumber),
            GuestCount: null,
            ReservationStatus.CheckedIn,
            NightlyRate: null,
            RatePlanCode: null));

        LodgingTabs.SelectedIndex = FolioTabIndex;

        await current.RunAsync(() => LoadFolioForStayAsync(current));
    }

    // =========================== 2. Nouvelle reservation ==========================

    private async void SearchCustomerButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        var search = CustomerSearchTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(search))
        {
            current.SetStatus("Saisissez un code ou un nom de client à rechercher.", isError: true);
            return;
        }

        await current.RunAsync(async () =>
        {
            var customers = await current.ApiClient.GetCustomersAsync(current.ApiBaseUrl, search, includeInactive: false);

            var options = customers
                .OrderBy(customer => customer.Code)
                .Select(customer => new CustomerOption(customer.Code, $"{customer.Code} — {customer.Name}"))
                .ToArray();

            CustomerResultsComboBox.ItemsSource = options;
            CustomerResultsComboBox.SelectedIndex = options.Length == 1 ? 0 : -1;

            current.SetStatus(options.Length == 0
                ? "Aucun client ne correspond à cette recherche."
                : options.Length == 1
                    ? $"Client retenu : {options[0].Label}."
                    : $"{options.Length} clients trouvés — choisissez le client retenu.");
        });
    }

    private void CustomerResultsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Les tarifs affiches ont ete resolus avec le client de la RECHERCHE :
        // changer le client retenu apres coup rendrait le total annonce mensonger
        // (convention differente). On previent plutot que de laisser reserver.
        var selectedCode = (CustomerResultsComboBox.SelectedItem as CustomerOption)?.Code;

        if (currentAvailability is not null && !string.Equals(selectedCode, availabilitySearchCustomerCode, StringComparison.OrdinalIgnoreCase))
        {
            AvailabilitySummaryTextBlock.Text =
                "Client modifié après la recherche : relancez « Rechercher les disponibilités » pour appliquer sa convention aux tarifs.";
        }
    }

    private async void SearchAvailabilityButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        if (AvailabilityUnitComboBox.SelectedItem is not HotelUnitResponse unit)
        {
            current.SetStatus("Sélectionnez l'unité hôtelière du séjour.", isError: true);
            return;
        }

        if (AvailabilityFromDatePicker.SelectedDate is not DateTime fromDate ||
            AvailabilityToDatePicker.SelectedDate is not DateTime toDate)
        {
            current.SetStatus("Les dates d'arrivée et de départ sont requises.", isError: true);
            return;
        }

        if (toDate <= fromDate)
        {
            current.SetStatus("La date de départ doit être postérieure à la date d'arrivée.", isError: true);
            return;
        }

        if (!int.TryParse(AvailabilityGuestsTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var guests) ||
            guests <= 0)
        {
            current.SetStatus("Le nombre de personnes doit être un entier strictement positif.", isError: true);
            return;
        }

        var customerCode = (CustomerResultsComboBox.SelectedItem as CustomerOption)?.Code;

        await current.RunAsync(async () =>
        {
            var availability = await current.ApiClient.GetAvailabilityAsync(
                current.ApiBaseUrl,
                unit.Code,
                DateOnly.FromDateTime(fromDate),
                DateOnly.FromDateTime(toDate),
                guests,
                customerCode);

            currentAvailability = availability;
            availabilitySearchCustomerCode = customerCode;
            BookingSuccessBorder.Visibility = Visibility.Collapsed;

            AvailabilityRoomsItemsControl.ItemsSource = availability.Rooms
                .OrderBy(room => !room.HasRate)
                .ThenBy(room => room.RoomNumber)
                .Select(ToAvailableRoomRow)
                .ToArray();

            AvailabilityEmptyTitleTextBlock.Text = "Aucune chambre disponible";
            AvailabilityEmptyHintTextBlock.Text =
                "Aucune chambre libre ne peut accueillir ce groupe sur ces dates — élargissez la période ou changez d'unité.";

            var customerPart = customerCode is null
                ? "sans client retenu (tarifs sans convention)"
                : $"convention du client {customerCode} appliquée le cas échéant";

            AvailabilitySummaryTextBlock.Text =
                $"{availability.Rooms.Count} chambre(s) libre(s) du {FormatDate(availability.From)} au {FormatDate(availability.To)} " +
                $"({FormatNights(availability.Nights)}, {FormatGuests(availability.Guests)}) — {customerPart}.";

            current.SetStatus("Disponibilités chargées : les tarifs affichés sont ceux que la réservation figera.");
        });
    }

    private AvailableRoomRow ToAvailableRoomRow(AvailableRoomResponse room)
    {
        if (!room.HasRate)
        {
            // Chambre libre mais sans tarif couvrant les dates : visible mais
            // grisee - l'exploitant doit voir son trou de tarification.
            return new AvailableRoomRow(
                room.RoomId,
                room.RoomNumber,
                $"{room.RoomTypeLabel} ({room.RoomTypeCode}) — capacité {FormatGuests(room.Capacity)}",
                room.RateIssue ?? "Le module tarifaire ne sait pas tarifer ces dates.",
                "Aucun tarif couvrant ces dates",
                "—",
                NightDetailToolTip: room.RateIssue,
                TotalText: "—",
                IsRateMissing: true,
                CanBook: false,
                BookToolTip: "Aucun tarif ne couvre ces dates : complétez la grille tarifaire avant de réserver.",
                Source: room);
        }

        var amounts = room.NightlyRates.Select(night => night.Amount).Distinct().ToArray();
        var isVariable = amounts.Length > 1;

        var rateText = isVariable
            ? $"{FormatAmount(amounts.Min())} à {FormatAmount(amounts.Max())} / nuit"
            : $"{FormatAmount(amounts[0])} / nuit";

        // Detail nuit par nuit en infobulle quand le tarif varie sur le sejour.
        string? nightDetail = null;

        if (isVariable)
        {
            var builder = new StringBuilder("Détail des nuitées :");

            foreach (var night in room.NightlyRates.OrderBy(night => night.Night))
            {
                builder.Append('\n')
                    .Append(FormatDate(night.Night))
                    .Append(" : ")
                    .Append(FormatAmount(night.Amount))
                    .Append(" (plan ")
                    .Append(night.RatePlanCode)
                    .Append(')');
            }

            nightDetail = builder.ToString();
        }

        var planText = $"Plan {room.RatePlanCode}";

        if (room.ConventionCustomerCode is not null)
        {
            planText += room.DiscountPercent is decimal discount
                ? $" · convention {room.ConventionCustomerCode} (remise {discount.ToString("N2", CultureInfo.CurrentCulture)} %)"
                : $" · convention {room.ConventionCustomerCode}";
        }

        return new AvailableRoomRow(
            room.RoomId,
            room.RoomNumber,
            $"{room.RoomTypeLabel} ({room.RoomTypeCode}) — capacité {FormatGuests(room.Capacity)}",
            planText,
            isVariable ? "Tarif variable (détail en infobulle)" : "Tarif par nuit",
            rateText,
            nightDetail,
            room.TotalStayAmount is decimal total ? FormatAmount(total) : "—",
            IsRateMissing: false,
            CanBook: canWrite,
            BookToolTip: canWrite
                ? "Réserver cette chambre au total annoncé (détail des nuitées figé par le serveur)"
                : WritePermissionHint,
            Source: room);
    }

    private async void BookRoomButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || (sender as Button)?.DataContext is not AvailableRoomRow row)
        {
            return;
        }

        var availability = currentAvailability;

        if (availability is null)
        {
            current.SetStatus("Relancez la recherche de disponibilités avant de réserver.", isError: true);
            return;
        }

        if (CustomerResultsComboBox.SelectedItem is not CustomerOption customer)
        {
            current.SetStatus(
                "Retenez d'abord un client (recherche ci-dessus) : la réservation est nominative.",
                isError: true);
            return;
        }

        // Les tarifs affiches ont ete resolus avec le client de la recherche : si
        // le client retenu a change depuis, le total annonce peut etre faux
        // (convention differente) - on exige une nouvelle recherche.
        if (!string.Equals(customer.Code, availabilitySearchCustomerCode, StringComparison.OrdinalIgnoreCase))
        {
            current.SetStatus(
                "Le client retenu a changé depuis la recherche : relancez « Rechercher les disponibilités » pour afficher ses tarifs.",
                isError: true);
            return;
        }

        var totalPart = row.Source.TotalStayAmount is decimal total
            ? $"Total annoncé : {FormatAmount(total)} — le serveur fige ce détail nuit par nuit, le folio facturera exactement ce total."
            : "Total non tarifé.";

        var confirmed = Confirm(
            $"Réserver la chambre {row.RoomNumber} ({row.Source.RoomTypeLabel}) pour le client {customer.Label} ?\n\n" +
            $"Séjour du {FormatDate(availability.From)} au {FormatDate(availability.To)} " +
            $"({FormatNights(availability.Nights)}, {FormatGuests(availability.Guests)}).\n\n" +
            totalPart,
            "Création d'une réservation");

        if (!confirmed)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            var created = await current.ApiClient.CreateReservationAsync(
                current.ApiBaseUrl,
                new CreateReservationRequest(
                    availability.HotelUnitCode,
                    row.RoomId,
                    customer.Code,
                    availability.From,
                    availability.To,
                    availability.Guests));

            // La chambre vient d'etre prise : la liste est rechargee pour rester
            // honnete (les disponibilites affichees sont toujours les vraies).
            var refreshed = await current.ApiClient.GetAvailabilityAsync(
                current.ApiBaseUrl,
                availability.HotelUnitCode,
                availability.From,
                availability.To,
                availability.Guests,
                availabilitySearchCustomerCode);

            currentAvailability = refreshed;
            AvailabilityRoomsItemsControl.ItemsSource = refreshed.Rooms
                .OrderBy(freeRoom => !freeRoom.HasRate)
                .ThenBy(freeRoom => freeRoom.RoomNumber)
                .Select(ToAvailableRoomRow)
                .ToArray();

            BookingSuccessTextBlock.Text =
                $"Chambre {row.RoomNumber} réservée pour le client {customer.Label}, du " +
                $"{FormatDate(created.ArrivalDate)} au {FormatDate(created.DepartureDate)} " +
                $"({FormatNights(created.Nights)}, nuitée d'arrivée figée à {FormatAmount(created.NightlyRateSnapshot)}).";
            BookingSuccessBorder.Visibility = Visibility.Visible;

            // Le planning affiche des donnees perimees s'il avait deja ete charge.
            planningLoaded = false;

            current.SetStatus($"Réservation créée : chambre {row.RoomNumber} pour le client {customer.Code}.");
        });
    }

    private void GoToFrontDeskButton_Click(object sender, RoutedEventArgs e)
    {
        LodgingTabs.SelectedIndex = FrontDeskTabIndex;
        _ = RefreshFrontDeskAfterNavigationAsync();
    }

    private void GoToPlanningButton_Click(object sender, RoutedEventArgs e)
    {
        // Le chargement paresseux de LodgingTabs_SelectionChanged prend le relais.
        LodgingTabs.SelectedIndex = PlanningTabIndex;
    }

    private async Task RefreshFrontDeskAfterNavigationAsync()
    {
        var current = context;

        if (current is null || !current.ApiClient.IsAuthenticated)
        {
            return;
        }

        await current.RunAsync(() => LoadFrontDeskAsync(current));
    }

    // ================================ 3. Planning ================================

    // Chargement paresseux du tape chart : la premiere ouverture de l'onglet
    // Planning declenche son chargement, les suivantes non (sauf invalidation
    // apres creation de reservation).
    private async void LodgingTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged est un evenement routeur qui bulle : ignorer ceux des
        // ComboBox et DataGrid internes.
        if (!ReferenceEquals(e.OriginalSource, LodgingTabs))
        {
            return;
        }

        if (LodgingTabs.SelectedIndex != PlanningTabIndex || planningLoaded)
        {
            return;
        }

        var current = context;

        if (current is null || !current.ApiClient.IsAuthenticated)
        {
            return;
        }

        planningLoaded = true;
        await current.RunAsync(() => LoadPlanningAsync(current));
    }

    private async void PlanningUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressUnitSelectionEvents)
        {
            return;
        }

        var current = context;

        if (current is null || !current.ApiClient.IsAuthenticated || LodgingTabs.SelectedIndex != PlanningTabIndex)
        {
            return;
        }

        await current.RunAsync(() => LoadPlanningAsync(current));
    }

    private async void PlanningPrevButton_Click(object sender, RoutedEventArgs e)
    {
        planningStart = planningStart.AddDays(-7);
        await ReloadPlanningAsync();
    }

    private async void PlanningNextButton_Click(object sender, RoutedEventArgs e)
    {
        planningStart = planningStart.AddDays(7);
        await ReloadPlanningAsync();
    }

    private async void PlanningTodayButton_Click(object sender, RoutedEventArgs e)
    {
        planningStart = DateOnly.FromDateTime(DateTime.Today);
        await ReloadPlanningAsync();
    }

    private async void RefreshPlanningButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || !current.ApiClient.IsAuthenticated)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await LoadPlanningAsync(current);
            current.SetStatus("Planning actualisé.");
        });
    }

    private async Task ReloadPlanningAsync()
    {
        var current = context;

        if (current is null || !current.ApiClient.IsAuthenticated)
        {
            return;
        }

        planningLoaded = true;
        await current.RunAsync(() => LoadPlanningAsync(current));
    }

    // Charge les chambres actives de l'unite et les reservations dont le sejour
    // touche la fenetre de 14 jours, puis construit le tape chart.
    private async Task LoadPlanningAsync(ModuleViewContext current)
    {
        var windowEnd = planningStart.AddDays(PlanningDays - 1);
        PlanningRangeTextBlock.Text = $"Du {FormatDate(planningStart)} au {FormatDate(windowEnd)}";

        if (PlanningUnitComboBox.SelectedItem is not HotelUnitResponse unit)
        {
            planningRooms = Array.Empty<RoomResponse>();
            planningReservations = Array.Empty<ReservationResponse>();
            BuildPlanningGrid();
            return;
        }

        var rooms = await current.ApiClient.GetRoomsAsync(current.ApiBaseUrl, unit.Code, includeInactive: false);

        planningRooms = rooms
            .Where(room => room.IsActive)
            .OrderBy(room => room.Number)
            .ToArray();

        planningReservations = (await current.ApiClient.GetReservationsAsync(
                current.ApiBaseUrl,
                planningStart,
                windowEnd,
                unit.Code,
                status: null,
                customerCode: null))
            .ToArray();

        // Les barres sont reconstruites : une selection qui pointait une barre
        // detruite ne doit pas survivre.
        selectedPlanningBar = null;
        SetSelectedStay(null);
        BuildPlanningGrid();
    }

    // ------------------------- Construction du tape chart -------------------------

    // Grille chambres x jours entierement reconstruite a chaque chargement :
    // colonne 0 = etiquette de chambre, colonnes 1..14 = les jours de la fenetre.
    private void BuildPlanningGrid()
    {
        PlanningGrid.Children.Clear();
        PlanningGrid.RowDefinitions.Clear();
        PlanningGrid.ColumnDefinitions.Clear();

        if (planningRooms.Count == 0)
        {
            PlanningEmptyStatePanel.Visibility = Visibility.Visible;
            PlanningEmptyTitleTextBlock.Text = PlanningUnitComboBox.SelectedItem is null
                ? "Aucun planning affiché"
                : "Aucune chambre active";
            PlanningEmptyHintTextBlock.Text = PlanningUnitComboBox.SelectedItem is null
                ? "Choisissez une unité hôtelière pour afficher ses chambres sur 14 jours."
                : "Cette unité n'a aucune chambre active : complétez le référentiel des chambres.";
            return;
        }

        PlanningEmptyStatePanel.Visibility = Visibility.Collapsed;

        PlanningGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PlanningRoomColumnWidth) });

        for (var day = 0; day < PlanningDays; day++)
        {
            PlanningGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PlanningDayWidth) });
        }

        PlanningGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(PlanningHeaderHeight) });

        for (var row = 0; row < planningRooms.Count; row++)
        {
            PlanningGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(PlanningRowHeight) });
        }

        AddPlanningHeader();

        for (var row = 0; row < planningRooms.Count; row++)
        {
            AddPlanningRoomRow(planningRooms[row], row);
        }

        AddPlanningTodayHighlight();
        AddPlanningReservationBars();
    }

    private void AddPlanningHeader()
    {
        var cornerCell = new Border
        {
            BorderBrush = (Brush)FindResource("PanelBorderBrush"),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(10, 0, 0, 0),
            Child = new TextBlock
            {
                Text = "Chambre",
                Style = (Style)FindResource("MetricLabelText"),
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetRow(cornerCell, 0);
        Grid.SetColumn(cornerCell, 0);
        PlanningGrid.Children.Add(cornerCell);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var culture = CultureInfo.CurrentCulture;

        for (var day = 0; day < PlanningDays; day++)
        {
            var date = planningStart.AddDays(day);
            var isToday = date == today;

            var dayName = new TextBlock
            {
                Text = culture.DateTimeFormat.GetAbbreviatedDayName(date.DayOfWeek),
                Style = (Style)FindResource("CaptionText"),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var dayDate = new TextBlock
            {
                Text = date.ToString("dd/MM", culture),
                Style = (Style)FindResource("CaptionText"),
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            if (isToday)
            {
                dayName.Foreground = (Brush)FindResource("PrimaryBrush");
                dayDate.Foreground = (Brush)FindResource("PrimaryBrush");
            }

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(dayName);
            stack.Children.Add(dayDate);

            // AUJOURD'HUI est marque par un liseret accent sous son en-tete.
            var headerCell = new Border
            {
                BorderBrush = isToday
                    ? (Brush)FindResource("AccentBrush")
                    : (Brush)FindResource("PanelBorderBrush"),
                BorderThickness = isToday ? new Thickness(0, 0, 1, 2.5) : new Thickness(0, 0, 1, 1),
                Child = stack
            };

            Grid.SetRow(headerCell, 0);
            Grid.SetColumn(headerCell, day + 1);
            PlanningGrid.Children.Add(headerCell);
        }
    }

    private void AddPlanningRoomRow(RoomResponse room, int rowIndex)
    {
        var gridRow = rowIndex + 1;
        var isAltRow = rowIndex % 2 == 1;

        var numberText = new TextBlock
        {
            Text = room.Number,
            Style = (Style)FindResource("BodyText"),
            FontWeight = FontWeights.SemiBold
        };

        var typeText = new TextBlock
        {
            Text = room.RoomTypeCode,
            Style = (Style)FindResource("CaptionText")
        };

        var labelStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        labelStack.Children.Add(numberText);
        labelStack.Children.Add(typeText);

        var labelCell = new Border
        {
            Background = isAltRow ? (Brush)FindResource("RowAltBrush") : (Brush)FindResource("SurfaceBrush"),
            BorderBrush = (Brush)FindResource("PanelBorderBrush"),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(10, 0, 0, 0),
            Child = labelStack
        };
        Grid.SetRow(labelCell, gridRow);
        Grid.SetColumn(labelCell, 0);
        PlanningGrid.Children.Add(labelCell);

        for (var day = 0; day < PlanningDays; day++)
        {
            var dayCell = new Border
            {
                Background = isAltRow ? (Brush)FindResource("RowAltBrush") : (Brush)FindResource("SurfaceBrush"),
                BorderBrush = (Brush)FindResource("PanelBorderBrush"),
                BorderThickness = new Thickness(0, 0, 1, 1)
            };
            Grid.SetRow(dayCell, gridRow);
            Grid.SetColumn(dayCell, day + 1);
            PlanningGrid.Children.Add(dayCell);
        }
    }

    // Colonne du jour : voile translucide accent sur toute la hauteur, pose
    // au-dessus des cellules et sous les barres.
    private void AddPlanningTodayHighlight()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayIndex = today.DayNumber - planningStart.DayNumber;

        if (todayIndex < 0 || todayIndex >= PlanningDays)
        {
            return;
        }

        var highlight = new Border
        {
            Background = (Brush)FindResource("AccentSelectionBrush"),
            IsHitTestVisible = false
        };
        Grid.SetRow(highlight, 1);
        Grid.SetRowSpan(highlight, Math.Max(1, planningRooms.Count));
        Grid.SetColumn(highlight, todayIndex + 1);
        PlanningGrid.Children.Add(highlight);
    }

    // Chaque reservation est UNE barre continue de l'arrivee au depart (nuit du
    // depart exclue - la chambre est libre ce jour-la), tronquee proprement aux
    // bords de la fenetre, coloree par statut avec les teintes Status* du theme.
    private void AddPlanningReservationBars()
    {
        var roomRowByRoomId = new Dictionary<Guid, int>();

        for (var index = 0; index < planningRooms.Count; index++)
        {
            roomRowByRoomId[planningRooms[index].Id] = index;
        }

        foreach (var reservation in planningReservations)
        {
            // Seuls les sejours qui TIENNENT la chambre figurent sur le tape chart -
            // demandes, annulations et no-shows n'encombrent pas le planning. La
            // definition vient du domaine (Blocks), jamais d'une liste locale de statuts.
            if (!reservation.Status.Blocks())
            {
                continue;
            }

            // Une reservation sans chambre affectee (vente au type) n'a pas de ligne sur
            // le planning par chambre : elle tient l'inventaire, pas une chambre precise.
            if (reservation.RoomId is not { } planningRoomId
                || !roomRowByRoomId.TryGetValue(planningRoomId, out var roomRow))
            {
                continue;
            }

            var startIndex = reservation.ArrivalDate.DayNumber - planningStart.DayNumber;
            var endIndex = reservation.DepartureDate.DayNumber - planningStart.DayNumber;

            var clampedStart = Math.Max(startIndex, 0);
            var clampedEnd = Math.Min(endIndex, PlanningDays);
            var span = clampedEnd - clampedStart;

            if (span <= 0)
            {
                continue;
            }

            var bar = CreateReservationBar(reservation, truncatedLeft: startIndex < 0, truncatedRight: endIndex > PlanningDays);

            Grid.SetRow(bar, roomRow + 1);
            Grid.SetColumn(bar, clampedStart + 1);
            Grid.SetColumnSpan(bar, span);
            PlanningGrid.Children.Add(bar);

            if (selectedStay?.Id == reservation.Id)
            {
                HighlightPlanningBar(bar);
            }
        }
    }

    private Border CreateReservationBar(ReservationResponse reservation, bool truncatedLeft, bool truncatedRight)
    {
        var (label, backgroundKey, foregroundKey) = DescribeStatus(reservation.Status);

        var text = new TextBlock
        {
            Text = reservation.CustomerCode,
            Style = (Style)FindResource("CaptionText"),
            Foreground = (Brush)FindResource(foregroundKey),
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 6, 0)
        };

        var bar = new Border
        {
            Height = 28,
            VerticalAlignment = VerticalAlignment.Center,
            Background = (Brush)FindResource(backgroundKey),
            BorderBrush = (Brush)FindResource(foregroundKey),
            BorderThickness = new Thickness(1),
            // Coins arrondis, aplatis du cote ou le sejour continue hors fenetre.
            CornerRadius = new CornerRadius(
                truncatedLeft ? 0 : 7,
                truncatedRight ? 0 : 7,
                truncatedRight ? 0 : 7,
                truncatedLeft ? 0 : 7),
            Margin = new Thickness(truncatedLeft ? 0 : 3, 0, truncatedRight ? 0 : 3, 0),
            Cursor = Cursors.Hand,
            Tag = reservation,
            Child = text,
            ToolTip =
                $"Client {reservation.CustomerCode}\n" +
                $"Chambre {reservation.RoomNumber ?? "?"}\n" +
                $"Du {FormatDate(reservation.ArrivalDate)} au {FormatDate(reservation.DepartureDate)} " +
                $"({FormatNights(reservation.Nights)}, {FormatGuests(reservation.GuestCount)})\n" +
                $"Nuitée d'arrivée : {FormatAmount(reservation.NightlyRateSnapshot)} — plan {reservation.RatePlanCodeSnapshot}\n" +
                $"Statut : {label} — cliquez pour ouvrir la carte de séjour (solde inclus)"
        };

        if (reservation.Status == ReservationStatus.CheckedOut)
        {
            // Sejours termines attenues : ils documentent, ils n'appellent plus
            // d'action.
            bar.Opacity = 0.55;
        }

        bar.MouseLeftButtonUp += PlanningBar_MouseLeftButtonUp;

        return bar;
    }

    private async void PlanningBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border bar || bar.Tag is not ReservationResponse reservation)
        {
            return;
        }

        e.Handled = true;
        HighlightPlanningBar(bar);
        SetSelectedStay(StaySelection.FromReservation(reservation));

        // Le solde vient du serveur : la carte de sejour le charge avec le folio
        // des que le sejour en a un (a partir du check-in).
        if (reservation.Status is ReservationStatus.CheckedIn or ReservationStatus.CheckedOut)
        {
            var current = context;

            if (current is null || !current.ApiClient.IsAuthenticated)
            {
                return;
            }

            await current.RunAsync(() => LoadFolioForStayAsync(current));
        }
    }

    private void HighlightPlanningBar(Border bar)
    {
        if (selectedPlanningBar is not null && selectedPlanningBar.Tag is ReservationResponse previous)
        {
            var (_, _, foregroundKey) = DescribeStatus(previous.Status);
            selectedPlanningBar.BorderBrush = (Brush)FindResource(foregroundKey);
            selectedPlanningBar.BorderThickness = new Thickness(1);
        }

        selectedPlanningBar = bar;
        bar.BorderBrush = (Brush)FindResource("AccentBrush");
        bar.BorderThickness = new Thickness(2);
    }

    // ------------------------------ Carte de sejour ------------------------------

    // Met a jour la carte de sejour et le rattachement du folio. Aucun appel
    // reseau ici : les appelants chargent le folio via RunAsync quand il existe.
    private void SetSelectedStay(StaySelection? stay)
    {
        selectedStay = stay;
        selectedStayBalance = null;

        if (stay is null)
        {
            StayCardPanel.Visibility = Visibility.Collapsed;
            StayCardEmptyPanel.Visibility = Visibility.Visible;
            ResetFolioPanel();
            UpdateActionStates();
            return;
        }

        StayCardEmptyPanel.Visibility = Visibility.Collapsed;
        StayCardPanel.Visibility = Visibility.Visible;

        StayCardTitleTextBlock.Text = $"Chambre {stay.RoomNumber} — client {stay.CustomerDisplay}";
        StayCardSubtitleTextBlock.Text = stay.GuestCount is int guests
            ? $"{FormatNights(stay.Nights)} · {FormatGuests(guests)}"
            : FormatNights(stay.Nights);
        StayDatesTextBlock.Text = $"{FormatDate(stay.ArrivalDate)} → {FormatDate(stay.DepartureDate)}";
        StayNightsTextBlock.Text = FormatNights(stay.Nights);
        StayRateTextBlock.Text = stay.NightlyRate is decimal rate ? FormatAmount(rate) : "—";
        StayPlanTextBlock.Text = stay.RatePlanCode is null ? string.Empty : $"Plan {stay.RatePlanCode}";

        var (label, backgroundKey, foregroundKey) = DescribeStatus(stay.Status);
        StayStatusBadgeTextBlock.Text = label;
        StayStatusBadgeBorder.Background = (Brush)FindResource(backgroundKey);
        StayStatusBadgeTextBlock.Foreground = (Brush)FindResource(foregroundKey);

        StayBalanceTextBlock.Text = "—";
        StayBalanceHintTextBlock.Text = stay.Status is ReservationStatus.CheckedIn or ReservationStatus.CheckedOut
            ? "Chargé avec le folio (serveur)"
            : "Le folio s'ouvre au check-in";

        // Le folio affiche se rattache toujours a la selection courante.
        FolioChargesDataGrid.ItemsSource = null;
        FolioBalanceTextBlock.Text = "—";
        FolioCaptionTextBlock.Text = stay.Status is ReservationStatus.CheckedIn or ReservationStatus.CheckedOut
            ? $"Folio de la chambre {stay.RoomNumber} — client {stay.CustomerDisplay} : chargement depuis la Réception ou le Planning."
            : "Le folio s'ouvre au check-in : ce séjour n'en a pas encore.";

        UpdateActionStates();
    }

    private async void StayCheckInButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;
        var stay = selectedStay;

        if (current is null || stay is null || !stay.Status.IsPreArrival())
        {
            return;
        }

        var confirmed = Confirm(
            $"Enregistrer l'arrivée du client {stay.CustomerDisplay} " +
            $"(chambre {stay.RoomNumber}, du {FormatDate(stay.ArrivalDate)} au {FormatDate(stay.DepartureDate)}) ?\n\n" +
            "Le folio du séjour sera ouvert avec une ligne par nuit au tarif figé à la réservation.",
            "Check-in d'une réservation");

        if (!confirmed)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            var updated = await current.ApiClient.CheckInReservationAsync(current.ApiBaseUrl, stay.Id);
            await LoadPlanningAsync(current);
            SetSelectedStay(StaySelection.FromReservation(updated));
            await LoadFolioForStayAsync(current);
            current.SetStatus($"Check-in enregistré pour la chambre {stay.RoomNumber} — le folio du séjour est ouvert.");
        });
    }

    private async void StayCheckOutButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;
        var stay = selectedStay;

        if (current is null || stay is null || stay.Status != ReservationStatus.CheckedIn)
        {
            return;
        }

        if (selectedStayBalance is null)
        {
            current.SetStatus(
                "Le solde du folio n'est pas encore chargé : rouvrez la carte de séjour puis réessayez.",
                isError: true);
            return;
        }

        if (selectedStayBalance.Value != 0m)
        {
            current.SetStatus(
                $"Check-out refusé : solde de {FormatAmount(selectedStayBalance.Value)}. " + BalanceDuePathMessage,
                isError: true);
            return;
        }

        var confirmed = Confirm(
            $"Enregistrer le départ du client {stay.CustomerDisplay} " +
            $"(chambre {stay.RoomNumber}, séjour du {FormatDate(stay.ArrivalDate)} au {FormatDate(stay.DepartureDate)}) ?\n\n" +
            "Le check-out est définitif : le séjour sera clos et son folio figé, sans réouverture possible.",
            "Check-out d'une réservation");

        if (!confirmed)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.CheckOutReservationAsync(current.ApiBaseUrl, stay.Id);
            await LoadPlanningAsync(current);
            current.SetStatus($"Check-out enregistré pour la chambre {stay.RoomNumber}.");
        });
    }

    private async void StayNoShowButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;
        var stay = selectedStay;

        if (current is null || stay is null || !stay.Status.IsPreArrival())
        {
            return;
        }

        var confirmed = Confirm(
            $"Constater le no-show du client {stay.CustomerDisplay} " +
            $"(chambre {stay.RoomNumber}, arrivée prévue le {FormatDate(stay.ArrivalDate)}) ?\n\n" +
            "La réservation sera définitivement close et la chambre libérée.",
            "No-show d'une réservation");

        if (!confirmed)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.MarkReservationNoShowAsync(current.ApiBaseUrl, stay.Id);
            await LoadPlanningAsync(current);
            current.SetStatus($"No-show constaté pour le client {stay.CustomerDisplay}.");
        });
    }

    private async void StayCancelButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;
        var stay = selectedStay;

        if (current is null || stay is null || !stay.Status.IsPreArrival())
        {
            return;
        }

        var reason = StayCancelReasonTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(reason))
        {
            current.SetStatus("Le motif d'annulation est requis.", isError: true);
            return;
        }

        var confirmed = Confirm(
            $"Annuler la réservation du client {stay.CustomerDisplay} " +
            $"(chambre {stay.RoomNumber}, arrivée le {FormatDate(stay.ArrivalDate)}) ?\n\n" +
            $"Motif : {reason}\n\nL'annulation est définitive.",
            "Annulation d'une réservation");

        if (!confirmed)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.CancelReservationAsync(
                current.ApiBaseUrl,
                stay.Id,
                new CancelReservationRequest(reason));

            StayCancelReasonTextBox.Text = string.Empty;

            await LoadPlanningAsync(current);
            current.SetStatus($"Réservation du client {stay.CustomerDisplay} annulée.");
        });
    }

    private async void StayFolioButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;
        var stay = selectedStay;

        if (current is null || stay is null)
        {
            return;
        }

        LodgingTabs.SelectedIndex = FolioTabIndex;

        if (stay.Status is ReservationStatus.CheckedIn or ReservationStatus.CheckedOut)
        {
            await current.RunAsync(() => LoadFolioForStayAsync(current));
        }
    }

    // ================================== 4. Folio =================================

    // Charge le folio du sejour selectionne : lignes, solde (serveur) - alimente
    // a la fois l'onglet Folio et le solde de la carte de sejour.
    private async Task LoadFolioForStayAsync(ModuleViewContext current)
    {
        var stay = selectedStay;

        if (stay is null)
        {
            return;
        }

        var folio = await current.ApiClient.GetReservationFolioAsync(current.ApiBaseUrl, stay.Id);

        FolioChargesDataGrid.ItemsSource = folio.Charges
            .OrderBy(charge => charge.LineNumber)
            .Select(ToFolioChargeRow)
            .ToArray();

        FolioBalanceTextBlock.Text = FormatAmount(folio.Balance);
        FolioCaptionTextBlock.Text =
            $"Folio de la chambre {stay.RoomNumber} — client {stay.CustomerDisplay} " +
            $"(séjour du {FormatDate(stay.ArrivalDate)} au {FormatDate(stay.DepartureDate)}).";

        selectedStayBalance = folio.Balance;
        StayBalanceTextBlock.Text = FormatAmount(folio.Balance);
        StayBalanceHintTextBlock.Text = folio.Balance == 0m
            ? "Soldé — check-out possible"
            : "À solder avant le check-out";
    }

    private async void AddChargeButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;
        var stay = selectedStay;

        if (current is null)
        {
            return;
        }

        if (stay is null || stay.Status != ReservationStatus.CheckedIn)
        {
            current.SetStatus("Sélectionnez un séjour en cours (Réception ou Planning) pour compléter son folio.", isError: true);
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
                stay.Id,
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
            FolioBalanceTextBlock.Text = FormatAmount(folio.Balance);

            selectedStayBalance = folio.Balance;
            StayBalanceTextBlock.Text = FormatAmount(folio.Balance);
            StayBalanceHintTextBlock.Text = folio.Balance == 0m
                ? "Soldé — check-out possible"
                : "À solder avant le check-out";

            current.SetStatus($"Ligne ajoutée au folio — nouveau solde : {FormatAmount(folio.Balance)}.");
        });
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

    // ======================= 5. Referentiel et occupation ========================

    private async void RefreshReferentialButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || !current.ApiClient.IsAuthenticated)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await LoadRoomsReferentialAsync(current);
            current.SetStatus("Référentiel des chambres actualisé.");
        });
    }

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
    // declencher une erreur API previsible. L'etat metier du sejour selectionne
    // est croise avec les droits lodging.write / lodging.checkin du profil. Les
    // boutons generes par ligne (check-in, check-out, reserver...) portent leur
    // etat dans leur ligne, recalcule a chaque rechargement - meme symetrie que
    // ApplyPermissionHint, sans etat residuel possible.
    private void UpdateActionStates()
    {
        var stay = selectedStay;

        StayCheckInButton.IsEnabled = canCheckin && stay is not null && stay.Status.IsPreArrival();
        StayCheckOutButton.IsEnabled = canCheckin && stay?.Status == ReservationStatus.CheckedIn;
        StayNoShowButton.IsEnabled = canWrite && stay is not null && stay.Status.IsPreArrival();
        StayCancelButton.IsEnabled = canWrite && stay is not null && stay.Status.IsPreArrival();
        StayFolioButton.IsEnabled = stay is not null;
        AddChargeButton.IsEnabled = canCheckin && stay?.Status == ReservationStatus.CheckedIn;
        SearchCustomerButton.IsEnabled = canReadCustomers;

        ApplyPermissionHint(StayCheckInButton, canCheckin, CheckinPermissionHint);
        ApplyPermissionHint(StayCheckOutButton, canCheckin, CheckinPermissionHint);
        ApplyPermissionHint(AddChargeButton, canCheckin, CheckinPermissionHint);
        ApplyPermissionHint(StayNoShowButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(StayCancelButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(SearchCustomerButton, canReadCustomers, CustomersReadPermissionHint);
    }

    // Pose le message d'explication quand le droit manque, et RESTAURE l'info-bulle
    // d'origine du bouton quand il est present : l'affectation doit etre symetrique,
    // les vues survivant a la deconnexion (meme motif que Treasury/Settings/Users).
    private void ApplyPermissionHint(Button button, bool allowed, string hint)
    {
        if (!originalToolTips.ContainsKey(button))
        {
            originalToolTips[button] = button.ToolTip;
        }

        button.ToolTip = allowed ? originalToolTips[button] : hint;
    }

    // ================================ Utilitaires ================================

    // Source unique des libelles francais du statut de reservation et de ses
    // teintes semantiques : grille, barres du planning, badge de la carte de
    // sejour rendent le meme mot et la meme couleur.
    private static (string Label, string BackgroundKey, string ForegroundKey) DescribeStatus(ReservationStatus status) => status switch
    {
        ReservationStatus.Inquiry => ("Demande", "StatusDraftBackgroundBrush", "StatusDraftForegroundBrush"),
        ReservationStatus.Option => ("Option", "StatusSubmittedBackgroundBrush", "StatusSubmittedForegroundBrush"),
        ReservationStatus.Confirmed => ("Confirmée", "StatusSubmittedBackgroundBrush", "StatusSubmittedForegroundBrush"),
        ReservationStatus.Guaranteed => ("Garantie", "StatusSubmittedBackgroundBrush", "StatusSubmittedForegroundBrush"),
        ReservationStatus.CheckedIn => ("En séjour", "StatusValidatedBackgroundBrush", "StatusValidatedForegroundBrush"),
        ReservationStatus.CheckedOut => ("Terminée", "StatusDraftBackgroundBrush", "StatusDraftForegroundBrush"),
        ReservationStatus.Cancelled => ("Annulée", "StatusRejectedBackgroundBrush", "StatusRejectedForegroundBrush"),
        _ => ("No-show", "StatusRejectedBackgroundBrush", "StatusRejectedForegroundBrush")
    };

    private static string FormatDate(DateOnly date) =>
        date.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);

    private static string FormatAmount(decimal amount) =>
        amount.ToString("N2", CultureInfo.CurrentCulture);

    private static string FormatNights(int nights) =>
        nights == 1 ? "1 nuit" : $"{nights} nuits";

    private static string FormatGuests(int guests) =>
        guests == 1 ? "1 pers." : $"{guests} pers.";

    private static string FormatCustomer(string code, string? name) =>
        string.IsNullOrWhiteSpace(name) ? code : $"{name} ({code})";

    // Montants : culture du poste d'abord, repli invariant (meme tolerance de
    // saisie que TryReadMoney dans MainWindow). Un reglement se saisit en negatif.
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

    // ============================ Projections d'ecran ============================

    /// <summary>
    /// Ligne d'arrivee de la reception (du jour ou en retard) : les etats des
    /// boutons y sont figes a la construction, recalcules a chaque rechargement.
    /// </summary>
    private sealed record ArrivalRow(
        Guid ReservationId,
        string RoomNumber,
        string CustomerDisplay,
        string CustomerCode,
        string StayText,
        string NightsGuestsText,
        string RateText,
        string TotalText,
        string LatenessText,
        DateOnly ArrivalDate,
        DateOnly DepartureDate,
        bool CanCheckIn,
        string? CheckInToolTip,
        bool CanDecide,
        string? DecideToolTip,
        string? CancelToolTip);

    /// <summary>
    /// Ligne de depart de la reception (du jour ou en retard) : le solde vient du
    /// serveur, jamais recalcule ici.
    /// </summary>
    private sealed record DepartureRow(
        Guid ReservationId,
        string RoomNumber,
        string CustomerDisplay,
        string CustomerCode,
        string StayText,
        DateOnly ArrivalDate,
        DateOnly DepartureDate,
        decimal? Balance,
        string BalanceText,
        bool IsBalanceDue,
        bool CanCheckOut,
        string? CheckOutToolTip);

    /// <summary>
    /// Carte de chambre disponible : tarifs et total renvoyes par le serveur ;
    /// une chambre sans tarif reste visible mais grisee et non reservable.
    /// </summary>
    private sealed record AvailableRoomRow(
        Guid RoomId,
        string RoomNumber,
        string RoomTypeText,
        string PlanText,
        string RateLabel,
        string RateText,
        string? NightDetailToolTip,
        string TotalText,
        bool IsRateMissing,
        bool CanBook,
        string? BookToolTip,
        AvailableRoomResponse Source);

    /// <summary>Client retenu pour la reservation, choisi parmi la recherche.</summary>
    private sealed record CustomerOption(string Code, string Label);

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

    /// <summary>
    /// Sejour selectionne, quel que soit l'ecran d'origine (barre du planning ou
    /// ligne de la reception) : la carte de sejour et l'onglet Folio s'y
    /// rattachent. Le tarif est nul quand l'ecran d'origine ne le portait pas.
    /// </summary>
    private sealed record StaySelection(
        Guid Id,
        string RoomNumber,
        string CustomerDisplay,
        string CustomerCode,
        DateOnly ArrivalDate,
        DateOnly DepartureDate,
        int Nights,
        int? GuestCount,
        ReservationStatus Status,
        decimal? NightlyRate,
        string? RatePlanCode)
    {
        public static StaySelection FromReservation(ReservationResponse reservation) => new(
            reservation.Id,
            reservation.RoomNumber ?? "?",
            reservation.CustomerCode,
            reservation.CustomerCode,
            reservation.ArrivalDate,
            reservation.DepartureDate,
            reservation.Nights,
            reservation.GuestCount,
            reservation.Status,
            reservation.NightlyRateSnapshot,
            reservation.RatePlanCodeSnapshot);
    }
}
