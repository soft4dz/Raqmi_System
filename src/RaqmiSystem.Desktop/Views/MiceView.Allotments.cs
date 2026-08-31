using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Mice;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Volet GROUPES de l'ecran 10.6 : allotements et rooming lists.
///
/// CE QUE L'ECRAN DOIT FAIRE COMPRENDRE, et qui n'est pas evident : un bloc RETIRE des chambres de
/// la vente publique. Les colonnes "Tenues / Prises / Restant" existent pour cela - elles disent
/// combien de chambres sont immobilisees et combien le groupe consomme reellement. Un bloc de 20
/// chambres dont 3 sont prises, c'est 17 chambres invendables affichees noir sur blanc.
///
/// Le "Restant" est calcule sur la nuit la PLUS CHARGEE, pas en moyenne : c'est ce soir-la qui
/// decide si le bloc peut etre reduit.
/// </summary>
public partial class MiceView
{
    private IReadOnlyList<RoomAllotmentResponse> allotments = [];

    private RoomAllotmentResponse? selectedAllotment;

    // ================================ Chargement ================================

    private async Task ReloadAllotmentsAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        allotments = (await moduleContext.ApiClient.GetAllotmentsAsync(
            moduleContext.ApiBaseUrl,
            SelectedUnitCode,
            FromDatePicker.SelectedDate is { } from ? DateOnly.FromDateTime(from) : null,
            ToDatePicker.SelectedDate is { } to ? DateOnly.FromDateTime(to) : null,
            IncludeClosedAllotmentsCheckBox.IsChecked == true)).ToList();

        var rows = allotments.Select(item => new AllotmentRow(item)).ToList();

        AllotmentsDataGrid.ItemsSource = rows;

        // Le chiffre qui compte pour l'exploitant : combien de chambres sont immobilisees en ce
        // moment par des blocs encore tenants.
        var immobilised = allotments
            .Where(item => item.IsHolding)
            .Sum(item => item.RemainingAtPeak);

        AllotmentSummaryTextBlock.Text = immobilised == 0
            ? "Aucune chambre immobilisée par un bloc sur cette période."
            : $"{immobilised} chambre(s) actuellement retirée(s) de la vente publique par des blocs.";

        AllotmentRoomTypeComboBox.ItemsSource = roomTypes;
        AllotmentCustomerComboBox.ItemsSource = customers;

        // On retrouve la selection par identifiant : recharger apres une action ne doit pas
        // refermer la rooming list que l'utilisateur avait ouverte.
        if (selectedAllotment is { } previous)
        {
            var match = allotments.FirstOrDefault(item => item.Id == previous.Id);

            if (match is not null)
            {
                AllotmentsDataGrid.SelectedItem = rows.First(row => row.Id == match.Id);
                selectedAllotment = match;
                await LoadRoomingListAsync(match);
                UpdateAllotmentButtons();
                return;
            }
        }

