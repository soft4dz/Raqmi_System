using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Application.Tariffs;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Tarifs &amp; conventions : plans tarifaires par unite hoteliere, periodes de
/// tarif par type de chambre, conventions clients et testeur de resolution d'un
/// tarif (lecture seule).
///
/// Vue de module autonome : elle ne connait ni MainWindow ni les autres vues,
/// tout passe par le <see cref="ModuleViewContext"/> recu dans Initialize().
/// </summary>
public partial class TariffsView : UserControl
{
    private const string WritePermissionHint =
        "Permission requise : tariffs.write. Votre profil ne peut que consulter les tarifs.";

    // Legende neutre de la carte des periodes, posee tant qu'aucun plan n'est
    // selectionne - et remise a la deconnexion pour ne pas laisser le code du plan
    // d'un utilisateur a l'ecran du suivant.
    private const string DefaultPeriodsCaption =
        "Sélectionnez un plan tarifaire pour afficher et gérer ses périodes.";

    private ModuleViewContext? context;

    // Info-bulles d'origine des boutons, capturees avant toute substitution par un
    // message de permission : l'affectation doit rester symetrique (voir
    // ApplyPermissionHint), les vues survivant a la deconnexion.
    private readonly Dictionary<Button, object?> originalToolTips = [];

    // Droit d'ecriture du profil connecte, releve a l'ouverture de session. Le
    // serveur reste la seule autorite : ceci n'est qu'un confort d'interface.
    private bool canWrite = true;

    // Code du plan en cours de modification, ou null quand le formulaire cree un
    // nouveau plan (meme motif editingUnitCode que l'onglet Unites).
    private string? editingPlanCode;

    // Identifiant de la convention en cours de modification, ou null en creation.
    private Guid? editingConventionId;

    public TariffsView()
    {
        InitializeComponent();
        InitializeDefaults();
    }

    /// <summary>
    /// Memorise le contexte prete par la fenetre et releve la permission
    /// d'ecriture du profil. Aucun appel reseau ici : le premier chargement est
    /// declenche par LoadAsync().
    /// </summary>
    public void Initialize(ModuleViewContext moduleViewContext)
    {
        context = moduleViewContext;
        canWrite = moduleViewContext.HasPermission(PermissionCatalog.TariffsWrite);
        UpdateActionStates();
    }

