using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RaqmiSystem.Application.HumanResources;
using RaqmiSystem.Domain.HumanResources;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module Ressources humaines et paie. Quatre sections, dans l'ordre du cycle metier :
/// Collaborateurs (annuaire, fiche, contrats), Temps et absences, Paie du mois, Referentiel.
///
/// Deux regles de cet ecran ne sont pas cosmetiques :
/// la CLOTURE de la paie demande une confirmation explicite, parce qu'elle est irreversible et
/// verrouille le mois cote serveur ; et l'annuaire n'affiche AUCUN identifiant legal (NIN, NSS,
/// RIB), parce que ces donnees relevent de la loi 18-07 et ne sont servies que par la fiche,
/// dont la consultation est journalisee.
///
/// Chargement paresseux par section : ouvrir l'onglet Paie ne doit pas ramener le referentiel,
/// et l'ecran s'ouvre sur l'annuaire seul.
///
/// Vue autonome : elle ne connait que le ModuleViewContext que la fenetre lui prete.
/// </summary>
public partial class HumanResourcesView : UserControl
{
    private const string WritePermission = PermissionCatalog.HrWrite;

    private const string PayrollPermission = PermissionCatalog.HrPayroll;

    private const string ClosePermission = PermissionCatalog.HrPayrollClose;

    private const int EmployeesSection = 0;

    private const int TimeSection = 1;

    private const int PayrollSection = 2;

    private const int ReferenceSection = 3;

    private ModuleViewContext? context;

    // Droits du profil connecte, memorises a l'ouverture de session. Confort d'interface
    // uniquement : le serveur reste la seule autorite en matiere d'autorisation.
    private bool canWrite;

    private bool canRunPayroll;

    private bool canClosePayroll;

    // Sections deja chargees : le changement d'onglet ne relance pas un appel inutile.
    private readonly HashSet<int> loadedSections = [];

    private Guid? selectedEmployeeId;

    private IReadOnlyCollection<EmployeeSummaryResponse> employees = [];

    public HumanResourcesView()
    {
        InitializeComponent();

        PeriodTextBox.Text = DateTime.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        var firstOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        TimeFromDatePicker.SelectedDate = firstOfMonth;
        TimeToDatePicker.SelectedDate = firstOfMonth.AddMonths(1).AddDays(-1);
        TimeEntryDatePicker.SelectedDate = DateTime.Today;
        HireDatePicker.SelectedDate = DateTime.Today;
        ContractStartDatePicker.SelectedDate = DateTime.Today;
        AbsenceStartDatePicker.SelectedDate = DateTime.Today;
        AbsenceEndDatePicker.SelectedDate = DateTime.Today;
        TerminationDatePicker.SelectedDate = DateTime.Today;

        FillEnumCombo(ContractTypeComboBox, ContractType.Permanent);
        FillEnumCombo(AbsenceTypeComboBox, AbsenceType.AnnualLeave);
        FillStatusFilter(EmployeeStatusFilterComboBox, "Tous les statuts", Enum.GetValues<EmployeeStatus>().Cast<object>());
        FillStatusFilter(AbsenceStatusFilterComboBox, "Toutes", Enum.GetValues<AbsenceStatus>().Cast<object>());

        UpdateActionButtons();
        ClearEmployeeForm();
    }

    /// <summary>Memorise le contexte fourni par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext context)
    {
        this.context = context;
        canWrite = context.HasPermission(WritePermission);
        canRunPayroll = context.HasPermission(PayrollPermission);
        canClosePayroll = context.HasPermission(ClosePermission);

        UpdateActionButtons();
    }

    /// <summary>
    /// (Re)charge la section active. Sort silencieusement tant qu'aucun contexte n'est disponible
    /// ou qu'aucune session n'est ouverte.
    /// </summary>
    public async Task LoadAsync()
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        loadedSections.Clear();

