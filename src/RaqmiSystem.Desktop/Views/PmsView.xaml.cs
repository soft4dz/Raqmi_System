using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module 10 - PMS hotelier, front office : planning, arrivees, departs, clients presents,
/// previsionnel, hors service, regles de vente et night audit.
///
/// CETTE VUE NE CALCULE RIEN. Elle n'a ni compteur d'inventaire, ni soustraction de disponibilite,
/// ni regle de vente locale : tout vient du serveur, qui applique un calcul unique partage par la
/// recherche, la creation et le previsionnel. Une vue qui recalculerait finirait par afficher autre
/// chose que ce que la vente accepte, et l'ecart se paierait en survente.
///
/// Les boutons d'ecriture sont grises selon les permissions du profil - confort d'interface
/// seulement : le refus fait autorite cote serveur.
/// </summary>
public partial class PmsView : UserControl
{
    private ModuleViewContext? context;

    private IReadOnlyCollection<RoomResponse> rooms = [];
    private TapeChartResponse? tapeChart;

    public PmsView()
    {
        InitializeComponent();

        var today = DateTime.Today;
        FromDatePicker.SelectedDate = today;
        ToDatePicker.SelectedDate = today.AddDays(14);
        BlockFromDatePicker.SelectedDate = today;
        BlockToDatePicker.SelectedDate = today.AddDays(1);

        foreach (var category in Enum.GetValues<RoomBlockCategory>())
        {
            BlockCategoryComboBox.Items.Add(new ComboBoxItem { Content = DescribeCategory(category), Tag = category });
        }

        BlockCategoryComboBox.SelectedIndex = 0;
    }

    /// <summary>Memorise le contexte fourni par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext moduleContext)
    {
        context = moduleContext;
        ApplyPermissions();
    }

    /// <summary>Vide l'ecran a la deconnexion.</summary>
    public void ResetState()
    {
        UnitComboBox.ItemsSource = null;
        UnassignedGrid.ItemsSource = null;
        ArrivalsGrid.ItemsSource = null;
        DeparturesGrid.ItemsSource = null;
        InHouseGrid.ItemsSource = null;
        ForecastGrid.ItemsSource = null;
        BlocksGrid.ItemsSource = null;
        RestrictionsGrid.ItemsSource = null;
        OverbookingGrid.ItemsSource = null;
        NightAuditFindingsGrid.ItemsSource = null;
        BlockRoomComboBox.ItemsSource = null;
        TapeChartGrid.Children.Clear();
        TapeChartGrid.RowDefinitions.Clear();
        TapeChartGrid.ColumnDefinitions.Clear();
        NightAuditReportText.Text = string.Empty;
        BusinessDateText.Text = "Date métier : —";
        tapeChart = null;
        rooms = [];
    }

    /// <summary>(Re)charge l'ecran. Sort sans bruit tant qu'aucune session n'est ouverte.</summary>
    public async Task LoadAsync()
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            if (UnitComboBox.ItemsSource is null)
            {
                var units = await moduleContext.ApiClient.GetHotelUnitsAsync(
                    moduleContext.ApiBaseUrl,
                    includeInactive: false);

                UnitComboBox.ItemsSource = units;
                UnitComboBox.SelectedItem = units.FirstOrDefault();
            }