    /// <summary>
    /// (Re)charge les unites, les plans tarifaires et les conventions. Sort
    /// silencieusement tant qu'aucun contexte n'est fourni ou que l'utilisateur
    /// n'est pas connecte.
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
            await LoadPlansAsync(current);
            await LoadConventionsAsync(current);
        });
    }

    /// <summary>
    /// Vide grilles et formulaires : appele a la deconnexion pour ne jamais
    /// laisser les donnees d'un utilisateur a l'ecran.
    /// </summary>
    public void ResetState()
    {
        PlansDataGrid.ItemsSource = null;
        PeriodsDataGrid.ItemsSource = null;
        ConventionsDataGrid.ItemsSource = null;
        FilterUnitComboBox.ItemsSource = null;
        PlanUnitComboBox.ItemsSource = null;
        ConventionPlanComboBox.ItemsSource = null;
        ResolveUnitComboBox.ItemsSource = null;
        IncludeInactiveTariffsCheckBox.IsChecked = false;
        ResolveResultTextBlock.Text = string.Empty;
        ResolveRoomTypeTextBox.Text = string.Empty;
        ResolveCustomerTextBox.Text = string.Empty;
        PeriodsPlanCaptionTextBlock.Text = DefaultPeriodsCaption;
        ResetPlanForm();
        ResetPeriodForm();
        ResetConventionForm();
        InitializeDefaults();
    }

    private void InitializeDefaults()
    {
        var today = DateTime.Today;

        PeriodFromDatePicker.SelectedDate = today;
        PeriodToDatePicker.SelectedDate = today.AddMonths(1);
        ConventionFromDatePicker.SelectedDate = today;
        ConventionToDatePicker.SelectedDate = today.AddYears(1);
        ResolveNightDatePicker.SelectedDate = today;

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

        // Le filtre conserve sa selection d'un rechargement a l'autre.
        var previousFilterCode = (FilterUnitComboBox.SelectedItem as UnitFilterOption)?.Code;
        var options = new List<UnitFilterOption> { new(null, "Toutes les unités") };
        options.AddRange(units.Select(unit => new UnitFilterOption(unit.Code, $"{unit.Code} — {unit.Name}")));

        FilterUnitComboBox.ItemsSource = options;
        var filterIndex = options.FindIndex(option => option.Code == previousFilterCode);
        FilterUnitComboBox.SelectedIndex = filterIndex >= 0 ? filterIndex : 0;

        RebindUnitComboBox(PlanUnitComboBox, units);
        RebindUnitComboBox(ResolveUnitComboBox, units);
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

    private async Task LoadPlansAsync(ModuleViewContext current)
    {
        var unitCode = (FilterUnitComboBox.SelectedItem as UnitFilterOption)?.Code;
        var plans = await current.ApiClient.GetRatePlansAsync(
            current.ApiBaseUrl,
            unitCode,
            IncludeInactiveTariffsCheckBox.IsChecked == true);

        PlansDataGrid.ItemsSource = plans
            .OrderBy(plan => plan.HotelUnitCode)
            .ThenBy(plan => plan.Code)
            .ToArray();

        // La liste des plans du formulaire de convention suit celle de la grille,
        // en conservant la selection quand le plan existe toujours.
        var previousPlanCode = (ConventionPlanComboBox.SelectedItem as RatePlanResponse)?.Code;
        var activePlans = plans
            .Where(plan => plan.IsActive)
            .OrderBy(plan => plan.Code)
            .ToArray();

        ConventionPlanComboBox.ItemsSource = activePlans;
        var planIndex = Array.FindIndex(activePlans, plan => plan.Code == previousPlanCode);
        ConventionPlanComboBox.SelectedIndex = planIndex >= 0 ? planIndex : (activePlans.Length == 1 ? 0 : -1);

        // La selection precedente ne survit pas au rebind : le formulaire et les
        // periodes repartent d'un etat neutre.
        ResetPlanForm();
        PeriodsDataGrid.ItemsSource = null;
        PeriodsPlanCaptionTextBlock.Text = DefaultPeriodsCaption;
        UpdateActionStates();
    }

    private async Task LoadPeriodsAsync(ModuleViewContext current, string planCode)
    {
        var periods = await current.ApiClient.GetRatePeriodsAsync(current.ApiBaseUrl, planCode, roomTypeCode: null);

        PeriodsDataGrid.ItemsSource = periods
            .OrderBy(period => period.RoomTypeCode)
            .ThenBy(period => period.FromDate)
            .ToArray();

        PeriodsPlanCaptionTextBlock.Text = $"Périodes du plan {planCode}.";
    }

    private async Task LoadConventionsAsync(ModuleViewContext current)
    {
        var conventions = await current.ApiClient.GetCustomerConventionsAsync(
            current.ApiBaseUrl,
            customerCode: null,
            IncludeInactiveTariffsCheckBox.IsChecked == true);

        ConventionsDataGrid.ItemsSource = conventions
            .OrderBy(convention => convention.CustomerCode)
            .ThenBy(convention => convention.FromDate)
            .ToArray();

        ResetConventionForm();
        UpdateActionStates();
    }

    private async void RefreshTariffsButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null || !current.ApiClient.IsAuthenticated)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await LoadHotelUnitsAsync(current);
            await LoadPlansAsync(current);
            await LoadConventionsAsync(current);
            current.SetStatus("Tarifs et conventions actualisés.");
        });
    }

    // ============================== Plans tarifaires =============================

    private async void PlansDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PlansDataGrid.SelectedItem is not RatePlanResponse selected)
        {
            UpdateActionStates();
            return;
        }

        editingPlanCode = selected.Code;
        PlanFormTitleTextBlock.Text = $"Modifier {selected.Code}";
        PlanCodeTextBox.Text = selected.Code;
        PlanCodeTextBox.IsEnabled = false;
        PlanLabelTextBox.Text = selected.Label;
        PlanIsDefaultCheckBox.IsChecked = selected.IsDefault;
        PlanIsDefaultCheckBox.IsEnabled = false;

        if (PlanUnitComboBox.ItemsSource is HotelUnitResponse[] units)
        {
            PlanUnitComboBox.SelectedIndex = Array.FindIndex(units, unit => unit.Code == selected.HotelUnitCode);
        }

        // L'unite d'un plan est fixee a la creation : en modification, seule
        // l'edition du libelle est proposee.
        PlanUnitComboBox.IsEnabled = false;
        SavePlanButton.Content = "Modifier";
        UpdateActionStates();

        var current = context;

        if (current is null || !current.ApiClient.IsAuthenticated)
        {
            return;
        }

        await current.RunAsync(() => LoadPeriodsAsync(current, selected.Code));
    }

    private void NewPlanButton_Click(object sender, RoutedEventArgs e)
    {
        ResetPlanForm();
        PeriodsDataGrid.ItemsSource = null;
        PeriodsPlanCaptionTextBlock.Text = DefaultPeriodsCaption;
        UpdateActionStates();
    }

    private void ResetPlanForm()
    {
        editingPlanCode = null;
        PlanFormTitleTextBlock.Text = "Nouveau plan tarifaire";
        PlanCodeTextBox.Text = string.Empty;
        PlanCodeTextBox.IsEnabled = true;
        PlanLabelTextBox.Text = string.Empty;
        PlanIsDefaultCheckBox.IsChecked = false;
        PlanIsDefaultCheckBox.IsEnabled = true;
        PlanUnitComboBox.IsEnabled = true;
        SavePlanButton.Content = "Créer";
        PlansDataGrid.SelectedItem = null;
    }

    private async void SavePlanButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            var label = PlanLabelTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(label))
            {
                current.SetStatus("Le libellé du plan est requis.", isError: true);
                return;
            }

            if (editingPlanCode is null)
            {
                var code = PlanCodeTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(code))
                {
                    current.SetStatus("Le code du plan est requis.", isError: true);
                    return;
                }

                if (PlanUnitComboBox.SelectedItem is not HotelUnitResponse unit)
                {
                    current.SetStatus("Sélectionnez l'unité hôtelière du plan.", isError: true);
                    return;
                }

                await current.ApiClient.CreateRatePlanAsync(
                    current.ApiBaseUrl,
                    new CreateRatePlanRequest(code, label, unit.Code, PlanIsDefaultCheckBox.IsChecked == true));
                current.SetStatus($"Plan tarifaire {code} créé.");
            }
            else
            {
                await current.ApiClient.UpdateRatePlanAsync(
                    current.ApiBaseUrl,
                    editingPlanCode,
                    new UpdateRatePlanRequest(label));
                current.SetStatus($"Plan tarifaire {editingPlanCode} mis à jour.");
            }

            await LoadPlansAsync(current);
        });
    }

    private async void SetDefaultPlanButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        if (PlansDataGrid.SelectedItem is not RatePlanResponse selected)
        {
            current.SetStatus("Sélectionnez un plan tarifaire.", isError: true);
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.SetRatePlanDefaultAsync(current.ApiBaseUrl, selected.Code);
            await LoadPlansAsync(current);
            current.SetStatus($"Le plan {selected.Code} est désormais le plan par défaut de son unité.");
        });
    }

    private async void ActivatePlanButton_Click(object sender, RoutedEventArgs e)
    {
        await SetPlanActiveAsync(isActive: true);
    }

    private async void DeactivatePlanButton_Click(object sender, RoutedEventArgs e)
    {
        await SetPlanActiveAsync(isActive: false);
    }

    private async Task SetPlanActiveAsync(bool isActive)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        if (PlansDataGrid.SelectedItem is not RatePlanResponse selected)
        {
            current.SetStatus("Sélectionnez un plan tarifaire.", isError: true);
            return;
        }

        // Acte engageant sur le referentiel tarifaire : la desactivation est
        // confirmee (gabarit de la charte) ; l'activation reste sans confirmation.
        if (!isActive)
        {
            var confirmed = Confirm(
                $"Désactiver le plan tarifaire {selected.Code} — {selected.Label} ?\n\n" +
                "Il ne sera plus utilisé pour résoudre le tarif d'une nuit tant qu'il n'aura pas été réactivé.",
                "Désactivation d'un plan tarifaire");

            if (!confirmed)
            {
                return;
            }
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.SetRatePlanActiveAsync(current.ApiBaseUrl, selected.Code, isActive);
            await LoadPlansAsync(current);
            current.SetStatus(isActive
                ? $"Plan tarifaire {selected.Code} activé."
                : $"Plan tarifaire {selected.Code} désactivé.");
        });
    }

    // ============================== Periodes de tarif ============================

    private async void AddPeriodButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        if (editingPlanCode is not { } planCode)
        {
            current.SetStatus("Sélectionnez d'abord le plan tarifaire à compléter.", isError: true);
            return;
        }

        await current.RunAsync(async () =>
        {
            var roomTypeCode = PeriodRoomTypeTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(roomTypeCode))
            {
                current.SetStatus("Le code du type de chambre est requis.", isError: true);
                return;
            }

            if (PeriodFromDatePicker.SelectedDate is not DateTime fromDate ||
                PeriodToDatePicker.SelectedDate is not DateTime toDate)
            {
                current.SetStatus("Les dates de début et de fin de la période sont requises.", isError: true);
                return;
            }

            if (fromDate > toDate)
            {
                current.SetStatus("La date de début ne peut pas être postérieure à la date de fin.", isError: true);
                return;
            }

            if (!TryReadAmount(PeriodAmountTextBox.Text, out var amount) || amount <= 0)
            {
                current.SetStatus("Le montant de la nuitée doit être un montant strictement positif.", isError: true);
                return;
            }

            await current.ApiClient.CreateRatePeriodAsync(
                current.ApiBaseUrl,
                planCode,
                new CreateRatePeriodRequest(
                    roomTypeCode,
                    DateOnly.FromDateTime(fromDate),
                    DateOnly.FromDateTime(toDate),
                    amount));

            ResetPeriodForm();
            await LoadPeriodsAsync(current, planCode);
            current.SetStatus($"Période ajoutée au plan {planCode}.");
        });
    }

    private async void DeletePeriodButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        if (editingPlanCode is not { } planCode ||
            PeriodsDataGrid.SelectedItem is not RatePeriodResponse selected)
        {
            current.SetStatus("Sélectionnez la période de tarif à supprimer.", isError: true);
            return;
        }

        var confirmed = Confirm(
            $"Supprimer la période {selected.RoomTypeCode} du " +
            $"{selected.FromDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)} au " +
            $"{selected.ToDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)} " +
            $"({selected.NightlyAmount.ToString("N2", CultureInfo.CurrentCulture)} la nuitée) ?\n\n" +
            "Les nuits de cette période ne seront plus couvertes par ce plan.",
            "Suppression d'une période de tarif");

        if (!confirmed)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.DeleteRatePeriodAsync(current.ApiBaseUrl, planCode, selected.Id);
            await LoadPeriodsAsync(current, planCode);
            current.SetStatus($"Période supprimée du plan {planCode}.");
        });
    }

    private void ResetPeriodForm()
    {
        PeriodRoomTypeTextBox.Text = string.Empty;
        PeriodAmountTextBox.Text = string.Empty;
    }

    // ============================= Conventions clients ===========================

    private void ConventionsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ConventionsDataGrid.SelectedItem is not CustomerConventionResponse selected)
        {
            UpdateActionStates();
            return;
        }

        editingConventionId = selected.Id;
        ConventionFormTitleTextBlock.Text = $"Modifier la convention de {selected.CustomerCode}";
        ConventionCustomerTextBox.Text = selected.CustomerCode;

        // Le client d'une convention est fixe a la creation : en modification,
        // seuls le plan, la remise et la periode de validite sont editables.
        ConventionCustomerTextBox.IsEnabled = false;
        ConventionDiscountTextBox.Text = selected.DiscountPercent?.ToString("0.##", CultureInfo.CurrentCulture) ?? string.Empty;
        ConventionFromDatePicker.SelectedDate = selected.FromDate.ToDateTime(TimeOnly.MinValue);
        ConventionToDatePicker.SelectedDate = selected.ToDate.ToDateTime(TimeOnly.MinValue);

        if (ConventionPlanComboBox.ItemsSource is RatePlanResponse[] plans)
        {
            ConventionPlanComboBox.SelectedIndex = Array.FindIndex(plans, plan => plan.Code == selected.RatePlanCode);
        }

        SaveConventionButton.Content = "Modifier";
        UpdateActionStates();
    }

    private void NewConventionButton_Click(object sender, RoutedEventArgs e)
    {
        ResetConventionForm();
        UpdateActionStates();
    }

    private void ResetConventionForm()
    {
        editingConventionId = null;
        ConventionFormTitleTextBlock.Text = "Nouvelle convention";
        ConventionCustomerTextBox.Text = string.Empty;
        ConventionCustomerTextBox.IsEnabled = true;
        ConventionDiscountTextBox.Text = string.Empty;
        ConventionFromDatePicker.SelectedDate = DateTime.Today;
        ConventionToDatePicker.SelectedDate = DateTime.Today.AddYears(1);
        SaveConventionButton.Content = "Créer";
        ConventionsDataGrid.SelectedItem = null;
    }

    private async void SaveConventionButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        await current.RunAsync(async () =>
        {
            if (ConventionPlanComboBox.SelectedItem is not RatePlanResponse plan)
            {
                current.SetStatus("Sélectionnez le plan tarifaire de la convention.", isError: true);
                return;
            }

            if (ConventionFromDatePicker.SelectedDate is not DateTime fromDate ||
                ConventionToDatePicker.SelectedDate is not DateTime toDate)
            {
                current.SetStatus("Les dates de validité de la convention sont requises.", isError: true);
                return;
            }

            if (fromDate > toDate)
            {
                current.SetStatus("La date de début ne peut pas être postérieure à la date de fin.", isError: true);
                return;
            }

            decimal? discount = null;
            var discountText = ConventionDiscountTextBox.Text.Trim();

            if (!string.IsNullOrEmpty(discountText))
            {
                if (!TryReadAmount(discountText, out var parsedDiscount) || parsedDiscount < 0 || parsedDiscount > 100)
                {
                    current.SetStatus("La remise doit être un pourcentage entre 0 et 100.", isError: true);
                    return;
                }

                discount = parsedDiscount;
            }

            var from = DateOnly.FromDateTime(fromDate);
            var to = DateOnly.FromDateTime(toDate);

            if (editingConventionId is not { } conventionId)
            {
                var customerCode = ConventionCustomerTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(customerCode))
                {
                    current.SetStatus("Le code client de la convention est requis.", isError: true);
                    return;
                }

                await current.ApiClient.CreateCustomerConventionAsync(
                    current.ApiBaseUrl,
                    new CreateCustomerConventionRequest(customerCode, plan.Code, discount, from, to));
                current.SetStatus($"Convention créée pour le client {customerCode}.");
            }
            else
            {
                await current.ApiClient.UpdateCustomerConventionAsync(
                    current.ApiBaseUrl,
                    conventionId,
                    new UpdateCustomerConventionRequest(plan.Code, discount, from, to));
                current.SetStatus("Convention mise à jour.");
            }

            await LoadConventionsAsync(current);
        });
    }

    private async void ActivateConventionButton_Click(object sender, RoutedEventArgs e)
    {
        await SetConventionActiveAsync(isActive: true);
    }

    private async void DeactivateConventionButton_Click(object sender, RoutedEventArgs e)
    {
        await SetConventionActiveAsync(isActive: false);
    }

    private async Task SetConventionActiveAsync(bool isActive)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        if (ConventionsDataGrid.SelectedItem is not CustomerConventionResponse selected)
        {
            current.SetStatus("Sélectionnez une convention client.", isError: true);
            return;
        }

        if (!isActive)
        {
            var confirmed = Confirm(
                $"Désactiver la convention du client {selected.CustomerCode} (plan {selected.RatePlanCode}) ?\n\n" +
                "Elle ne sera plus appliquée à la résolution des tarifs de ce client tant qu'elle n'aura pas été réactivée.",
                "Désactivation d'une convention");

            if (!confirmed)
            {
                return;
            }
        }

        await current.RunAsync(async () =>
        {
            await current.ApiClient.SetCustomerConventionActiveAsync(current.ApiBaseUrl, selected.Id, isActive);
            await LoadConventionsAsync(current);
            current.SetStatus(isActive
                ? $"Convention du client {selected.CustomerCode} activée."
                : $"Convention du client {selected.CustomerCode} désactivée.");
        });
    }

    // ================================= Resolution ================================

    private async void ResolveTariffButton_Click(object sender, RoutedEventArgs e)
    {
        var current = context;

        if (current is null)
        {
            return;
        }

        if (ResolveUnitComboBox.SelectedItem is not HotelUnitResponse unit)
        {
            current.SetStatus("Sélectionnez l'unité hôtelière du test.", isError: true);
            return;
        }

        var roomTypeCode = ResolveRoomTypeTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(roomTypeCode))
        {
            current.SetStatus("Le code du type de chambre est requis.", isError: true);
            return;
        }

        if (ResolveNightDatePicker.SelectedDate is not DateTime night)
        {
            current.SetStatus("Sélectionnez la nuit à tester.", isError: true);
            return;
        }

        var customerCode = ResolveCustomerTextBox.Text.Trim();

        await current.RunAsync(async () =>
        {
            var resolved = await current.ApiClient.ResolveTariffAsync(
                current.ApiBaseUrl,
                unit.Code,
                roomTypeCode,
                DateOnly.FromDateTime(night),
                string.IsNullOrEmpty(customerCode) ? null : customerCode);

            var amountText = resolved.Amount.ToString("N2", CultureInfo.CurrentCulture);
            var conventionText = resolved.ConventionCustomerCode is null
                ? "sans convention"
                : resolved.DiscountPercent is { } discountPercent
                    ? $"convention du client {resolved.ConventionCustomerCode}, remise {discountPercent.ToString("0.##", CultureInfo.CurrentCulture)} %"
                    : $"convention du client {resolved.ConventionCustomerCode}";

            ResolveResultTextBlock.Text =
                $"Nuitée du {night.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)} : {amountText} — plan {resolved.RatePlanCode} ({conventionText}).";
            current.SetStatus("Tarif résolu.");
        });
    }

    // ================================== Etats ====================================

    // Une action d'ecriture indisponible est grisee plutot que de laisser
    // l'utilisateur declencher un 403 previsible.
    private void UpdateActionStates()
    {
        var hasPlanSelection = PlansDataGrid.SelectedItem is RatePlanResponse;
        var hasPeriodTarget = editingPlanCode is not null;
        var hasConventionSelection = ConventionsDataGrid.SelectedItem is CustomerConventionResponse;

        SavePlanButton.IsEnabled = canWrite;
        SetDefaultPlanButton.IsEnabled = canWrite && hasPlanSelection;
        ActivatePlanButton.IsEnabled = canWrite && hasPlanSelection;
        DeactivatePlanButton.IsEnabled = canWrite && hasPlanSelection;
        AddPeriodButton.IsEnabled = canWrite && hasPeriodTarget;
        DeletePeriodButton.IsEnabled = canWrite && hasPeriodTarget;
        SaveConventionButton.IsEnabled = canWrite;
        ActivateConventionButton.IsEnabled = canWrite && hasConventionSelection;
        DeactivateConventionButton.IsEnabled = canWrite && hasConventionSelection;

        ApplyPermissionHint(SavePlanButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(SetDefaultPlanButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(ActivatePlanButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(DeactivatePlanButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(AddPeriodButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(DeletePeriodButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(SaveConventionButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(ActivateConventionButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(DeactivateConventionButton, canWrite, WritePermissionHint);
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

    // Montants et pourcentages : culture du poste d'abord, repli invariant (meme
    // tolerance de saisie que TryReadMoney dans MainWindow).
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
}