        await moduleContext.RunAsync(() => LoadSectionAsync(moduleContext, SectionTabs.SelectedIndex));
    }

    /// <summary>Vide toutes les grilles et tous les indicateurs (appelee a la deconnexion).</summary>
    public void ResetState()
    {
        loadedSections.Clear();
        selectedEmployeeId = null;
        employees = [];

        EmployeesDataGrid.ItemsSource = null;
        ContractsDataGrid.ItemsSource = null;
        TimeEntriesDataGrid.ItemsSource = null;
        AbsencesDataGrid.ItemsSource = null;
        PayslipsDataGrid.ItemsSource = null;
        BonusesDataGrid.ItemsSource = null;
        DepartmentsDataGrid.ItemsSource = null;
        PositionsDataGrid.ItemsSource = null;
        WarningsItemsControl.ItemsSource = null;
        WarningsBorder.Visibility = Visibility.Collapsed;

        PeriodStatusTextBlock.Text = "—";
        PeriodStatusDetailTextBlock.Text = string.Empty;
        PayslipCountTextBlock.Text = "—";
        DraftCountTextBlock.Text = string.Empty;
        TotalGrossTextBlock.Text = "—";
        TotalNetTextBlock.Text = "—";
        TotalEmployerCostTextBlock.Text = "—";

        ClearEmployeeForm();
    }

    // ================================ Navigation ================================

    private async void OnSectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Le TabControl leve aussi cet evenement pour ses grilles internes : on ne reagit
        // qu'a un changement de section.
        if (!ReferenceEquals(e.OriginalSource, SectionTabs))
        {
            return;
        }

        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        var section = SectionTabs.SelectedIndex;

        if (loadedSections.Contains(section))
        {
            return;
        }

        await moduleContext.RunAsync(() => LoadSectionAsync(moduleContext, section));
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    private async Task LoadSectionAsync(ModuleViewContext moduleContext, int section)
    {
        switch (section)
        {
            case EmployeesSection:
                await ReloadEmployeesAsync(moduleContext);
                await ReloadReferenceAsync(moduleContext);
                break;
            case TimeSection:
                await EnsureEmployeeOptionsAsync(moduleContext);
                await ReloadTimeEntriesAsync(moduleContext);
                await ReloadAbsencesAsync(moduleContext);
                break;
            case PayrollSection:
                await EnsureEmployeeOptionsAsync(moduleContext);
                await ReloadPayrollAsync(moduleContext, keepWarnings: false);
                break;
            case ReferenceSection:
                await ReloadReferenceAsync(moduleContext);
                break;
        }

        loadedSections.Add(section);
    }

    // ============================== Collaborateurs ==============================

    private async void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        await RunAsync(ReloadEmployeesAsync);
    }

    private async void OnEmployeeFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || context is null)
        {
            return;
        }

        await RunAsync(ReloadEmployeesAsync);
    }

    private async void OnEmployeeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EmployeesDataGrid.SelectedItem is not EmployeeSummaryResponse summary)
        {
            return;
        }

        await RunAsync(async moduleContext =>
        {
            // La fiche detaillee est un appel dedie : c'est lui, et lui seul, qui expose les
            // identifiants legaux, et le serveur en ecrit une trace d'audit.
            var employee = await moduleContext.ApiClient.GetHrEmployeeAsync(moduleContext.ApiBaseUrl, summary.Id);
            FillEmployeeForm(employee);

            var contracts = await moduleContext.ApiClient.GetHrContractsAsync(moduleContext.ApiBaseUrl, employee.Id);
            ContractsDataGrid.ItemsSource = contracts;
        });
    }

    private void OnNewEmployeeClick(object sender, RoutedEventArgs e)
    {
        EmployeesDataGrid.SelectedItem = null;
        ContractsDataGrid.ItemsSource = null;
        ClearEmployeeForm();
    }

    private async void OnSaveEmployeeClick(object sender, RoutedEventArgs e)
    {
        var unitCode = EmployeeUnitComboBox.SelectedItem as string;
        var positionCode = EmployeePositionComboBox.SelectedValue as string;

        if (string.IsNullOrWhiteSpace(unitCode) || string.IsNullOrWhiteSpace(positionCode))
        {
            SetError("Sélectionnez une unité et un poste.");
            return;
        }

        if (!TryReadInt(DependentChildrenTextBox, "Enfants à charge", out var children))
        {
            return;
        }

        await RunAsync(async moduleContext =>
        {
            if (selectedEmployeeId is null)
            {
                if (HireDatePicker.SelectedDate is null)
                {
                    SetError("La date d'embauche est obligatoire.");
                    return;
                }

                var created = await moduleContext.ApiClient.CreateHrEmployeeAsync(
                    moduleContext.ApiBaseUrl,
                    new CreateEmployeeRequest(
                        EmployeeNumberTextBox.Text.Trim(),
                        FirstNameTextBox.Text.Trim(),
                        LastNameTextBox.Text.Trim(),
                        unitCode,
                        positionCode,
                        DateOnly.FromDateTime(HireDatePicker.SelectedDate.Value),
                        NullIfBlank(EmployeeEmailTextBox.Text),
                        NullIfBlank(EmployeePhoneTextBox.Text),
                        NullIfBlank(NationalIdentityTextBox.Text),
                        NullIfBlank(SocialSecurityTextBox.Text),
                        NullIfBlank(BankAccountTextBox.Text),
                        NullIfBlank(BadgeTextBox.Text),
                        children));

                moduleContext.SetStatus($"Collaborateur {created.EmployeeNumber} créé.");
            }
            else
            {
                var updated = await moduleContext.ApiClient.UpdateHrEmployeeAsync(
                    moduleContext.ApiBaseUrl,
                    selectedEmployeeId.Value,
                    new UpdateEmployeeRequest(
                        FirstNameTextBox.Text.Trim(),
                        LastNameTextBox.Text.Trim(),
                        unitCode,
                        positionCode,
                        NullIfBlank(EmployeeEmailTextBox.Text),
                        NullIfBlank(EmployeePhoneTextBox.Text),
                        NullIfBlank(NationalIdentityTextBox.Text),
                        NullIfBlank(SocialSecurityTextBox.Text),
                        NullIfBlank(BankAccountTextBox.Text),
                        NullIfBlank(BadgeTextBox.Text),
                        children));

                moduleContext.SetStatus($"Fiche de {updated.EmployeeNumber} enregistrée.");
            }

            await ReloadEmployeesAsync(moduleContext);
        });
    }

    private Task OnSuspendEmployeeClickCore(bool suspended)
    {
        return RunAsync(async moduleContext =>
        {
            if (selectedEmployeeId is null)
            {
                SetError("Sélectionnez un collaborateur.");
                return;
            }

            var employee = await moduleContext.ApiClient.SetHrEmployeeSuspendedAsync(
                moduleContext.ApiBaseUrl,
                selectedEmployeeId.Value,
                suspended);

            moduleContext.SetStatus(
                suspended
                    ? $"Collaborateur {employee.EmployeeNumber} suspendu : il ne sera plus repris par la pré-paie."
                    : $"Collaborateur {employee.EmployeeNumber} réactivé.");

            await ReloadEmployeesAsync(moduleContext);
        });
    }

    private async void OnSuspendEmployeeClick(object sender, RoutedEventArgs e)
    {
        await OnSuspendEmployeeClickCore(suspended: true);
    }

    private async void OnReactivateEmployeeClick(object sender, RoutedEventArgs e)
    {
        await OnSuspendEmployeeClickCore(suspended: false);
    }

    private async void OnTerminateEmployeeClick(object sender, RoutedEventArgs e)
    {
        if (selectedEmployeeId is null)
        {
            SetError("Sélectionnez un collaborateur.");
            return;
        }

        if (TerminationDatePicker.SelectedDate is null)
        {
            SetError("Indiquez la date de fin de relation.");
            return;
        }

        if (string.IsNullOrWhiteSpace(TerminationReasonTextBox.Text))
        {
            SetError("Le motif de la fin de relation est obligatoire.");
            return;
        }

        var terminationDate = DateOnly.FromDateTime(TerminationDatePicker.SelectedDate.Value);

        var confirmation = MessageBox.Show(
            $"Mettre fin à la relation de travail de {FirstNameTextBox.Text} {LastNameTextBox.Text} "
            + $"au {terminationDate:dd/MM/yyyy} ?\n\n"
            + "Le contrat actif sera clôturé à cette date. Le collaborateur reste payé pour le mois "
            + "de son départ.",
            "Fin de relation de travail",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        await RunAsync(async moduleContext =>
        {
            var employee = await moduleContext.ApiClient.TerminateHrEmployeeAsync(
                moduleContext.ApiBaseUrl,
                selectedEmployeeId.Value,
                new TerminateEmployeeRequest(terminationDate, TerminationReasonTextBox.Text.Trim()));

            moduleContext.SetStatus($"Fin de relation enregistrée pour {employee.EmployeeNumber}.");

            await ReloadEmployeesAsync(moduleContext);
        });
    }

    private async void OnAddContractClick(object sender, RoutedEventArgs e)
    {
        if (selectedEmployeeId is null)
        {
            SetError("Sélectionnez un collaborateur avant d'ajouter un contrat.");
            return;
        }

        if (ContractTypeComboBox.SelectedItem is not ContractType type
            || ContractStartDatePicker.SelectedDate is null)
        {
            SetError("Le type et la date de début du contrat sont obligatoires.");
            return;
        }

        if (!TryReadDecimal(ContractGrossSalaryTextBox, "Salaire brut", out var grossSalary)
            || !TryReadDecimal(ContractWeeklyHoursTextBox, "Heures / semaine", out var weeklyHours))
        {
            return;
        }

        // Un CDI ne porte pas de date de fin, un CDD en exige une : le serveur applique la regle,
        // on evite juste un aller-retour perdu.
        DateOnly? endDate = ContractEndDatePicker.SelectedDate is null
            ? null
            : DateOnly.FromDateTime(ContractEndDatePicker.SelectedDate.Value);

        if (type == ContractType.Permanent)
        {
            endDate = null;
        }
        else if (endDate is null)
        {
            SetError("Un contrat à durée déterminée exige une date de fin.");
            return;
        }

        await RunAsync(async moduleContext =>
        {
            var contract = await moduleContext.ApiClient.CreateHrContractAsync(
                moduleContext.ApiBaseUrl,
                selectedEmployeeId.Value,
                new CreateContractRequest(
                    type,
                    DateOnly.FromDateTime(ContractStartDatePicker.SelectedDate.Value),
                    endDate,
                    grossSalary,
                    weeklyHours));

            moduleContext.SetStatus($"Contrat {contract.Type} créé à compter du {contract.StartDate:dd/MM/yyyy}.");

            var contracts = await moduleContext.ApiClient.GetHrContractsAsync(
                moduleContext.ApiBaseUrl,
                selectedEmployeeId.Value);

            ContractsDataGrid.ItemsSource = contracts;

            await ReloadEmployeesAsync(moduleContext);
        });
    }

    // ============================= Temps et absences =============================

    private async void OnReloadTimeClick(object sender, RoutedEventArgs e)
    {
        await RunAsync(ReloadTimeEntriesAsync);
    }

    private async void OnSaveTimeEntryClick(object sender, RoutedEventArgs e)
    {
        if (TimeEmployeeComboBox.SelectedValue is not Guid employeeId
            || TimeEntryDatePicker.SelectedDate is null)
        {
            SetError("Sélectionnez un collaborateur et un jour.");
            return;
        }

        if (!TryReadDecimal(TimeEntryHoursTextBox, "Heures", out var hours))
        {
            return;
        }

        await RunAsync(async moduleContext =>
        {
            await moduleContext.ApiClient.SaveHrTimeEntryAsync(
                moduleContext.ApiBaseUrl,
                new SaveTimeEntryRequest(
                    employeeId,
                    DateOnly.FromDateTime(TimeEntryDatePicker.SelectedDate.Value),
                    hours,
                    TimeEntrySource.Manual));

            moduleContext.SetStatus("Pointage enregistré en brouillon. Validez-le pour qu'il alimente la paie.");

            await ReloadTimeEntriesAsync(moduleContext);
        });
    }

    private async void OnValidateTimeEntryClick(object sender, RoutedEventArgs e)
    {
        if (TimeEntriesDataGrid.SelectedItem is not TimeEntryResponse entry)
        {
            SetError("Sélectionnez un pointage à valider.");
            return;
        }

        await RunAsync(async moduleContext =>
        {
            await moduleContext.ApiClient.ValidateHrTimeEntryAsync(moduleContext.ApiBaseUrl, entry.Id);
            moduleContext.SetStatus($"Pointage du {entry.WorkDate:dd/MM/yyyy} validé.");

            await ReloadTimeEntriesAsync(moduleContext);
        });
    }

    private async void OnAbsenceFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || context is null || !loadedSections.Contains(TimeSection))
        {
            return;
        }

        await RunAsync(ReloadAbsencesAsync);
    }

    private async void OnCreateAbsenceClick(object sender, RoutedEventArgs e)
    {
        if (AbsenceEmployeeComboBox.SelectedValue is not Guid employeeId
            || AbsenceTypeComboBox.SelectedItem is not AbsenceType type
            || AbsenceStartDatePicker.SelectedDate is null
            || AbsenceEndDatePicker.SelectedDate is null)
        {
            SetError("Collaborateur, type et dates de l'absence sont obligatoires.");
            return;
        }

        await RunAsync(async moduleContext =>
        {
            await moduleContext.ApiClient.CreateHrAbsenceAsync(
                moduleContext.ApiBaseUrl,
                new CreateAbsenceRequest(
                    employeeId,
                    type,
                    DateOnly.FromDateTime(AbsenceStartDatePicker.SelectedDate.Value),
                    DateOnly.FromDateTime(AbsenceEndDatePicker.SelectedDate.Value),
                    NullIfBlank(AbsenceReasonTextBox.Text)));

            moduleContext.SetStatus("Absence enregistrée, en attente de décision.");

            await ReloadAbsencesAsync(moduleContext);
        });
    }

    private async void OnApproveAbsenceClick(object sender, RoutedEventArgs e)
    {
        await DecideAbsenceAsync(approve: true);
    }

    private async void OnRejectAbsenceClick(object sender, RoutedEventArgs e)
    {
        await DecideAbsenceAsync(approve: false);
    }

    private Task DecideAbsenceAsync(bool approve)
    {
        if (AbsencesDataGrid.SelectedItem is not AbsenceResponse absence)
        {
            SetError("Sélectionnez une absence.");
            return Task.CompletedTask;
        }

        return RunAsync(async moduleContext =>
        {
            var request = new DecideAbsenceRequest(NullIfBlank(AbsenceReasonTextBox.Text));

            if (approve)
            {
                await moduleContext.ApiClient.ApproveHrAbsenceAsync(moduleContext.ApiBaseUrl, absence.Id, request);
                moduleContext.SetStatus(
                    absence.IsUnpaid
                        ? $"Absence sans solde approuvée : {absence.TotalDays} jour(s) seront retenus sur la paie."
                        : "Absence approuvée.");
            }
            else
            {
                await moduleContext.ApiClient.RejectHrAbsenceAsync(moduleContext.ApiBaseUrl, absence.Id, request);
                moduleContext.SetStatus("Absence refusée.");
            }

            await ReloadAbsencesAsync(moduleContext);
        });
    }

    // ==================================== Paie ====================================

    private async void OnGenerateClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadPeriod(out var period))
        {
            return;
        }

        await RunAsync(async moduleContext =>
        {
            var run = await moduleContext.ApiClient.GeneratePrePayrollAsync(moduleContext.ApiBaseUrl, period);

            // Le compte des bulletins ignores est affiche explicitement : c'est la preuve, pour
            // l'operateur, que relancer la pre-paie n'a rien reecrit de ce qui etait deja valide.
            moduleContext.SetStatus(
                $"Pré-paie {run.Period} : {run.Generated} bulletin(s) créé(s), {run.Updated} recalculé(s), "
                + $"{run.SkippedValidated} déjà validé(s) et laissé(s) intact(s).");

            ShowWarnings(run.Warnings);

            await ReloadPayrollAsync(moduleContext, keepWarnings: true);
        });
    }

    private async void OnValidatePayslipClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadPeriod(out var period))
        {
            return;
        }

        if (PayslipsDataGrid.SelectedItem is not PayslipResponse payslip)
        {
            SetError("Sélectionnez un bulletin à valider.");
            return;
        }

        await RunAsync(async moduleContext =>
        {
            await moduleContext.ApiClient.ValidatePayslipAsync(moduleContext.ApiBaseUrl, period, payslip.Id);
            moduleContext.SetStatus($"Bulletin de {payslip.EmployeeFullName} validé pour {period}.");

            await ReloadPayrollAsync(moduleContext, keepWarnings: true);
        });
    }

    private async void OnValidatePeriodClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadPeriod(out var period))
        {
            return;
        }

        await RunAsync(async moduleContext =>
        {
            var result = await moduleContext.ApiClient.ValidatePayrollPeriodAsync(moduleContext.ApiBaseUrl, period);
            moduleContext.SetStatus($"Période {result.Period} validée : {result.PayslipCount} bulletin(s) contrôlé(s).");

            await ReloadPayrollAsync(moduleContext, keepWarnings: true);
        });
    }

    private async void OnClosePeriodClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadPeriod(out var period))
        {
            return;
        }

        // Confirmation explicite : la cloture est le seul acte irreversible de cet ecran.
        var confirmation = MessageBox.Show(
            $"Clôturer définitivement la paie de {period} ?\n\n"
            + "Après la clôture, aucun bulletin, prime, pointage ni absence sans solde de ce mois "
            + "ne pourra plus être modifié. Une correction devra passer par une régularisation "
            + "sur une période ouverte.",
            "Clôture de la paie",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        await RunAsync(async moduleContext =>
        {
            var result = await moduleContext.ApiClient.ClosePayrollPeriodAsync(moduleContext.ApiBaseUrl, period);
            moduleContext.SetStatus($"Période {result.Period} clôturée. Le mois est verrouillé.");

            await ReloadPayrollAsync(moduleContext, keepWarnings: false);
        });
    }

    private async void OnAddBonusClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadPeriod(out var period))
        {
            return;
        }

        if (BonusEmployeeComboBox.SelectedValue is not Guid employeeId)
        {
            SetError("Sélectionnez un collaborateur.");
            return;
        }

        if (!TryReadDecimal(BonusAmountTextBox, "Montant", out var amount))
        {
            return;
        }

        await RunAsync(async moduleContext =>
        {
            await moduleContext.ApiClient.AddPayrollBonusAsync(
                moduleContext.ApiBaseUrl,
                period,
                new CreateBonusRequest(
                    employeeId,
                    BonusCodeTextBox.Text.Trim(),
                    BonusLabelTextBox.Text.Trim(),
                    amount));

            moduleContext.SetStatus("Prime enregistrée. Relancez la pré-paie pour qu'elle entre dans le brut.");

            await ReloadPayrollAsync(moduleContext, keepWarnings: true);
        });
    }

    private async void OnDeleteBonusClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadPeriod(out var period))
        {
            return;
        }

        if (BonusesDataGrid.SelectedItem is not PayrollBonusResponse bonus)
        {
            SetError("Sélectionnez une prime.");
            return;
        }

        await RunAsync(async moduleContext =>
        {
            await moduleContext.ApiClient.DeletePayrollBonusAsync(moduleContext.ApiBaseUrl, period, bonus.Id);
            moduleContext.SetStatus("Prime supprimée. Relancez la pré-paie pour la retirer du brut.");

            await ReloadPayrollAsync(moduleContext, keepWarnings: true);
        });
    }

    // ================================ Referentiel ================================

    private async void OnAddDepartmentClick(object sender, RoutedEventArgs e)
    {
        await RunAsync(async moduleContext =>
        {
            await moduleContext.ApiClient.CreateHrDepartmentAsync(
                moduleContext.ApiBaseUrl,
                new CreateDepartmentRequest(
                    DepartmentCodeTextBox.Text.Trim(),
                    DepartmentLabelTextBox.Text.Trim()));

            moduleContext.SetStatus("Département créé.");

            DepartmentCodeTextBox.Clear();
            DepartmentLabelTextBox.Clear();

            await ReloadReferenceAsync(moduleContext);
        });
    }

    private async void OnAddPositionClick(object sender, RoutedEventArgs e)
    {
        if (PositionDepartmentComboBox.SelectedValue is not string departmentCode)
        {
            SetError("Sélectionnez un département.");
            return;
        }

        if (!TryReadDecimal(PositionMinimumSalaryTextBox, "Plancher", out var minimumSalary))
        {
            return;
        }

        await RunAsync(async moduleContext =>
        {
            await moduleContext.ApiClient.CreateHrPositionAsync(
                moduleContext.ApiBaseUrl,
                new CreatePositionRequest(
                    PositionCodeTextBox.Text.Trim(),
                    PositionLabelTextBox.Text.Trim(),
                    departmentCode,
                    minimumSalary));

            moduleContext.SetStatus("Poste créé.");

            PositionCodeTextBox.Clear();
            PositionLabelTextBox.Clear();
            PositionMinimumSalaryTextBox.Clear();

            await ReloadReferenceAsync(moduleContext);
        });
    }

    // ================================ Chargements ================================

    private async Task ReloadEmployeesAsync(ModuleViewContext moduleContext)
    {
        var status = EmployeeStatusFilterComboBox.SelectedItem as EmployeeStatus?;

        employees = await moduleContext.ApiClient.GetHrEmployeesAsync(
            moduleContext.ApiBaseUrl,
            status: status,
            search: NullIfBlank(SearchTextBox.Text));

        EmployeesDataGrid.ItemsSource = employees;
        FillEmployeeOptions();
    }

    private async Task EnsureEmployeeOptionsAsync(ModuleViewContext moduleContext)
    {
        if (employees.Count > 0)
        {
            return;
        }

        employees = await moduleContext.ApiClient.GetHrEmployeesAsync(moduleContext.ApiBaseUrl);
        FillEmployeeOptions();
    }

    private async Task ReloadTimeEntriesAsync(ModuleViewContext moduleContext)
    {
        if (TimeFromDatePicker.SelectedDate is null || TimeToDatePicker.SelectedDate is null)
        {
            return;
        }

        var entries = await moduleContext.ApiClient.GetHrTimeEntriesAsync(
            moduleContext.ApiBaseUrl,
            DateOnly.FromDateTime(TimeFromDatePicker.SelectedDate.Value),
            DateOnly.FromDateTime(TimeToDatePicker.SelectedDate.Value));

        TimeEntriesDataGrid.ItemsSource = entries;
    }

    private async Task ReloadAbsencesAsync(ModuleViewContext moduleContext)
    {
        var status = AbsenceStatusFilterComboBox.SelectedItem as AbsenceStatus?;

        var absences = await moduleContext.ApiClient.GetHrAbsencesAsync(
            moduleContext.ApiBaseUrl,
            status: status);

        AbsencesDataGrid.ItemsSource = absences;
    }

    private async Task ReloadPayrollAsync(ModuleViewContext moduleContext, bool keepWarnings)
    {
        if (!TryReadPeriod(out var period))
        {
            return;
        }

        var summary = await moduleContext.ApiClient.GetPayrollPeriodAsync(moduleContext.ApiBaseUrl, period);
        var payslips = await moduleContext.ApiClient.GetPayslipsAsync(moduleContext.ApiBaseUrl, period);
        var bonuses = await moduleContext.ApiClient.GetPayrollBonusesAsync(moduleContext.ApiBaseUrl, period);

        PayslipsDataGrid.ItemsSource = payslips;
        BonusesDataGrid.ItemsSource = bonuses;

        PeriodStatusTextBlock.Text = DescribeStatus(summary.Status);
        PeriodStatusDetailTextBlock.Text = DescribeStatusDetail(summary);
        PayslipCountTextBlock.Text = summary.PayslipCount.ToString(CultureInfo.CurrentCulture);
        DraftCountTextBlock.Text = summary.DraftPayslipCount == 0
            ? "aucun brouillon"
            : $"{summary.DraftPayslipCount} en brouillon";

        TotalGrossTextBlock.Text = FormatAmount(summary.TotalTaxableGross);
        TotalNetTextBlock.Text = FormatAmount(summary.TotalNetPay);
        TotalEmployerCostTextBlock.Text = FormatAmount(summary.TotalEmployerCost);

        if (!keepWarnings)
        {
            ShowWarnings([]);
        }

        UpdateActionButtons(summary.Status);
    }

    private async Task ReloadReferenceAsync(ModuleViewContext moduleContext)
    {
        var departments = await moduleContext.ApiClient.GetHrDepartmentsAsync(moduleContext.ApiBaseUrl, includeInactive: true);
        var positions = await moduleContext.ApiClient.GetHrPositionsAsync(moduleContext.ApiBaseUrl, includeInactive: true);
        var units = await moduleContext.ApiClient.GetHotelUnitsAsync(moduleContext.ApiBaseUrl, includeInactive: false);

        DepartmentsDataGrid.ItemsSource = departments;
        PositionsDataGrid.ItemsSource = positions;

        var activeDepartments = departments.Where(department => department.IsActive).ToArray();
        RestoreSelection(PositionDepartmentComboBox, activeDepartments, department => department.Code);

        var activePositions = positions.Where(position => position.IsActive).ToArray();
        RestoreSelection(EmployeePositionComboBox, activePositions, position => position.Code);

        var unitCodes = units.Select(unit => unit.Code).ToArray();
        var selectedUnit = EmployeeUnitComboBox.SelectedItem as string;
        EmployeeUnitComboBox.ItemsSource = unitCodes;

        if (selectedUnit is not null && unitCodes.Contains(selectedUnit))
        {
            EmployeeUnitComboBox.SelectedItem = selectedUnit;
        }
    }

    // ================================== Formulaire ==================================

    private void FillEmployeeForm(EmployeeResponse employee)
    {
        selectedEmployeeId = employee.Id;

        EmployeeFormTitleTextBlock.Text = $"Fiche de {employee.FirstName} {employee.LastName}";
        EmployeeNumberTextBox.Text = employee.EmployeeNumber;
        FirstNameTextBox.Text = employee.FirstName;
        LastNameTextBox.Text = employee.LastName;
        EmployeeEmailTextBox.Text = employee.Email ?? string.Empty;
        EmployeePhoneTextBox.Text = employee.Phone ?? string.Empty;
        NationalIdentityTextBox.Text = employee.NationalIdentityNumber ?? string.Empty;
        SocialSecurityTextBox.Text = employee.SocialSecurityNumber ?? string.Empty;
        BankAccountTextBox.Text = employee.BankAccountNumber ?? string.Empty;
        BadgeTextBox.Text = employee.BadgeId ?? string.Empty;
        DependentChildrenTextBox.Text = employee.DependentChildren.ToString(CultureInfo.CurrentCulture);
        HireDatePicker.SelectedDate = employee.HireDate.ToDateTime(TimeOnly.MinValue);
        EmployeeUnitComboBox.SelectedItem = employee.HotelUnitCode;
        EmployeePositionComboBox.SelectedValue = employee.PositionCode;

        // Le matricule et la date d'embauche identifient le dossier : ils ne se modifient pas
        // depuis la fiche, l'API ne les accepte pas non plus en mise a jour.
        EmployeeNumberTextBox.IsEnabled = false;
        HireDatePicker.IsEnabled = false;

        UpdateActionButtons();
    }

    private void ClearEmployeeForm()
    {
        selectedEmployeeId = null;

        EmployeeFormTitleTextBlock.Text = "Nouveau collaborateur";
        EmployeeNumberTextBox.Clear();
        FirstNameTextBox.Clear();
        LastNameTextBox.Clear();
        EmployeeEmailTextBox.Clear();
        EmployeePhoneTextBox.Clear();
        NationalIdentityTextBox.Clear();
        SocialSecurityTextBox.Clear();
        BankAccountTextBox.Clear();
        BadgeTextBox.Clear();
        DependentChildrenTextBox.Text = "0";
        TerminationReasonTextBox.Clear();
        HireDatePicker.SelectedDate = DateTime.Today;

        EmployeeNumberTextBox.IsEnabled = true;
        HireDatePicker.IsEnabled = true;

        UpdateActionButtons();
    }

    private void FillEmployeeOptions()
    {
        var options = employees
            .Select(employee => new EmployeeOption(
                employee.Id,
                $"{employee.EmployeeNumber} — {employee.LastName} {employee.FirstName}"))
            .ToArray();

        RestoreSelection(TimeEmployeeComboBox, options, option => option.Id);
        RestoreSelection(AbsenceEmployeeComboBox, options, option => option.Id);
        RestoreSelection(BonusEmployeeComboBox, options, option => option.Id);
    }

    /// <summary>
    /// Remplace la source d'une liste deroulante sans perdre la selection courante : recharger le
    /// referentiel apres une creation ne doit pas vider un formulaire a moitie saisi.
    /// </summary>
    private static void RestoreSelection<T, TKey>(
        ComboBox comboBox,
        IReadOnlyCollection<T> items,
        Func<T, TKey> keySelector)
    {
        var previous = comboBox.SelectedValue;

        comboBox.ItemsSource = items;

        if (previous is not null && items.Any(item => Equals(keySelector(item), previous)))
        {
            comboBox.SelectedValue = previous;
        }
    }

    private void ShowWarnings(IReadOnlyCollection<string> warnings)
    {
        WarningsItemsControl.ItemsSource = warnings;
        WarningsBorder.Visibility = warnings.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private bool TryReadPeriod(out string period)
    {
        period = PeriodTextBox.Text?.Trim() ?? string.Empty;

        // Le meme format que le serveur, verifie ici pour ne pas partir en appel reseau sur une
        // saisie qui ne peut pas aboutir.
        if (PayrollMonth.TryParse(period, out var parsed))
        {
            period = parsed.ToString();
            return true;
        }

        SetError("La période doit être saisie au format AAAA-MM (par exemple 2026-08).");
        return false;
    }

    private bool TryReadDecimal(TextBox textBox, string fieldName, out decimal value)
    {
        var text = textBox.Text?.Trim() ?? string.Empty;

        // La saisie suit la culture du poste, mais un montant tape avec un point sur un clavier
        // configure en virgule doit passer aussi : les deux lectures sont tentees.
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        SetError($"{fieldName} : saisissez un nombre valide.");
        return false;
    }

    private bool TryReadInt(TextBox textBox, string fieldName, out int value)
    {
        if (int.TryParse(textBox.Text?.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out value))
        {
            return true;
        }

        SetError($"{fieldName} : saisissez un nombre entier.");
        return false;
    }

    private void UpdateActionButtons(PayrollPeriodStatus? status = null)
    {
        SaveEmployeeButton.IsEnabled = canWrite;
        NewEmployeeButton.IsEnabled = canWrite;
        SuspendEmployeeButton.IsEnabled = canWrite && selectedEmployeeId is not null;
        ReactivateEmployeeButton.IsEnabled = canWrite && selectedEmployeeId is not null;
        TerminateEmployeeButton.IsEnabled = canWrite && selectedEmployeeId is not null;
        AddContractButton.IsEnabled = canWrite && selectedEmployeeId is not null;

        SaveTimeEntryButton.IsEnabled = canWrite;
        ValidateTimeEntryButton.IsEnabled = canWrite;
        CreateAbsenceButton.IsEnabled = canWrite;
        ApproveAbsenceButton.IsEnabled = canWrite;
        RejectAbsenceButton.IsEnabled = canWrite;

        AddDepartmentButton.IsEnabled = canWrite;
        AddPositionButton.IsEnabled = canWrite;

        var payrollOpen = status != PayrollPeriodStatus.Closed;

        GenerateButton.IsEnabled = canRunPayroll && payrollOpen;
        ValidatePayslipButton.IsEnabled = canRunPayroll && payrollOpen;
        AddBonusButton.IsEnabled = canRunPayroll && payrollOpen;
        DeleteBonusButton.IsEnabled = canRunPayroll && payrollOpen;

        // Valider puis cloturer sont deux etapes distinctes du meme droit : on n'active que
        // celle qui a un sens dans l'etat courant de la periode.
        ValidatePeriodButton.IsEnabled = canClosePayroll && status is null or PayrollPeriodStatus.Draft;
        ClosePeriodButton.IsEnabled = canClosePayroll && status == PayrollPeriodStatus.Validated;

        GenerateButton.ToolTip = canRunPayroll
            ? "Recalcule les bulletins en brouillon du mois. Les bulletins déjà validés ne sont pas touchés."
            : "Permission hr.payroll requise.";

        ClosePeriodButton.ToolTip = canClosePayroll
            ? "Verrouille définitivement le mois : plus aucun bulletin, prime, pointage ou absence sans solde ne pourra être modifié."
            : "Permission hr.payroll.close requise.";
    }

    private Task RunAsync(Func<ModuleViewContext, Task> action)
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return Task.CompletedTask;
        }

        return moduleContext.RunAsync(() => action(moduleContext));
    }

    private void SetError(string message)
    {
        context?.SetStatus(message, isError: true);
    }

    private static void FillEnumCombo<T>(ComboBox comboBox, T selected)
        where T : struct, Enum
    {
        comboBox.ItemsSource = Enum.GetValues<T>();
        comboBox.SelectedItem = selected;
    }

    /// <summary>
    /// Remplit un filtre de statut : une premiere entree "tous" (null) suivie des valeurs de
    /// l'enumeration, de sorte que la selection soit directement le filtre a appliquer.
    /// </summary>
    private static void FillStatusFilter(ComboBox comboBox, string allLabel, IEnumerable<object> values)
    {
        var items = new List<object> { allLabel };
        items.AddRange(values);

        comboBox.ItemsSource = items;
        comboBox.SelectedIndex = 0;
    }

    private static string DescribeStatus(PayrollPeriodStatus status)
    {
        return status switch
        {
            PayrollPeriodStatus.Draft => "Ouverte",
            PayrollPeriodStatus.Validated => "Validée",
            PayrollPeriodStatus.Closed => "Clôturée",
            _ => status.ToString()
        };
    }

    private static string DescribeStatusDetail(PayrollPeriodResponse summary)
    {
        return summary.Status switch
        {
            PayrollPeriodStatus.Closed when summary.ClosedAt is not null =>
                $"Clôturée le {summary.ClosedAt.Value.LocalDateTime:dd/MM/yyyy} par {summary.ClosedBy}",
            PayrollPeriodStatus.Validated when summary.ValidatedAt is not null =>
                $"Validée le {summary.ValidatedAt.Value.LocalDateTime:dd/MM/yyyy} par {summary.ValidatedBy}",
            PayrollPeriodStatus.Draft => "Modifiable",
            _ => string.Empty
        };
    }

    private static string FormatAmount(decimal value)
    {
        return value.ToString("N2", CultureInfo.CurrentCulture);
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Entree des listes deroulantes de collaborateurs.</summary>
    private sealed record EmployeeOption(Guid Id, string Display);
}