        selectedAllotment = null;
        RoomingListDataGrid.ItemsSource = null;
        RoomingListTitleTextBlock.Text = "Rooming list";
        RoomingListRejectedTextBlock.Text = string.Empty;
        UpdateAllotmentButtons();
    }

    private async void AllotmentsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AllotmentsDataGrid.SelectedItem is not AllotmentRow row)
        {
            return;
        }

        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        selectedAllotment = allotments.FirstOrDefault(item => item.Id == row.Id);
        UpdateAllotmentButtons();

        if (selectedAllotment is { } allotment)
        {
            await moduleContext.RunAsync(() => LoadRoomingListAsync(allotment));
        }
    }

    private async Task LoadRoomingListAsync(RoomAllotmentResponse allotment)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var list = await moduleContext.ApiClient.GetRoomingListAsync(moduleContext.ApiBaseUrl, allotment.Id);

        RoomingListDataGrid.ItemsSource = list.Entries;

        RoomingListTitleTextBlock.Text =
            $"Rooming list — {allotment.Reference} ({list.Entries.Count}/{allotment.RoomsHeld})";
    }

    // ================================ Actions ================================

    private async void CreateAllotmentButton_Click(object sender, RoutedEventArgs e)
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

        if (AllotmentCustomerComboBox.SelectedItem is not CustomerResponse customer)
        {
            moduleContext.SetStatus("Sélectionnez le client porteur du groupe.", isError: true);
            return;
        }

        if (AllotmentRoomTypeComboBox.SelectedItem is not RoomTypeResponse roomType)
        {
            moduleContext.SetStatus("Sélectionnez le type de chambre à tenir.", isError: true);
            return;
        }

        if (AllotmentArrivalDatePicker.SelectedDate is not { } arrival
            || AllotmentDepartureDatePicker.SelectedDate is not { } departure)
        {
            moduleContext.SetStatus("Renseignez les dates d'arrivée et de départ du bloc.", isError: true);
            return;
        }

        if (!int.TryParse(AllotmentRoomsTextBox.Text?.Trim(), out var roomsHeld) || roomsHeld <= 0)
        {
            moduleContext.SetStatus("Le nombre de chambres tenues doit être un entier positif.", isError: true);
            return;
        }

        DateOnly? release = AllotmentReleaseDatePicker.SelectedDate is { } releaseDate
            ? DateOnly.FromDateTime(releaseDate)
            : null;

        // Sans date de release, le bloc immobilise les chambres jusqu'au depart, meme invendues.
        // C'est un engagement lourd : on le fait confirmer plutot que de le laisser passer par
        // simple oubli du champ.
        if (release is null && !Confirm(
                $"Poser {roomsHeld} chambre(s) SANS date de release ?\n"
                + "Elles resteront retirées de la vente jusqu'au départ, même si le groupe ne les prend pas.",
                "Bloc sans release"))
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var created = await moduleContext.ApiClient.CreateAllotmentAsync(
                moduleContext.ApiBaseUrl,
                new CreateRoomAllotmentRequest(
                    unitCode,
                    AllotmentReferenceTextBox.Text,
                    customer.Code,
                    roomType.Code,
                    DateOnly.FromDateTime(arrival),
                    DateOnly.FromDateTime(departure),
                    roomsHeld,
                    release,
                    null));

            AllotmentReferenceTextBox.Clear();
            selectedAllotment = created;

            await ReloadAllotmentsAsync();
            moduleContext.SetStatus(
                $"Bloc {created.Reference} posé : {created.RoomsHeld} chambre(s) retirées de la vente.");
        });
    }

    private async void ConfirmAllotmentButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !canWrite || selectedAllotment is not { } allotment)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.ConfirmAllotmentAsync(moduleContext.ApiBaseUrl, allotment.Id);
            await ReloadAllotmentsAsync();
            moduleContext.SetStatus($"Bloc {allotment.Reference} confirmé.");
        });
    }

    private async void ReleaseAllotmentButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !canWrite || selectedAllotment is not { } allotment)
        {
            return;
        }

        var confirmed = Confirm(
            $"Libérer le solde du bloc {allotment.Reference} ?\n"
            + $"{allotment.RemainingAtPeak} chambre(s) retourneront à la vente publique. "
            + "Les chambres déjà prises par le groupe restent réservées.",
            "Libérer le solde");

        if (!confirmed)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.ReleaseAllotmentAsync(moduleContext.ApiBaseUrl, allotment.Id);
            await ReloadAllotmentsAsync();
            moduleContext.SetStatus($"Solde du bloc {allotment.Reference} rendu à la vente.");
        });
    }

    private async void CancelAllotmentButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !canWrite || selectedAllotment is not { } allotment)
        {
            return;
        }

        var confirmed = Confirm(
            $"Annuler définitivement le bloc {allotment.Reference} ({allotment.CustomerName}) ?\n"
            + "Le serveur refusera si des réservations y sont déjà rattachées.",
            "Annuler le bloc");

        if (!confirmed)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.CancelAllotmentAsync(
                moduleContext.ApiBaseUrl,
                allotment.Id,
                new CancelRoomAllotmentRequest("Annulé depuis l'écran groupes"));

            await ReloadAllotmentsAsync();
            moduleContext.SetStatus($"Bloc {allotment.Reference} annulé.");
        });
    }

    private async void SubmitRoomingListButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !canWrite || selectedAllotment is not { } allotment)
        {
            return;
        }

        var entries = ParseRoomingList(RoomingListNamesTextBox.Text);

        if (entries.Count == 0)
        {
            moduleContext.SetStatus("Collez au moins un nom, un par ligne.", isError: true);
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var result = await moduleContext.ApiClient.SubmitRoomingListAsync(
                moduleContext.ApiBaseUrl,
                allotment.Id,
                entries);

            RoomingListNamesTextBox.Clear();
            await ReloadAllotmentsAsync();

            // Un envoi PARTIEL est normal et doit se voir : on affiche ce qui n'est pas passe
            // plutot que d'annoncer un succes global qui masquerait la moitie du groupe.
            RoomingListRejectedTextBlock.Text = result.Rejected.Count == 0
                ? string.Empty
                : "Non logé(s) : " + string.Join(" · ", result.Rejected);

            var logged = entries.Count - result.Rejected.Count;

            moduleContext.SetStatus(
                result.Rejected.Count == 0
                    ? $"{logged} occupant(s) logé(s) sur le bloc {allotment.Reference}."
                    : $"{logged} occupant(s) logé(s), {result.Rejected.Count} refusé(s) — voir le détail.",
                isError: result.Rejected.Count > 0);
        });
    }

    /// <summary>
    /// Un nom par ligne, avec un nombre de personnes optionnel apres un point-virgule. C'est le
    /// format sous lequel une agence envoie reellement une rooming list : une liste collee, pas un
    /// formulaire ligne a ligne.
    /// </summary>
    private static List<RoomingListEntryRequest> ParseRoomingList(string? text)
    {
        var entries = new List<RoomingListEntryRequest>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return entries;
        }

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            var parts = line.Split(';', 2);
            var name = parts[0].Trim();

            if (name.Length == 0)
            {
                continue;
            }

            var guestCount = 1;

            if (parts.Length == 2
                && int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var parsed)
                && parsed > 0)
            {
                guestCount = parsed;
            }

            entries.Add(new RoomingListEntryRequest(name, guestCount, null, null));
        }

        return entries;
    }

    private void UpdateAllotmentButtons()
    {
        var hasSelection = selectedAllotment is not null;
        var isOpen = selectedAllotment?.Status is "Draft" or "Confirmed";

        ApplyPermissionHint(CreateAllotmentButton, canWrite, AllotmentPermissionHint);
        ApplyPermissionHint(ConfirmAllotmentButton, canWrite && hasSelection && selectedAllotment?.Status == "Draft", AllotmentPermissionHint);
        ApplyPermissionHint(ReleaseAllotmentButton, canWrite && hasSelection && isOpen, AllotmentPermissionHint);
        ApplyPermissionHint(CancelAllotmentButton, canWrite && hasSelection && isOpen, AllotmentPermissionHint);
        ApplyPermissionHint(SubmitRoomingListButton, canWrite && hasSelection && isOpen, AllotmentPermissionHint);
    }

    private const string AllotmentPermissionHint =
        "Permissions mice.write ET lodging.write requises : poser un bloc gèle de l'inventaire chambres.";

    /// <summary>Ligne du tableau des blocs.</summary>
    private sealed class AllotmentRow(RoomAllotmentResponse allotment)
    {
        public Guid Id { get; } = allotment.Id;

        public string Reference { get; } = allotment.Reference;

        public string CustomerName { get; } = allotment.CustomerName;

        public string RoomTypeLabel { get; } = allotment.RoomTypeLabel;

        public string PeriodLabel { get; } =
            $"{allotment.ArrivalDate:dd/MM} → {allotment.DepartureDate:dd/MM} ({allotment.Nights} n.)";

        public int RoomsHeld { get; } = allotment.RoomsHeld;

        public int PickedUpPeak { get; } = allotment.PickedUpPeak;

        public int RemainingAtPeak { get; } = allotment.RemainingAtPeak;

        public string ReleaseLabel { get; } = allotment.ReleaseDate is { } release
            ? release.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)
            : "aucune";

        // On distingue "confirme" de "confirme mais qui ne tient plus" : passee la date de release,
        // un bloc existe encore mais ne retire plus rien de la vente. Afficher le seul statut
        // laisserait croire l'inverse.
        public string StatusLabel { get; } = allotment.Status switch
        {
            "Draft" => allotment.IsHolding ? "Option — tient" : "Option — release passé",
            "Confirmed" => allotment.IsHolding ? "Confirmé — tient" : "Confirmé — release passé",
            "Released" => "Solde libéré",
            _ => "Annulé"
        };
    }
}