            await ReloadAsync(moduleContext);
        });
    }

    private string? SelectedUnitCode => (UnitComboBox.SelectedItem as HotelUnitResponse)?.Code;

    private async Task ReloadAsync(ModuleViewContext moduleContext)
    {
        var unitCode = SelectedUnitCode;

        if (string.IsNullOrWhiteSpace(unitCode))
        {
            return;
        }

        var from = DateOnly.FromDateTime(FromDatePicker.SelectedDate ?? DateTime.Today);
        var to = DateOnly.FromDateTime(ToDatePicker.SelectedDate ?? DateTime.Today.AddDays(14));

        if (to <= from)
        {
            to = from.AddDays(1);
            ToDatePicker.SelectedDate = to.ToDateTime(TimeOnly.MinValue);
        }

        var businessDate = await moduleContext.ApiClient.GetBusinessDateAsync(moduleContext.ApiBaseUrl, unitCode);

        BusinessDateText.Text = businessDate.IsLate
            ? $"Date métier : {businessDate.BusinessDate:dd/MM/yyyy} — {businessDate.PendingDays} journée(s) en attente de clôture"
            : $"Date métier : {businessDate.BusinessDate:dd/MM/yyyy}";

        rooms = await moduleContext.ApiClient.GetRoomsAsync(moduleContext.ApiBaseUrl, unitCode, includeInactive: false);
        BlockRoomComboBox.ItemsSource = rooms;

        tapeChart = await moduleContext.ApiClient.GetTapeChartAsync(moduleContext.ApiBaseUrl, unitCode, from, to);
        RenderTapeChart(tapeChart);
        UnassignedGrid.ItemsSource = tapeChart.UnassignedStays;

        var arrivals = await moduleContext.ApiClient.GetArrivalsAsync(moduleContext.ApiBaseUrl, unitCode, null);
        ArrivalsGrid.ItemsSource = arrivals.Arrivals;
        ArrivalSummaryText.Text =
            $"{arrivals.Arrivals.Count} arrivée(s) attendue(s), {arrivals.ExpectedGuests} personne(s) — "
            + $"{arrivals.UnassignedCount} sans chambre, {arrivals.NotReadyCount} chambre(s) non prête(s)";

        var departures = await moduleContext.ApiClient.GetDeparturesAsync(moduleContext.ApiBaseUrl, unitCode, null);
        DeparturesGrid.ItemsSource = departures.Departures;
        DepartureSummaryText.Text =
            $"{departures.PendingCount} départ(s) à traiter — solde restant dû : "
            + departures.OutstandingBalance.ToString("N2", CultureInfo.CurrentCulture);

        InHouseGrid.ItemsSource = await moduleContext.ApiClient.GetInHouseAsync(moduleContext.ApiBaseUrl, unitCode);

        await ReloadForecastAsync(moduleContext, unitCode, from);
        await ReloadInventoryAsync(moduleContext, unitCode, from, to);
    }

    private async Task ReloadForecastAsync(ModuleViewContext moduleContext, string unitCode, DateOnly from)
    {
        var days = ReadSelectedTag(ForecastHorizonComboBox, 14);
        var forecast = await moduleContext.ApiClient.GetForecastAsync(moduleContext.ApiBaseUrl, unitCode, from, days);

        ForecastGrid.ItemsSource = forecast.Entries;
        ForecastSummaryText.Text =
            $"Occupation moyenne {forecast.AverageOccupancyPercent:N2} % — "
            + $"CA chambres {forecast.TotalRoomRevenue:N2} — "
            + $"ADR {forecast.AverageAdr:N2} — RevPAR {forecast.AverageRevPar:N2}";
    }

    private async Task ReloadInventoryAsync(
        ModuleViewContext moduleContext,
        string unitCode,
        DateOnly from,
        DateOnly to)
    {
        BlocksGrid.ItemsSource = await moduleContext.ApiClient.GetRoomBlocksAsync(
            moduleContext.ApiBaseUrl,
            unitCode,
            from,
            to,
            kind: null,
            includeClosed: IncludeClosedBlocksCheckBox.IsChecked == true);

        RestrictionsGrid.ItemsSource = await moduleContext.ApiClient.GetRestrictionsAsync(
            moduleContext.ApiBaseUrl,
            unitCode,
            from,
            to,
            includeInactive: true);

        OverbookingGrid.ItemsSource = await moduleContext.ApiClient.GetOverbookingAsync(
            moduleContext.ApiBaseUrl,
            unitCode,
            from,
            to,
            includeInactive: true);
    }

    // ==================================== Planning graphique ====================================

    /// <summary>
    /// Dessine le planning : une ligne par chambre, une colonne par jour, un bloc par sejour ou par
    /// blocage. Le dessin se fait en code plutot qu'en liaison de donnees parce qu'un bloc s'etend
    /// sur PLUSIEURS colonnes - ce qu'aucune grille de donnees ne sait exprimer.
    /// </summary>
    private void RenderTapeChart(TapeChartResponse chart)
    {
        TapeChartGrid.Children.Clear();
        TapeChartGrid.RowDefinitions.Clear();
        TapeChartGrid.ColumnDefinitions.Clear();

        var days = chart.To.DayNumber - chart.From.DayNumber;

        if (days <= 0)
        {
            return;
        }

        TapeChartGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

        for (var index = 0; index < days; index++)
        {
            TapeChartGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        }

        TapeChartGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });

        for (var index = 0; index < days; index++)
        {
            var day = chart.From.AddDays(index);

            var header = new TextBlock
            {
                Text = day.ToString("dd/MM", CultureInfo.CurrentCulture),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetRow(header, 0);
            Grid.SetColumn(header, index + 1);
            TapeChartGrid.Children.Add(header);
        }

        var row = 1;

        foreach (var line in chart.Rows)
        {
            TapeChartGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });

            var label = new TextBlock
            {
                Text = $"{line.RoomNumber}  ({line.RoomTypeCode})",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 8, 0),
                ToolTip = line.HousekeepingStatus is null ? null : $"État ménage : {line.HousekeepingStatus}"
            };

            Grid.SetRow(label, row);
            Grid.SetColumn(label, 0);
            TapeChartGrid.Children.Add(label);

            foreach (var bar in line.Bars)
            {
                // Le bloc est borne a la fenetre affichee : un sejour qui commence avant ou finit
                // apres se dessine tronque, il ne disparait pas.
                var start = Math.Max(0, bar.From.DayNumber - chart.From.DayNumber);
                var end = Math.Min(days, bar.To.DayNumber - chart.From.DayNumber);
                var span = end - start;

                if (span <= 0)
                {
                    continue;
                }

                var block = new Border
                {
                    Background = ToBrush(bar.Colour),
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(1, 4, 1, 4),
                    ToolTip = DescribeBar(bar),
                    Child = new TextBlock
                    {
                        Text = bar.Label,
                        Foreground = Brushes.White,
                        FontSize = 11,
                        Margin = new Thickness(4, 0, 4, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    }
                };

                Grid.SetRow(block, row);
                Grid.SetColumn(block, start + 1);
                Grid.SetColumnSpan(block, span);
                TapeChartGrid.Children.Add(block);
            }

            row++;
        }
    }

    private static string DescribeBar(TapeChartBarResponse bar)
    {
        var parts = new List<string>();

        if (bar.Number is not null)
        {
            parts.Add($"Dossier {bar.Number}");
        }

        parts.Add($"{bar.From:dd/MM/yyyy} → {bar.To:dd/MM/yyyy} ({bar.Nights} nuit(s))");

        if (bar.CustomerName is not null)
        {
            parts.Add(bar.CustomerName);
        }

        if (bar.Status is not null)
        {
            parts.Add($"Statut : {bar.Status}");
        }

        if (bar.IsOverbooking)
        {
            parts.Add("SURRÉSERVATION : à reloger");
        }

        if (bar.Balance is { } balance && balance != 0m)
        {
            parts.Add($"Solde : {balance:N2}");
        }

        return string.Join(Environment.NewLine, parts);
    }

    private static Brush ToBrush(string? colour)
    {
        if (string.IsNullOrWhiteSpace(colour))
        {
            return Brushes.SlateGray;
        }

        try
        {
            return (Brush)new BrushConverter().ConvertFromString(colour)!;
        }
        catch (FormatException)
        {
            return Brushes.SlateGray;
        }
    }

    // ======================================== Handlers ========================================

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    private async void UnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (context is not null && UnitComboBox.SelectedItem is HotelUnitResponse)
        {
            await LoadAsync();
        }
    }

    private async void ForecastHorizonComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var moduleContext = context;
        var unitCode = SelectedUnitCode;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated || unitCode is null)
        {
            return;
        }

        var from = DateOnly.FromDateTime(FromDatePicker.SelectedDate ?? DateTime.Today);

        await moduleContext.RunAsync(() => ReloadForecastAsync(moduleContext, unitCode, from));
    }

    private async void IncludeClosedBlocksCheckBox_Changed(object sender, RoutedEventArgs e) => await LoadAsync();

    private async void AssignRoomButton_Click(object sender, RoutedEventArgs e)
    {
        if (ArrivalsGrid.SelectedItem is not ArrivalRowResponse arrival || context is null)
        {
            SetStatus("Sélectionnez une arrivée.");
            return;
        }

        var free = rooms
            .Where(room => room.RoomTypeCode == arrival.RoomTypeCode)
            .Select(room => room.Number)
            .ToArray();

        if (free.Length == 0)
        {
            SetStatus($"Aucune chambre de type {arrival.RoomTypeCode} dans le parc.", isError: true);
            return;
        }

        var answer = Prompt(
            $"Numéro de chambre à affecter au dossier {arrival.Number} (type {arrival.RoomTypeCode}).\n"
            + $"Chambres du type : {string.Join(", ", free)}.\n"
            + "Laissez vide pour libérer la chambre affectée.",
            "Affectation de chambre",
            arrival.RoomNumber ?? string.Empty);

        if (answer is null)
        {
            return;
        }

        var target = rooms.FirstOrDefault(room =>
            string.Equals(room.Number, answer.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(answer) && target is null)
        {
            SetStatus($"Chambre « {answer} » introuvable dans cette unité.", isError: true);
            return;
        }

        var moduleContext = context;

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.AssignRoomAsync(
                moduleContext.ApiBaseUrl,
                arrival.ReservationId,
                target?.Id,
                "Affectation depuis le tableau des arrivées.");

            await ReloadAsync(moduleContext);
            SetStatus(target is null ? "Chambre libérée." : $"Chambre {target.Number} affectée.");
        });
    }

    private async void CheckInButton_Click(object sender, RoutedEventArgs e)
    {
        if (ArrivalsGrid.SelectedItem is not ArrivalRowResponse arrival || context is null)
        {
            SetStatus("Sélectionnez une arrivée.");
            return;
        }

        if (arrival.RoomId is null)
        {
            SetStatus("Ce dossier n'a pas de chambre affectée : affectez-en une d'abord.", isError: true);
            return;
        }

        if (!arrival.RoomIsReady
            && !Confirm(
                $"La chambre {arrival.RoomNumber} n'est pas déclarée prête ({arrival.HousekeepingStatus}).\n"
                + "Enregistrer l'arrivée quand même ?",
                "Chambre non prête"))
        {
            return;
        }

        var moduleContext = context;

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.CheckInReservationAsync(moduleContext.ApiBaseUrl, arrival.ReservationId);
            await ReloadAsync(moduleContext);
            SetStatus($"Arrivée enregistrée pour le dossier {arrival.Number}.");
        });
    }

    private void WalkInButton_Click(object sender, RoutedEventArgs e)
    {
        // Le walk-in demande un client, une chambre et une date de depart : il se saisit dans
        // l'ecran Hebergement, qui porte deja le fichier client et la recherche de disponibilite.
        SetStatus(
            "Walk-in : utilisez l'écran « Hébergement & occupation » pour choisir le client et la chambre, "
            + "puis revenez ici pour la suite du séjour.");
    }

    private async void PrepareCheckOutButton_Click(object sender, RoutedEventArgs e)
    {
        if (DeparturesGrid.SelectedItem is not DepartureRowResponse departure || context is null)
        {
            SetStatus("Sélectionnez un départ.");
            return;
        }

        var moduleContext = context;

        await moduleContext.RunAsync(async () =>
        {
            var folios = await moduleContext.ApiClient.PrepareCheckOutAsync(
                moduleContext.ApiBaseUrl,
                departure.ReservationId);

            await ReloadAsync(moduleContext);

            var total = folios.Sum(folio => folio.Balance);
            SetStatus($"Note préparée pour le dossier {departure.Number} : solde {total:N2}.");
        });
    }

    private async void CheckOutButton_Click(object sender, RoutedEventArgs e)
    {
        if (DeparturesGrid.SelectedItem is not DepartureRowResponse departure || context is null)
        {
            SetStatus("Sélectionnez un départ.");
            return;
        }

        if (departure.Balance != 0m)
        {
            SetStatus(
                $"Solde non nul ({departure.Balance:N2}) : enregistrez le règlement avant le départ.",
                isError: true);

            return;
        }

        var moduleContext = context;

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.CheckOutReservationAsync(moduleContext.ApiBaseUrl, departure.ReservationId);
            await ReloadAsync(moduleContext);
            SetStatus($"Départ enregistré pour le dossier {departure.Number}.");
        });
    }

    private async void RoomMoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (InHouseGrid.SelectedItem is not InHouseGuestResponse guest || context is null)
        {
            SetStatus("Sélectionnez un client présent.");
            return;
        }

        var target = Prompt(
            $"Numéro de la nouvelle chambre pour le dossier {guest.Number} (actuellement {guest.RoomNumber}).",
            "Changement de chambre",
            string.Empty);

        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        var room = rooms.FirstOrDefault(current =>
            string.Equals(current.Number, target.Trim(), StringComparison.OrdinalIgnoreCase));

        if (room is null)
        {
            SetStatus($"Chambre « {target} » introuvable dans cette unité.", isError: true);
            return;
        }

        var reason = Prompt(
            "Motif du changement de chambre (obligatoire).",
            "Changement de chambre",
            string.Empty);

        if (string.IsNullOrWhiteSpace(reason))
        {
            SetStatus("Le motif du changement de chambre est obligatoire.", isError: true);
            return;
        }

        var moduleContext = context;

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.MoveRoomAsync(
                moduleContext.ApiBaseUrl,
                guest.ReservationId,
                room.Id,
                reason.Trim());

            await ReloadAsync(moduleContext);
            SetStatus($"Dossier {guest.Number} déplacé vers la chambre {room.Number}.");
        });
    }

    private async void ExtendStayButton_Click(object sender, RoutedEventArgs e)
    {
        if (InHouseGrid.SelectedItem is not InHouseGuestResponse guest || context is null)
        {
            SetStatus("Sélectionnez un client présent.");
            return;
        }

        var answer = Prompt(
            $"Nouvelle date de départ pour le dossier {guest.Number} (format jj/mm/aaaa).\n"
            + $"Départ actuel : {guest.DepartureDate:dd/MM/yyyy}.",
            "Prolongation de séjour",
            guest.DepartureDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture));

        if (string.IsNullOrWhiteSpace(answer)
            || !DateOnly.TryParseExact(answer.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var departure))
        {
            SetStatus("Date de départ invalide.", isError: true);
            return;
        }

        var moduleContext = context;

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.ExtendStayAsync(
                moduleContext.ApiBaseUrl,
                guest.ReservationId,
                new ExtendStayRequest(departure, "Prolongation demandée au comptoir."));

            await ReloadAsync(moduleContext);
            SetStatus($"Séjour {guest.Number} prolongé jusqu'au {departure:dd/MM/yyyy}.");
        });
    }

    private async void CreateBlockButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;
        var unitCode = SelectedUnitCode;

        if (moduleContext is null || unitCode is null)
        {
            return;
        }

        if (BlockRoomComboBox.SelectedItem is not RoomResponse room)
        {
            SetStatus("Choisissez la chambre à bloquer.", isError: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(BlockReasonTextBox.Text))
        {
            SetStatus("Le motif du blocage est obligatoire.", isError: true);
            return;
        }

        var from = DateOnly.FromDateTime(BlockFromDatePicker.SelectedDate ?? DateTime.Today);
        var to = DateOnly.FromDateTime(BlockToDatePicker.SelectedDate ?? DateTime.Today.AddDays(1));

        if (to <= from)
        {
            SetStatus("La date de fin doit être postérieure à la date de début (elle est exclue).", isError: true);
            return;
        }

        var kind = ReadSelectedTag(BlockKindComboBox, RoomBlockKind.OutOfOrder);
        var category = (RoomBlockCategory)((ComboBoxItem)BlockCategoryComboBox.SelectedItem).Tag;

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.CreateRoomBlockAsync(
                moduleContext.ApiBaseUrl,
                unitCode,
                new CreateRoomBlockRequest(
                    room.Id,
                    kind,
                    from,
                    to,
                    BlockReasonTextBox.Text.Trim(),
                    category,
                    string.IsNullOrWhiteSpace(BlockReferenceTextBox.Text) ? null : BlockReferenceTextBox.Text.Trim()));

            BlockReasonTextBox.Text = string.Empty;
            BlockReferenceTextBox.Text = string.Empty;

            await ReloadAsync(moduleContext);
            SetStatus($"Chambre {room.Number} retirée de l'exploitation du {from:dd/MM/yyyy} au {to:dd/MM/yyyy}.");
        });
    }

    private async void CloseBlockButton_Click(object sender, RoutedEventArgs e)
    {
        if (BlocksGrid.SelectedItem is not RoomBlockResponse block || context is null)
        {
            SetStatus("Sélectionnez un blocage.");
            return;
        }

        var answer = Prompt(
            $"Date réelle de remise en service de la chambre {block.RoomNumber} (jj/mm/aaaa).",
            "Remise en service",
            DateTime.Today.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture));

        if (string.IsNullOrWhiteSpace(answer)
            || !DateOnly.TryParseExact(answer.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var returnDate))
        {
            SetStatus("Date de remise en service invalide.", isError: true);
            return;
        }

        var moduleContext = context;

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.CloseRoomBlockAsync(moduleContext.ApiBaseUrl, block.Id, returnDate);
            await ReloadAsync(moduleContext);
            SetStatus($"Chambre {block.RoomNumber} remise en service — elle repart en état SALE.");
        });
    }

    private async void CancelBlockButton_Click(object sender, RoutedEventArgs e)
    {
        if (BlocksGrid.SelectedItem is not RoomBlockResponse block || context is null)
        {
            SetStatus("Sélectionnez un blocage.");
            return;
        }

        var reason = Prompt(
            $"Motif d'annulation du blocage de la chambre {block.RoomNumber}.",
            "Annulation du blocage",
            string.Empty);

        if (string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        var moduleContext = context;

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.CancelRoomBlockAsync(moduleContext.ApiBaseUrl, block.Id, reason.Trim());
            await ReloadAsync(moduleContext);
            SetStatus($"Blocage de la chambre {block.RoomNumber} annulé.");
        });
    }

    private async void DryRunNightAuditButton_Click(object sender, RoutedEventArgs e) => await RunNightAuditAsync(dryRun: true);

    private async void RunNightAuditButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Confirm(
                "Le passage du night audit pose les nuitées de la journée et ne peut être joué qu'une fois "
                + "par journée d'exploitation.\n\nContinuer ?",
                "Night audit"))
        {
            return;
        }

        await RunNightAuditAsync(dryRun: false);
    }

    private async Task RunNightAuditAsync(bool dryRun)
    {
        var moduleContext = context;
        var unitCode = SelectedUnitCode;

        if (moduleContext is null || unitCode is null)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var run = await moduleContext.ApiClient.RunNightAuditAsync(
                moduleContext.ApiBaseUrl,
                new RunNightAuditRequest(
                    unitCode,
                    BusinessDate: null,
                    DryRun: dryRun,
                    AutoNoShow: AutoNoShowCheckBox.IsChecked == true));

            NightAuditFindingsGrid.ItemsSource = run.Findings;
            NightAuditReportText.Text = run.Report ?? string.Empty;

            SetStatus(
                dryRun
                    ? $"Répétition du {run.BusinessDate:dd/MM/yyyy} : {run.Findings.Count(finding => finding.IsBlocking)} constat(s) bloquant(s), aucune écriture."
                    : $"Night audit du {run.BusinessDate:dd/MM/yyyy} : {run.PostedRoomNights} nuitée(s) posée(s), {run.SkippedAlreadyPosted} déjà posée(s).",
                isError: run.Status == NightAuditStatus.Blocked);

            if (!dryRun)
            {
                await ReloadAsync(moduleContext);
            }
        });
    }

    private async void NoShowReportButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;
        var unitCode = SelectedUnitCode;

        if (moduleContext is null || unitCode is null)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var sweep = await moduleContext.ApiClient.SweepNoShowsAsync(
                moduleContext.ApiBaseUrl,
                unitCode,
                businessDate: null,
                apply: false);

            NightAuditFindingsGrid.ItemsSource = sweep.Candidates
                .Select(candidate => new NightAuditFindingResponse(
                    "no_show.candidat",
                    $"Dossier {candidate.Number} — {candidate.CustomerName} — arrivée du "
                        + $"{candidate.ArrivalDate:dd/MM/yyyy} non présentée. Pénalité estimée : "
                        + candidate.EstimatedPenalty.ToString("N2", CultureInfo.CurrentCulture),
                    IsBlocking: false,
                    candidate.ReservationId,
                    candidate.RoomNumber))
                .ToArray();

            SetStatus($"{sweep.Candidates.Count} non-présentation(s) candidate(s) au {sweep.BusinessDate:dd/MM/yyyy}.");
        });
    }

    // ========================================= Aides =========================================

    /// <summary>
    /// Grise les actions d'ecriture que le profil ne detient pas. Confort d'interface uniquement :
    /// le serveur refuse de toute facon, mais decouvrir l'interdiction APRES avoir saisi un
    /// formulaire est une mauvaise facon de l'apprendre.
    /// </summary>
    private void ApplyPermissions()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        AssignRoomButton.IsEnabled = moduleContext.HasPermission(PermissionCatalog.LodgingReserve)
            || moduleContext.HasPermission(PermissionCatalog.LodgingWrite);

        ExtendStayButton.IsEnabled = AssignRoomButton.IsEnabled;

        CheckInButton.IsEnabled = moduleContext.HasPermission(PermissionCatalog.LodgingCheckin);

        PrepareCheckOutButton.IsEnabled = moduleContext.HasPermission(PermissionCatalog.LodgingCheckout)
            || moduleContext.HasPermission(PermissionCatalog.LodgingCheckin);

        CheckOutButton.IsEnabled = PrepareCheckOutButton.IsEnabled;

        RoomMoveButton.IsEnabled = moduleContext.HasPermission(PermissionCatalog.LodgingRoomMove)
            || moduleContext.HasPermission(PermissionCatalog.LodgingCheckin);

        var manageRooms = moduleContext.HasPermission(PermissionCatalog.LodgingManageRooms)
            || moduleContext.HasPermission(PermissionCatalog.LodgingWrite);

        CreateBlockButton.IsEnabled = manageRooms;
        CloseBlockButton.IsEnabled = manageRooms;
        CancelBlockButton.IsEnabled = manageRooms;

        var nightAudit = moduleContext.HasPermission(PermissionCatalog.LodgingNightAudit)
            || moduleContext.HasPermission(PermissionCatalog.LodgingWrite);

        RunNightAuditButton.IsEnabled = nightAudit;
        DryRunNightAuditButton.IsEnabled = nightAudit;
    }

    private void SetStatus(string message, bool isError = false) => context?.SetStatus(message, isError);

    private bool Confirm(string message, string caption)
    {
        var owner = Window.GetWindow(this);

        var result = owner is null
            ? MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            : MessageBox.Show(owner, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    /// <summary>
    /// Petite saisie modale. Rend null quand l'operateur annule - a distinguer d'une chaine vide,
    /// qui est une reponse valide (liberer une chambre, par exemple).
    /// </summary>
    private string? Prompt(string message, string caption, string initialValue)
    {
        var dialog = new PmsPromptWindow(message, caption, initialValue)
        {
            Owner = Window.GetWindow(this)
        };

        return dialog.ShowDialog() == true ? dialog.Answer : null;
    }

    private static int ReadSelectedTag(ComboBox comboBox, int fallback)
    {
        if (comboBox.SelectedItem is ComboBoxItem item
            && item.Tag is string text
            && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return fallback;
    }

    private static RoomBlockKind ReadSelectedTag(ComboBox comboBox, RoomBlockKind fallback)
    {
        if (comboBox.SelectedItem is ComboBoxItem item
            && item.Tag is string text
            && Enum.TryParse<RoomBlockKind>(text, out var value))
        {
            return value;
        }

        return fallback;
    }

    private static string DescribeCategory(RoomBlockCategory category) => category switch
    {
        RoomBlockCategory.Unspecified => "Non classée",
        RoomBlockCategory.Plumbing => "Plomberie",
        RoomBlockCategory.Electrical => "Électricité",
        RoomBlockCategory.Hvac => "Climatisation / chauffage",
        RoomBlockCategory.Furniture => "Mobilier / literie",
        RoomBlockCategory.Renovation => "Travaux / peinture",
        RoomBlockCategory.DeepCleaning => "Nettoyage approfondi",
        RoomBlockCategory.InternalUse => "Usage interne",
        RoomBlockCategory.Administrative => "Blocage administratif",
        RoomBlockCategory.Damage => "Sinistre",
        _ => category.ToString()
    };
}
