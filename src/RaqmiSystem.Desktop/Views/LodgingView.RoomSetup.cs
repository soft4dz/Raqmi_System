using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Volet PARAMETRAGE de l'ecran Hebergement : creation et modification des types de chambre, des
/// chambres, et de leur couchage.
///
/// Ce volet comble un trou reel : l'API exposait ces routes depuis le debut, le client lourd ne les
/// appelait jamais, et l'onglet se contentait d'afficher un referentiel en lecture seule tout en
/// renvoyant l'utilisateur vers "l'administration" - un endroit qui n'existait pas.
///
/// REGLE DU COUCHAGE, tenue partout ici : le TYPE porte la composition standard ; une chambre ne la
/// surcharge que lorsqu'elle en differe. Une composition doit coucher EXACTEMENT la capacite du
/// type, parce que la recherche de disponibilite raisonne sur cette capacite : un ecart la rendrait
/// fausse. Le serveur refuse l'ecart ; cet ecran l'annonce avant l'envoi.
///
/// Fichier partiel, comme RaqmiApiClient : la vue Hebergement est deja longue, et ce chantier n'a
/// pas a se disputer le meme fichier que la reception ou le planning.
/// </summary>
public partial class LodgingView
{
    private readonly ObservableCollection<BedRow> typeBedRows = [];

    private readonly ObservableCollection<BedRow> roomBedRows = [];

    private IReadOnlyList<RoomTypeResponse> knownRoomTypes = [];

    private RoomTypeResponse? selectedRoomType;

    private RoomResponse? selectedRoom;

    private bool roomSetupInitialized;

    /// <summary>
    /// Prepare les grilles de couchage. Appele au premier chargement du referentiel plutot que dans
    /// le constructeur : les colonnes ComboBox n'existent qu'une fois le XAML charge.
    /// </summary>
    private void EnsureRoomSetupInitialized()
    {
        if (roomSetupInitialized)
        {
            return;
        }

        var options = Enum.GetValues<BedType>()
            .Select(bedType => new BedTypeOption(bedType.ToString(), DescribeBedType(bedType)))
            .ToList();

        TypeBedKindColumn.ItemsSource = options;
        TypeBedKindColumn.DisplayMemberPath = nameof(BedTypeOption.Label);
        TypeBedKindColumn.SelectedValuePath = nameof(BedTypeOption.Value);

        RoomBedKindColumn.ItemsSource = options;
        RoomBedKindColumn.DisplayMemberPath = nameof(BedTypeOption.Label);
        RoomBedKindColumn.SelectedValuePath = nameof(BedTypeOption.Value);

        TypeBedsDataGrid.ItemsSource = typeBedRows;
        RoomBedsDataGrid.ItemsSource = roomBedRows;

        roomSetupInitialized = true;
    }

    /// <summary>Recale le volet apres un rechargement du referentiel.</summary>
    private void OnRoomsReferentialLoaded(IReadOnlyCollection<RoomTypeResponse> roomTypes)
    {
        EnsureRoomSetupInitialized();

        knownRoomTypes = roomTypes.ToList();

        // Seuls les types ACTIFS sont proposes pour une chambre : le serveur refuse d'y rattacher
        // une chambre a un type desactive, autant ne pas l'offrir.
        RoomTypeComboBox.ItemsSource = knownRoomTypes.Where(roomType => roomType.IsActive).ToList();

        // La selection precedente est retrouvee par identifiant, pour qu'un enregistrement ne
        // referme pas le formulaire que l'utilisateur avait sous les yeux.
        if (selectedRoomType is { } previousType)
        {
            var match = knownRoomTypes.FirstOrDefault(roomType => roomType.Id == previousType.Id);

            if (match is not null)
            {
                RoomTypesDataGrid.SelectedItem = match;
            }
        }

        UpdateRoomSetupButtons();
        RefreshBedSummaries();
    }

    // ============================== Selections ==============================

    private void RoomTypesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        EnsureRoomSetupInitialized();

        if (RoomTypesDataGrid.SelectedItem is not RoomTypeResponse roomType)
        {
            return;
        }

        selectedRoomType = roomType;

