using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using RaqmiSystem.Application.Closing;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Domain.Closing;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Cloture journaliere et night audit : verrouillage officiel d'une journee
/// d'exploitation pour une unite hoteliere, et reouverture controlee.
///
/// Vue de module autonome : elle ne connait ni MainWindow ni les autres vues,
/// tout passe par le <see cref="ModuleViewContext"/> recu dans Initialize().
/// </summary>
public partial class ClosingView : UserControl
{
    private const string ClosePermissionHint =
        "Permission closing.close requise : votre profil ne peut pas clôturer une journée d'exploitation.";

    private const string ReopenPermissionHint =
        "Permission closing.reopen requise : votre profil ne peut pas rouvrir une journée clôturée.";

    private ModuleViewContext? context;

    // Info-bulles d'origine des boutons, capturees avant toute substitution par un
    // message de permission : l'affectation doit rester symetrique (voir
    // ApplyPermissionHint).
    private readonly Dictionary<Button, object?> originalToolTips = [];

    // Droits du profil connecte, releves a l'ouverture de session : les actions
    // sont grisees plutot que de laisser decouvrir un 403 apres la saisie. Le
    // serveur reste la seule autorite en matiere de droits.
    private bool canCloseDay = true;
    private bool canReopenDay = true;

    public ClosingView()
    {
        InitializeComponent();
        InitializeDefaults();
    }

    /// <summary>
    /// Memorise le contexte prete par la fenetre et releve les permissions du
    /// profil. Aucun appel reseau ici : le premier chargement est declenche par
    /// LoadAsync().
    /// </summary>
    public void Initialize(ModuleViewContext context)
    {
        this.context = context;

        canCloseDay = context.HasPermission(PermissionCatalog.ClosingClose);
        canReopenDay = context.HasPermission(PermissionCatalog.ClosingReopen);

        UpdateActionStates();
    }