        TypeCodeTextBox.Text = roomType.Code;
        TypeLabelTextBox.Text = roomType.Label;
        TypeCapacityTextBox.Text = roomType.Capacity.ToString(CultureInfo.CurrentCulture);
        TypeDescriptionTextBox.Text = roomType.Description ?? string.Empty;
        TypeExtraBedsTextBox.Text = roomType.MaxExtraBeds.ToString(CultureInfo.CurrentCulture);
        TypeCotsTextBox.Text = roomType.MaxCots.ToString(CultureInfo.CurrentCulture);

        typeBedRows.Clear();

        foreach (var bed in roomType.Beds)
        {
            typeBedRows.Add(new BedRow { BedType = bed.BedType, Quantity = bed.Quantity.ToString(CultureInfo.CurrentCulture) });
        }

        UpdateRoomSetupButtons();
        RefreshBedSummaries();
    }

    private void RoomsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        EnsureRoomSetupInitialized();

        if (RoomsDataGrid.SelectedItem is not RoomResponse room)
        {
            return;
        }

        selectedRoom = room;

        RoomNumberTextBox.Text = room.Number;
        RoomFloorTextBox.Text = room.Floor ?? string.Empty;
        RoomNotesTextBox.Text = room.Notes ?? string.Empty;

        RoomTypeComboBox.SelectedItem = knownRoomTypes.FirstOrDefault(roomType =>
            roomType.HotelUnitCode == room.HotelUnitCode && roomType.Code == room.RoomTypeCode);

        roomBedRows.Clear();

        // On ne recopie le couchage dans le formulaire QUE s'il est propre a la chambre. Sinon la
        // grille reste vide, ce qui veut dire "cette chambre suit son type" - et enregistrer sans y
        // toucher ne creerait donc pas une exception involontaire.
        if (room.OverridesBeds)
        {
            foreach (var bed in room.Beds)
            {
                roomBedRows.Add(new BedRow { BedType = bed.BedType, Quantity = bed.Quantity.ToString(CultureInfo.CurrentCulture) });
            }
        }

        RoomExtraBedsTextBox.Text = string.Empty;
        RoomCotsTextBox.Text = string.Empty;

        UpdateRoomSetupButtons();
        RefreshBedSummaries();
    }

    // ============================== Lignes de couchage ==============================

    private void AddTypeBedButton_Click(object sender, RoutedEventArgs e)
    {
        typeBedRows.Add(new BedRow { BedType = nameof(BedType.Single), Quantity = "1" });
        RefreshBedSummaries();
    }

    private void RemoveTypeBedButton_Click(object sender, RoutedEventArgs e)
    {
        if (TypeBedsDataGrid.SelectedItem is BedRow row)
        {
            typeBedRows.Remove(row);
            RefreshBedSummaries();
        }
    }

    private void AddRoomBedButton_Click(object sender, RoutedEventArgs e)
    {
        roomBedRows.Add(new BedRow { BedType = nameof(BedType.Single), Quantity = "1" });
        RefreshBedSummaries();
    }

    private void RemoveRoomBedButton_Click(object sender, RoutedEventArgs e)
    {
        if (RoomBedsDataGrid.SelectedItem is BedRow row)
        {
            roomBedRows.Remove(row);
            RefreshBedSummaries();
        }
    }

    /// <summary>
    /// Annonce l'ecart AVANT l'envoi. Le serveur refuse de toute facon une composition qui ne
    /// couche pas la capacite, mais decouvrir la regle sur un message d'erreur apres coup est une
    /// mauvaise facon de l'apprendre.
    /// </summary>
    private void RefreshBedSummaries()
    {
        TypeBedsDataGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: false);
        RoomBedsDataGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: false);

        TypeBedsSummaryTextBlock.Text = DescribeComposition(
            typeBedRows,
            TryReadInt(TypeCapacityTextBox.Text, out var typeCapacity) ? typeCapacity : null,
            "Aucun couchage déclaré : la capacité reste la seule information.");

        var roomCapacity = RoomTypeComboBox.SelectedItem is RoomTypeResponse selected
            ? selected.Capacity
            : (int?)null;

        RoomBedsSummaryTextBlock.Text = roomBedRows.Count == 0
            ? "Cette chambre suit le couchage de son type."
            : DescribeComposition(roomBedRows, roomCapacity, string.Empty);
    }

    private static string DescribeComposition(
        IReadOnlyCollection<BedRow> rows,
        int? expectedCapacity,
        string emptyMessage)
    {
        if (rows.Count == 0)
        {
            return emptyMessage;
        }

        var sleeps = 0;

        foreach (var row in rows)
        {
            if (!Enum.TryParse<BedType>(row.BedType, ignoreCase: true, out var bedType)
                || !TryReadInt(row.Quantity, out var quantity))
            {
                return "Composition incomplète : renseignez une nature et un nombre pour chaque ligne.";
            }

            sleeps += bedType.Sleeps() * quantity;
        }

        if (expectedCapacity is { } capacity && sleeps != capacity)
        {
            return $"Couche {sleeps} personne(s) alors que la capacité du type est {capacity} : "
                + "corrigez l'un ou l'autre avant d'enregistrer.";
        }

        return $"Couche {sleeps} personne(s).";
    }

    // ============================== Types de chambre ==============================

    private async void CreateRoomTypeButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || !canWrite)
        {
            return;
        }

        if (!TryBuildTypePayload(current, out var label, out var capacity, out var beds, out var extraBeds, out var cots))
        {
            return;
        }

        var unitCode = ResolveSetupUnitCode();

        if (unitCode is null)
        {
            current.SetStatus("Sélectionnez une unité hôtelière dans le référentiel avant de créer.", isError: true);
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.CreateRoomTypeAsync(
                current.ApiBaseUrl,
                new CreateRoomTypeRequest(
                    unitCode,
                    TypeCodeTextBox.Text,
                    label,
                    capacity,
                    NullIfBlank(TypeDescriptionTextBox.Text),
                    beds,
                    extraBeds,
                    cots));

            await LoadRoomsReferentialAsync(current);
            current.SetStatus($"Type de chambre {TypeCodeTextBox.Text} créé.");
        });
    }

    private async void UpdateRoomTypeButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || !canWrite || selectedRoomType is not { } roomType)
        {
            return;
        }

        if (!TryBuildTypePayload(current, out var label, out var capacity, out var beds, out var extraBeds, out var cots))
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.UpdateRoomTypeAsync(
                current.ApiBaseUrl,
                roomType.Id,
                new UpdateRoomTypeRequest(
                    label,
                    capacity,
                    NullIfBlank(TypeDescriptionTextBox.Text),
                    beds,
                    extraBeds,
                    cots));

            await LoadRoomsReferentialAsync(current);
            current.SetStatus($"Type de chambre {roomType.Code} enregistré.");
        });
    }

    private async void ToggleRoomTypeButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || !canWrite || selectedRoomType is not { } roomType)
        {
            return;
        }

        var target = !roomType.IsActive;

        if (!target && !ConfirmSetup(
                $"Désactiver le type {roomType.Code} ({roomType.Label}) ?\n"
                + "Il ne pourra plus recevoir de nouvelle chambre ni de nouvelle réservation. "
                + "Les chambres et réservations existantes sont conservées.",
                "Désactiver le type"))
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.SetRoomTypeActiveAsync(current.ApiBaseUrl, roomType.Id, target);
            await LoadRoomsReferentialAsync(current);
            current.SetStatus(target ? $"Type {roomType.Code} activé." : $"Type {roomType.Code} désactivé.");
        });
    }

    // ================================= Chambres =================================

    private async void CreateRoomButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || !canWrite)
        {
            return;
        }

        if (RoomTypeComboBox.SelectedItem is not RoomTypeResponse roomType)
        {
            current.SetStatus("Sélectionnez le type de la chambre.", isError: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(RoomNumberTextBox.Text))
        {
            current.SetStatus("Le numéro de chambre est requis.", isError: true);
            return;
        }

        if (!TryBuildRoomBeds(current, roomType, out var beds, out var extraBeds, out var cots))
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.CreateRoomAsync(
                current.ApiBaseUrl,
                new CreateRoomRequest(
                    roomType.HotelUnitCode,
                    RoomNumberTextBox.Text,
                    roomType.Code,
                    NullIfBlank(RoomFloorTextBox.Text),
                    NullIfBlank(RoomNotesTextBox.Text),
                    beds,
                    extraBeds,
                    cots));

            await LoadRoomsReferentialAsync(current);
            current.SetStatus($"Chambre {RoomNumberTextBox.Text} créée.");
        });
    }

    private async void UpdateRoomButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || !canWrite || selectedRoom is not { } room)
        {
            return;
        }

        if (RoomTypeComboBox.SelectedItem is not RoomTypeResponse roomType)
        {
            current.SetStatus("Sélectionnez le type de la chambre.", isError: true);
            return;
        }

        if (!TryBuildRoomBeds(current, roomType, out var beds, out var extraBeds, out var cots))
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.UpdateRoomAsync(
                current.ApiBaseUrl,
                room.Id,
                new UpdateRoomRequest(
                    roomType.Code,
                    NullIfBlank(RoomFloorTextBox.Text),
                    NullIfBlank(RoomNotesTextBox.Text),
                    beds,
                    extraBeds,
                    cots));

            await LoadRoomsReferentialAsync(current);
            current.SetStatus($"Chambre {room.Number} enregistrée.");
        });
    }

    private async void ToggleRoomButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || !canWrite || selectedRoom is not { } room)
        {
            return;
        }

        var target = !room.IsActive;

        if (!target && !ConfirmSetup(
                $"Désactiver la chambre {room.Number} ?\n"
                + "Elle sortira de la vente. Les réservations déjà prises sur cette chambre sont conservées.",
                "Désactiver la chambre"))
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.SetRoomActiveAsync(current.ApiBaseUrl, room.Id, target);
            await LoadRoomsReferentialAsync(current);
            current.SetStatus(target ? $"Chambre {room.Number} activée." : $"Chambre {room.Number} désactivée.");
        });
    }

    // ================================= Outillage =================================

    private bool TryBuildTypePayload(
        ModuleViewContext current,
        out string label,
        out int capacity,
        out IReadOnlyCollection<BedCompositionLine>? beds,
        out int extraBeds,
        out int cots)
    {
        label = TypeLabelTextBox.Text?.Trim() ?? string.Empty;
        capacity = 0;
        beds = null;
        extraBeds = 0;
        cots = 0;

        if (string.IsNullOrWhiteSpace(TypeCodeTextBox.Text) || string.IsNullOrWhiteSpace(label))
        {
            current.SetStatus("Le code et le libellé du type sont requis.", isError: true);
            return false;
        }

        if (!TryReadInt(TypeCapacityTextBox.Text, out capacity) || capacity <= 0)
        {
            current.SetStatus("La capacité doit être un nombre de personnes strictement positif.", isError: true);
            return false;
        }

        if (!TryReadOptionalCount(TypeExtraBedsTextBox.Text, out var parsedExtra)
            || !TryReadOptionalCount(TypeCotsTextBox.Text, out var parsedCots))
        {
            current.SetStatus("Lits d'appoint et berceaux doivent être des nombres positifs ou vides.", isError: true);
            return false;
        }

        extraBeds = parsedExtra ?? 0;
        cots = parsedCots ?? 0;

        if (!TryBuildComposition(current, typeBedRows, capacity, out beds))
        {
            return false;
        }

        return true;
    }

    private bool TryBuildRoomBeds(
        ModuleViewContext current,
        RoomTypeResponse roomType,
        out IReadOnlyCollection<BedCompositionLine>? beds,
        out int? extraBeds,
        out int? cots)
    {
        beds = null;
        extraBeds = null;
        cots = null;

        if (!TryReadOptionalCount(RoomExtraBedsTextBox.Text, out extraBeds)
            || !TryReadOptionalCount(RoomCotsTextBox.Text, out cots))
        {
            current.SetStatus("Lits d'appoint et berceaux doivent être des nombres positifs ou vides.", isError: true);
            return false;
        }

        // Grille vide : on envoie une liste VIDE et non null, ce qui efface explicitement une
        // eventuelle exception et fait retomber la chambre sur son type.
        return TryBuildComposition(current, roomBedRows, roomType.Capacity, out beds);
    }

    private bool TryBuildComposition(
        ModuleViewContext current,
        IReadOnlyCollection<BedRow> rows,
        int expectedCapacity,
        out IReadOnlyCollection<BedCompositionLine>? composition)
    {
        composition = null;

        var lines = new List<BedCompositionLine>(rows.Count);
        var sleeps = 0;

        foreach (var row in rows)
        {
            if (!Enum.TryParse<BedType>(row.BedType, ignoreCase: true, out var bedType))
            {
                current.SetStatus("Chaque ligne de couchage doit porter une nature de lit.", isError: true);
                return false;
            }

            if (!TryReadInt(row.Quantity, out var quantity) || quantity <= 0)
            {
                current.SetStatus("Le nombre de lits doit être un entier strictement positif.", isError: true);
                return false;
            }

            if (lines.Any(line => line.BedType == bedType.ToString()))
            {
                current.SetStatus(
                    $"La nature « {DescribeBedType(bedType)} » figure deux fois : regroupez-la sur une seule ligne.",
                    isError: true);
                return false;
            }

            lines.Add(new BedCompositionLine(bedType.ToString(), quantity));
            sleeps += bedType.Sleeps() * quantity;
        }

        if (lines.Count > 0 && sleeps != expectedCapacity)
        {
            current.SetStatus(
                $"Le couchage déclaré couche {sleeps} personne(s) pour une capacité de {expectedCapacity}. "
                + "La recherche de disponibilité se fie à la capacité : les deux doivent concorder.",
                isError: true);
            return false;
        }

        composition = lines;
        return true;
    }

    /// <summary>
    /// Unite retenue pour une creation : celle du type selectionne, sinon celle de la chambre
    /// selectionnee. L'ecran ne devine jamais une unite qui ne serait pas sous les yeux.
    /// </summary>
    private string? ResolveSetupUnitCode()
    {
        if (selectedRoomType is { } roomType)
        {
            return roomType.HotelUnitCode;
        }

        return selectedRoom?.HotelUnitCode ?? knownRoomTypes.FirstOrDefault()?.HotelUnitCode;
    }

    private void UpdateRoomSetupButtons()
    {
        ApplySetupPermission(CreateRoomTypeButton, canWrite);
        ApplySetupPermission(UpdateRoomTypeButton, canWrite && selectedRoomType is not null);
        ApplySetupPermission(ToggleRoomTypeButton, canWrite && selectedRoomType is not null);
        ApplySetupPermission(AddTypeBedButton, canWrite);
        ApplySetupPermission(RemoveTypeBedButton, canWrite);

        ApplySetupPermission(CreateRoomButton, canWrite);
        ApplySetupPermission(UpdateRoomButton, canWrite && selectedRoom is not null);
        ApplySetupPermission(ToggleRoomButton, canWrite && selectedRoom is not null);
        ApplySetupPermission(AddRoomBedButton, canWrite);
        ApplySetupPermission(RemoveRoomBedButton, canWrite);
    }

    private readonly Dictionary<Button, object?> setupToolTips = [];

    /// <summary>Affectation symetrique : soit l'info-bulle d'origine, soit le message de permission.</summary>
    private void ApplySetupPermission(Button button, bool isEnabled)
    {
        if (!setupToolTips.ContainsKey(button))
        {
            setupToolTips[button] = button.ToolTip;
        }

        button.IsEnabled = isEnabled;

        button.ToolTip = isEnabled
            ? setupToolTips[button]
            : canWrite
                ? "Sélectionnez d'abord une ligne dans le référentiel ci-dessus."
                : "Permission lodging.write requise pour modifier le référentiel des chambres.";
    }

    private bool ConfirmSetup(string message, string caption)
    {
        var owner = Window.GetWindow(this);

        var result = owner is null
            ? MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            : MessageBox.Show(owner, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool TryReadInt(string? value, out int result)
    {
        return int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out result);
    }

    private static bool TryReadOptionalCount(string? value, out int? result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = null;
            return true;
        }

        if (TryReadInt(value, out var parsed) && parsed >= 0)
        {
            result = parsed;
            return true;
        }

        result = null;
        return false;
    }

    private static string DescribeBedType(BedType bedType) => bedType switch
    {
        BedType.Single => "Lit simple (1 pers.)",
        BedType.Double => "Lit double (2 pers.)",
        BedType.Queen => "Queen (2 pers.)",
        BedType.King => "King (2 pers.)",
        BedType.SofaBed => "Canapé-lit (2 pers.)",
        BedType.BunkBed => "Lits superposés (2 pers.)",
        _ => bedType.ToString()
    };

    private sealed record BedTypeOption(string Value, string Label);

    /// <summary>Ligne editable de composition. Le nombre reste en texte tant qu'il est saisi.</summary>
    private sealed class BedRow
    {
        public string BedType { get; set; } = nameof(Domain.Lodging.BedType.Single);

        public string Quantity { get; set; } = "1";
    }
}