    /// <summary>
    /// (Re)charge les unites hotelieres et les clotures de la periode filtree.
    /// Sort silencieusement tant qu'aucun contexte n'est fourni ou que
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
            await LoadClosingsAsync(current);
        });
    }

    /// <summary>
    /// Vide grilles et formulaires : appele a la deconnexion pour ne jamais
    /// laisser les donnees d'un utilisateur a l'ecran.
    /// </summary>
    public void ResetState()
    {
        ClosingDataGrid.ItemsSource = null;
        FilterUnitComboBox.ItemsSource = null;
        CloseUnitComboBox.ItemsSource = null;
        CloseNotesTextBox.Text = string.Empty;
        ReopenReasonTextBox.Text = string.Empty;
        HideReopenPanel();
        InitializeDefaults();
    }

    // Periode par defaut : le mois courant. Journee a cloturer : la veille,
    // puisqu'on cloture typiquement la journee ecoulee.
    private void InitializeDefaults()
    {
        var today = DateTime.Today;

        FromDatePicker.SelectedDate = new DateTime(today.Year, today.Month, 1);
        ToDatePicker.SelectedDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

        // Le calendrier est borne a la journee du jour et propose la veille :
        // l'operateur raisonne en heure locale, la borne et la valeur par defaut
        // sont donc calculees sur DateTime.Today et non sur l'horloge UTC (sinon,
        // dans les heures qui suivent minuit en UTC+1, la "veille" proposee serait
        // l'avant-veille reelle).
        var lastClosableDate = LastClosableDate.ToDateTime(TimeOnly.MinValue);
        CloseDatePicker.DisplayDateEnd = lastClosableDate;
        CloseDatePicker.SelectedDate = lastClosableDate.AddDays(-1);

        UpdateActionStates();
    }

    private static DateOnly LastClosableDate => DateOnly.FromDateTime(DateTime.Today);

    private async Task LoadHotelUnitsAsync(ModuleViewContext current)
    {
        var units = (await current.ApiClient.GetHotelUnitsAsync(current.ApiBaseUrl, includeInactive: false))
            .Where(unit => unit.IsActive)
            .OrderBy(unit => unit.DisplayOrder)
            .ThenBy(unit => unit.Name)
            .ToArray();

        // Les deux listes conservent leur selection d'un rechargement a l'autre.
        var previousFilterCode = (FilterUnitComboBox.SelectedItem as UnitFilterOption)?.Code;
        var options = new List<UnitFilterOption> { new(null, "Toutes les unités") };
        options.AddRange(units.Select(unit => new UnitFilterOption(unit.Code, $"{unit.Code} — {unit.Name}")));

        FilterUnitComboBox.ItemsSource = options;
        var filterIndex = options.FindIndex(option => option.Code == previousFilterCode);
        FilterUnitComboBox.SelectedIndex = filterIndex >= 0 ? filterIndex : 0;

        var previousCloseCode = (CloseUnitComboBox.SelectedItem as HotelUnitResponse)?.Code;
        CloseUnitComboBox.ItemsSource = units;

        // Selection restauree si possible ; a defaut, l'unite est preselectionnee
        // quand il n'y en a qu'une, sinon le choix reste explicite (-1).
        var closeIndex = Array.FindIndex(units, unit => unit.Code == previousCloseCode);
        CloseUnitComboBox.SelectedIndex = closeIndex >= 0 ? closeIndex : (units.Length == 1 ? 0 : -1);

        UpdateActionStates();
    }

    /// <summary>
    /// Recharge la grille pour la periode filtree et renvoie le nombre de lignes,
    /// ou null quand la periode saisie est incoherente (aucun appel API effectue).
    /// </summary>
    private async Task<int?> LoadClosingsAsync(ModuleViewContext current)
    {
        var from = FromDatePicker.SelectedDate.HasValue
            ? DateOnly.FromDateTime(FromDatePicker.SelectedDate.Value)
            : (DateOnly?)null;

        var to = ToDatePicker.SelectedDate.HasValue
            ? DateOnly.FromDateTime(ToDatePicker.SelectedDate.Value)
            : (DateOnly?)null;

        if (from.HasValue && to.HasValue && from > to)
        {
            current.SetStatus("La date de début ne peut pas être postérieure à la date de fin.", isError: true);
            return null;
        }

        var unitCode = (FilterUnitComboBox.SelectedItem as UnitFilterOption)?.Code;
        var closings = await current.ApiClient.GetDailyClosingsAsync(current.ApiBaseUrl, from, to, unitCode);

        ClosingDataGrid.ItemsSource = closings;

        // La selection precedente ne survit pas au rechargement : le panneau de
        // motif ne doit pas rester ouvert sur une ligne qui n'existe plus.
        HideReopenPanel();
        UpdateActionStates();

        return closings.Count;
    }

    private async void RefreshClosingButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || !current.ApiClient.IsAuthenticated)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await LoadHotelUnitsAsync(current);
            var count = await LoadClosingsAsync(current);

            if (count is int loaded)
            {
                var formattedCount = loaded.ToString(CultureInfo.CurrentCulture);
                current.SetStatus(loaded > 1
                    ? $"{formattedCount} clôtures chargées."
                    : $"{formattedCount} clôture chargée.");
            }
        });
    }

    private async void CloseDayButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        if (CloseUnitComboBox.SelectedItem is not HotelUnitResponse unit)
        {
            current.SetStatus("Sélectionnez une unité hôtelière.", isError: true);
            return;
        }

        if (CloseDatePicker.SelectedDate is not DateTime selectedDate)
        {
            current.SetStatus("Sélectionnez la date métier à clôturer.", isError: true);
            return;
        }

        var businessDate = DateOnly.FromDateTime(selectedDate);
        var formattedDate = businessDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);

        // Acte engageant : la journee est verrouillee pour tous les modules qui
        // ecrivent des donnees datees.
        var confirmed = Confirm(
            $"Clôturer la journée du {formattedDate} pour l'unité {unit.Code} — {unit.Name} ?\n\n" +
            "Les recettes de cette journée ne pourront plus être saisies ni modifiées tant que la journée n'est pas rouverte.",
            "Clôture de la journée");

        if (!confirmed)
        {
            return;
        }

        var notes = string.IsNullOrWhiteSpace(CloseNotesTextBox.Text) ? null : CloseNotesTextBox.Text.Trim();

        await current.RunAsync(async () =>
        {
            await current.ApiClient.CloseBusinessDayAsync(
                current.ApiBaseUrl,
                new CloseBusinessDayRequest(businessDate, unit.Code, notes));

            CloseNotesTextBox.Text = string.Empty;

            await LoadClosingsAsync(current);
            current.SetStatus($"Journée du {formattedDate} clôturée pour l'unité {unit.Code}.");
        });
    }

    private void ReopenButton_Click(object sender, RoutedEventArgs e)
    {
        if (ClosingDataGrid.SelectedItem is not DailyClosingResponse selected || selected.Status != ClosingStatus.Closed)
        {
            return;
        }

        ReopenTargetTextBlock.Text =
            $"Réouverture de la journée du {selected.BusinessDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)} " +
            $"pour l'unité {selected.HotelUnitCode}.";

        ReopenPanelBorder.Visibility = Visibility.Visible;
        ReopenReasonTextBox.Focus();
        UpdateActionStates();
    }

    private void CancelReopenButton_Click(object sender, RoutedEventArgs e)
    {
        HideReopenPanel();
    }

    private async void ConfirmReopenButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        if (ClosingDataGrid.SelectedItem is not DailyClosingResponse selected || selected.Status != ClosingStatus.Closed)
        {
            current.SetStatus("Sélectionnez une journée clôturée à rouvrir.", isError: true);
            return;
        }

        var reason = ReopenReasonTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(reason))
        {
            current.SetStatus("Le motif de réouverture est obligatoire.", isError: true);
            return;
        }

        var formattedDate = selected.BusinessDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);

        // Acte de controle : la reouverture est tracee et doit etre confirmee.
        var confirmed = Confirm(
            $"Rouvrir la journée du {formattedDate} pour l'unité {selected.HotelUnitCode} ?\n\n" +
            $"Motif : {reason}\n\nCette réouverture est enregistrée dans le journal d'audit.",
            "Réouverture de la journée");

        if (!confirmed)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.ReopenDailyClosingAsync(
                current.ApiBaseUrl,
                selected.Id,
                new ReopenDailyClosingRequest(reason));

            ReopenReasonTextBox.Text = string.Empty;

            await LoadClosingsAsync(current);
            current.SetStatus($"Journée du {formattedDate} rouverte pour l'unité {selected.HotelUnitCode}.");
        });
    }

    private void ClosingDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Changer de ligne referme la saisie du motif : elle vise toujours la
        // ligne selectionnee au moment de son ouverture.
        HideReopenPanel();
    }

    private void CloseUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateActionStates();
    }

    private void CloseDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateActionStates();
    }

    private void ReopenReasonTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateActionStates();
    }

    private void HideReopenPanel()
    {
        ReopenPanelBorder.Visibility = Visibility.Collapsed;
        ReopenReasonTextBox.Text = string.Empty;
        UpdateActionStates();
    }

    // Une action indisponible est desactivee plutot que de laisser l'utilisateur
    // declencher une erreur API previsible. L'etat metier est croise avec les
    // droits closing.close / closing.reopen du profil connecte.
    private void UpdateActionStates()
    {
        var canClose = canCloseDay
            && CloseUnitComboBox.SelectedItem is HotelUnitResponse
            && CloseDatePicker.SelectedDate is DateTime selectedDate
            && DateOnly.FromDateTime(selectedDate) <= LastClosableDate;

        CloseDayButton.IsEnabled = canClose;

        var canReopen = canReopenDay
            && ClosingDataGrid.SelectedItem is DailyClosingResponse selected
            && selected.Status == ClosingStatus.Closed;

        ReopenButton.IsEnabled = canReopen;
        ConfirmReopenButton.IsEnabled = canReopen && !string.IsNullOrWhiteSpace(ReopenReasonTextBox.Text);

        ApplyPermissionHint(CloseDayButton, canCloseDay, ClosePermissionHint);
        ApplyPermissionHint(ReopenButton, canReopenDay, ReopenPermissionHint);
        ApplyPermissionHint(ConfirmReopenButton, canReopenDay, ReopenPermissionHint);
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

    // Gabarit de confirmation des actes engageants : fenetre proprietaire, icone
    // d'avertissement, defaut sur Non - la touche Entree ne suffit jamais a
    // engager l'action.
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
}

/// <summary>
/// Convertit un horodatage UTC renvoye par l'API en heure du poste : l'operateur
/// lit "clôturée le 12/03/2026 01:30" a l'heure a laquelle il a effectivement
/// clôturé, et non l'heure UTC correspondante.
/// </summary>
public sealed class UtcToLocalTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            DateTimeOffset moment => moment.ToLocalTime().DateTime,
            DateTime moment => moment.ToLocalTime(),
            _ => null
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
