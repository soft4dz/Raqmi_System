using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using RaqmiSystem.Application.Accounting;
using RaqmiSystem.Domain.Accounting;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module Comptabilite SCF : plan comptable, journaux, ecritures et balance generale.
///
/// Deux regles du domaine structurent l'ecran, et il les rend visibles plutot que de
/// les laisser decouvrir sur un refus du serveur :
///   - un compte ne se supprime pas, il se desactive ; son code n'est jamais
///     modifiable, puisque toutes les lignes deja passees le referencent ;
///   - une ecriture comptabilisee est immuable. On la corrige par une extourne, qui
///     cree une ecriture INVERSE ; seul un brouillon s'abandonne.
///
/// La vue est autonome : elle ne connait ni MainWindow ni les autres modules, et
/// passe par <see cref="ModuleViewContext.RunAsync"/> pour tout appel API (curseur
/// d'attente, barre de progression, traduction des erreurs).
/// </summary>
public partial class AccountingView : UserControl
{
    private const string WritePermissionHint =
        "Permission accounting.write requise : votre profil peut consulter la comptabilité, mais pas la saisir.";

    private const string PostPermissionHint =
        "Permission accounting.post requise : votre profil peut saisir des brouillons, mais pas les comptabiliser.";

    private const string AllClassesLabel = "Toutes les classes";

    private const string AllJournalsLabel = "Tous les journaux";

    private const string AllStatusesLabel = "Tous les statuts";

    private const string ChooseJournalLabel = "Sélectionner un journal…";

    // Libelles francais des enumerations du domaine : seul l'affichage est traduit,
    // la valeur envoyee a l'API reste celle du domaine.
    private static readonly AccountKindOption[] KindOptions =
    [
        new(AccountKind.Asset, "Actif"),
        new(AccountKind.Liability, "Passif"),
        new(AccountKind.Equity, "Capitaux propres"),
        new(AccountKind.Revenue, "Produit"),
        new(AccountKind.Expense, "Charge")
    ];

    private static readonly AccountKind[] AllKinds =
        KindOptions.Select(option => option.Value).ToArray();

    private static readonly EntryStatusOption[] StatusFilterOptions =
    [
        new(null, AllStatusesLabel),
        new(EntryStatus.Draft, "Brouillon"),
        new(EntryStatus.Posted, "Comptabilisée"),
        new(EntryStatus.Cancelled, "Abandonnée")
    ];

    // Info-bulles d'origine des boutons, restaurees des que le droit est present :
    // sans cette memoire, un message pose pour un profil restreint survivrait a la
    // reconnexion d'un profil qui, lui, a le droit d'ecrire.
    private readonly Dictionary<Button, object?> originalToolTips = [];

    // Lignes du brouillon en cours de composition. ObservableCollection : la grille
    // suit les ajouts et retraits sans etre rebindee.
    private readonly ObservableCollection<JournalEntryLineEditorRow> newEntryLines = [];

    private ModuleViewContext? context;

    private bool canWrite = true;
    private bool canPost = true;

    // Code du compte en cours de modification, ou null quand le formulaire cree un
    // compte. Le code lui-meme n'est jamais envoye en modification : il identifie la
    // ressource, il n'en est pas une propriete modifiable.
    private string? editingAccountCode;

    private JournalEntryResponse? selectedEntry;

    public AccountingView()
    {
        InitializeComponent();

        // Les formats {0:N2} et {0:dd/MM/yyyy} des grilles suivent la culture de
        // l'utilisateur, comme les valeurs formatees dans le code-behind.
        var languageTag = CultureInfo.CurrentCulture.IetfLanguageTag;

        if (!string.IsNullOrEmpty(languageTag))
        {
            Language = XmlLanguage.GetLanguage(languageTag);
        }

        AccountKindComboBox.ItemsSource = KindOptions;
        AccountKindComboBox.SelectedIndex = 0;

        EntryStatusComboBox.ItemsSource = StatusFilterOptions;
        EntryStatusComboBox.SelectedIndex = 0;

        NewEntryLinesDataGrid.ItemsSource = newEntryLines;

        // La liste des classes SCF est structurelle : elle ne depend pas des donnees
        // de l'etablissement, seulement du serveur qui les nomme. En attendant sa
        // reponse, le filtre propose au moins "toutes les classes".
        ResetAccountClassOptions();
        ResetJournalOptions();
        ApplyDefaultDates();
        ResetAccountForm();
        ResetNewEntryForm();
        ShowEntryDetail(null);
        PartyKindComboBox.ItemsSource = Enum.GetValues<PartyKind>();
        PartyKindComboBox.SelectedItem = PartyKind.Customer;
        FiscalYearStartDatePicker.SelectedDate = new DateTime(DateTime.Today.Year, 1, 1);
        FiscalYearEndDatePicker.SelectedDate = new DateTime(DateTime.Today.Year, 12, 31);
        LedgerFromDatePicker.SelectedDate = new DateTime(DateTime.Today.Year, 1, 1);
        LedgerToDatePicker.SelectedDate = DateTime.Today;
        UpdateActionState();
    }

    /// <summary>Memorise le contexte prete par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext moduleViewContext)
    {
        context = moduleViewContext;
        canWrite = moduleViewContext.HasPermission(PermissionCatalog.AccountingWrite);
        canPost = moduleViewContext.HasPermission(PermissionCatalog.AccountingPost);

        UpdateActionState();
    }

    /// <summary>
    /// (Re)charge la nomenclature (classes SCF, journaux), le plan comptable et les
    /// ecritures de la periode. Appelee a la premiere ouverture de l'onglet et par le
    /// bouton "Tout actualiser". La balance n'est pas chargee ici : elle porte sur une
    /// periode a choisir.
    /// </summary>
    public async Task LoadAsync()
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            await ReloadAccountClassesAsync(active);
            await ReloadJournalsAsync(active);
            await ReloadAccountsAsync(active);

            // Un rechargement des ecritures abandonne (plage de dates invalide) a
            // deja affiche son erreur : conclure "actualisee" la contredirait.
            if (await ReloadEntriesAsync(active))
            {
                active.SetStatus("Comptabilité actualisée.");
            }
        });
    }

    /// <summary>
    /// Vide toutes les surfaces de la vue : appelee a la deconnexion pour ne jamais
    /// laisser les donnees d'un utilisateur affichees pour le suivant.
    /// </summary>
    public void ResetState()
    {
        editingAccountCode = null;
        selectedEntry = null;

        AccountsDataGrid.ItemsSource = null;
        EntriesDataGrid.ItemsSource = null;
        EntryLinesDataGrid.ItemsSource = null;
        TrialBalanceDataGrid.ItemsSource = null;

        AccountsCountTextBlock.Text = "Plan comptable non chargé.";
        EntriesCountTextBlock.Text = "Écritures non chargées.";
        BalanceCountTextBlock.Text = "Balance non chargée.";
        BalancePeriodTextBlock.Text = "Aucune période chargée.";

        // Le libelle de perimetre est reecrit a chaque chargement d'apres ce que le
        // serveur annonce : sans cette remise a zero, la formulation vue par le
        // profil precedent survivrait a la deconnexion.
        BalanceScopeTextBlock.Text = "Écritures comptabilisées uniquement.";
        BalanceDifferenceTextBlock.Foreground = (Brush)FindResource("TextPrimaryBrush");

        BalanceTotalDebitTextBlock.Text = "—";
        BalanceTotalCreditTextBlock.Text = "—";
        BalanceDifferenceTextBlock.Text = "—";

        AccountSearchTextBox.Text = string.Empty;
        IncludeInactiveAccountsCheckBox.IsChecked = false;
        JournalCodeTextBox.Text = string.Empty;
        JournalLabelTextBox.Text = string.Empty;
        EntryAccountTextBox.Text = string.Empty;
        EntryStatusComboBox.SelectedIndex = 0;
        CancelEntryReasonTextBox.Text = string.Empty;

        ResetAccountClassOptions();
        ResetJournalOptions();
        ApplyDefaultDates();
        ResetAccountForm();
        ResetNewEntryForm();
        ShowEntryDetail(null);
        UpdateActionState();
    }

    private void ApplyDefaultDates()
    {
        var today = DateTime.Today;
        var firstOfMonth = new DateTime(today.Year, today.Month, 1);

        EntryFromDatePicker.SelectedDate = firstOfMonth;
        EntryToDatePicker.SelectedDate = today;
        NewEntryDatePicker.SelectedDate = today;
        BalanceFromDatePicker.SelectedDate = firstOfMonth;
        BalanceToDatePicker.SelectedDate = today;
    }

    private async void RefreshAllButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    // ============================== Plan comptable ==============================

    private void ResetAccountClassOptions()
    {
        AccountClassComboBox.ItemsSource = new[] { new AccountClassOption(null, AllClassesLabel) };
        AccountClassComboBox.SelectedIndex = 0;
    }

    private async Task ReloadAccountClassesAsync(ModuleViewContext active)
    {
        var classes = await active.ApiClient.GetAccountClassesAsync(active.ApiBaseUrl);

        var previous = (AccountClassComboBox.SelectedItem as AccountClassOption)?.Value;

        var options = new List<AccountClassOption> { new(null, AllClassesLabel) };
        options.AddRange(classes
            .OrderBy(item => item.AccountClass)
            .Select(item => new AccountClassOption(
                item.AccountClass,
                $"{item.AccountClass.ToString(CultureInfo.CurrentCulture)} — {item.Label}")));

        AccountClassComboBox.ItemsSource = options;
        AccountClassComboBox.SelectedItem = options.FirstOrDefault(option => option.Value == previous) ?? options[0];
    }

    private async void RefreshAccountsButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAccountsAsync();
    }

    private async void AccountSearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await RefreshAccountsAsync();
    }

    private async Task RefreshAccountsAsync()
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            await ReloadAccountsAsync(active);
            active.SetStatus("Plan comptable actualisé.");
        });
    }

    private async Task ReloadAccountsAsync(ModuleViewContext active)
    {
        var search = string.IsNullOrWhiteSpace(AccountSearchTextBox.Text) ? null : AccountSearchTextBox.Text.Trim();
        var accountClass = (AccountClassComboBox.SelectedItem as AccountClassOption)?.Value;

        var accounts = await active.ApiClient.GetChartAccountsAsync(
            active.ApiBaseUrl,
            search,
            accountClass,
            IncludeInactiveAccountsCheckBox.IsChecked == true);

        var rows = accounts
            .OrderBy(account => account.Code, StringComparer.Ordinal)
            .Select(ToAccountRow)
            .ToArray();

        AccountsDataGrid.ItemsSource = rows;

        AccountsCountTextBlock.Text = rows.Length switch
        {
            0 => "Aucun compte.",
            1 => "1 compte.",
            _ => $"{rows.Length.ToString(CultureInfo.CurrentCulture)} comptes."
        };

        // La selection precedente disparait avec la nouvelle liste : le formulaire
        // repart d'une creation plutot que de pretendre modifier une ligne absente.
        ResetAccountForm();
        UpdateActionState();
    }

    private static ChartAccountRowView ToAccountRow(ChartAccountResponse account) => new(
        account.Code,
        account.Label,
        account.AccountClassLabel is { Length: > 0 } label
            ? $"{account.AccountClass.ToString(CultureInfo.CurrentCulture)} — {label}"
            : account.AccountClass.ToString(CultureInfo.CurrentCulture),
        KindLabel(account.Kind),
        account.Kind,
        account.IsActive,
        account.UpdatedBy ?? account.CreatedBy);

    private void AccountsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AccountsDataGrid.SelectedItem is not ChartAccountRowView selected)
        {
            UpdateActionState();
            return;
        }

        editingAccountCode = selected.Code;
        AccountFormTitleTextBlock.Text = $"Modifier le compte {selected.Code}";
        AccountCodeTextBox.Text = selected.Code;
        AccountCodeTextBox.IsEnabled = false;
        AccountLabelTextBox.Text = selected.Label;
        AccountKindComboBox.SelectedItem = KindOptions.FirstOrDefault(option => option.Value == selected.Kind);
        SaveAccountButton.Content = "Enregistrer";

        UpdateActionState();
    }

    private void NewAccountButton_Click(object sender, RoutedEventArgs e)
    {
        AccountsDataGrid.SelectedItem = null;
        ResetAccountForm();
        UpdateActionState();
    }

    private void AccountCodeTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // La liste des natures est declaree apres le champ de code dans le XAML :
        // l'evenement peut se declencher avant qu'elle ne soit construite.
        if (AccountKindComboBox is null)
        {
            return;
        }

        ApplyKindOptionsForCode();
    }

    /// <summary>
    /// Restreint les natures proposees a celles qu'admet la classe du code en cours
    /// de saisie : le serveur applique la table classe/nature du domaine
    /// (AccountClassCatalog), et proposer une nature qu'il refusera reviendrait a
    /// promettre une regle differente de la sienne. La table est REFERENCEE depuis
    /// le domaine, jamais recopiee ici. Tant que le code ne designe aucune classe
    /// lisible, les cinq natures restent proposees.
    /// </summary>
    private void ApplyKindOptionsForCode()
    {
        var allowed = AllowedKindsForCode(AccountCodeTextBox.Text);

        var options = KindOptions
            .Where(option => allowed.Contains(option.Value))
            .ToArray();

        if (options.Length == 0)
        {
            options = KindOptions;
        }

        var previous = (AccountKindComboBox.SelectedItem as AccountKindOption)?.Value;

        AccountKindComboBox.ItemsSource = options;
        AccountKindComboBox.SelectedItem =
            options.FirstOrDefault(option => option.Value == previous) ?? options[0];
    }

    private static IReadOnlyCollection<AccountKind> AllowedKindsForCode(string codeText)
    {
        var trimmed = codeText.Trim();

        if (trimmed.Length == 0 || !char.IsAsciiDigit(trimmed[0]))
        {
            return AllKinds;
        }

        // La classe SCF est le premier chiffre du code - la meme derivation que
        // ChartAccount.ExtractAccountClass.
        return AccountClassCatalog.Find(trimmed[0] - '0')?.AllowedKinds ?? AllKinds;
    }

    private void ResetAccountForm()
    {
        editingAccountCode = null;
        AccountFormTitleTextBlock.Text = "Nouveau compte";
        AccountCodeTextBox.Text = string.Empty;
        AccountCodeTextBox.IsEnabled = true;
        AccountLabelTextBox.Text = string.Empty;
        AccountKindComboBox.SelectedIndex = 0;
        SaveAccountButton.Content = "Créer";
    }

    private async void SaveAccountButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            var label = AccountLabelTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(label))
            {
                active.SetStatus("Le libellé du compte est obligatoire.", isError: true);
                return;
            }

            if (AccountKindComboBox.SelectedItem is not AccountKindOption kind)
            {
                active.SetStatus("Sélectionnez la nature du compte.", isError: true);
                return;
            }

            if (editingAccountCode is { } code)
            {
                await active.ApiClient.UpdateChartAccountAsync(
                    active.ApiBaseUrl,
                    code,
                    new UpdateChartAccountRequest(label, kind.Value));

                active.SetStatus($"Compte {code} mis à jour.");
            }
            else
            {
                var newCode = AccountCodeTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(newCode))
                {
                    active.SetStatus("Le code du compte est obligatoire.", isError: true);
                    return;
                }

                await active.ApiClient.CreateChartAccountAsync(
                    active.ApiBaseUrl,
                    new CreateChartAccountRequest(newCode, label, kind.Value));

                active.SetStatus($"Compte {newCode} créé.");
            }

            await ReloadAccountsAsync(active);
        });
    }

    private async void ActivateAccountButton_Click(object sender, RoutedEventArgs e)
    {
        await SetSelectedAccountActiveAsync(isActive: true);
    }

    private async void DeactivateAccountButton_Click(object sender, RoutedEventArgs e)
    {
        await SetSelectedAccountActiveAsync(isActive: false);
    }

    private async Task SetSelectedAccountActiveAsync(bool isActive)
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        if (AccountsDataGrid.SelectedItem is not ChartAccountRowView selected)
        {
            active.SetStatus("Sélectionnez un compte.", isError: true);
            return;
        }

        await active.RunAsync(async () =>
        {
            await active.ApiClient.SetChartAccountActiveAsync(active.ApiBaseUrl, selected.Code, isActive);
            await ReloadAccountsAsync(active);

            active.SetStatus(isActive
                ? $"Compte {selected.Code} réactivé."
                : $"Compte {selected.Code} désactivé.");
        });
    }

    // ================================== Journaux ==================================

    private async void CreateJournalButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        var code = JournalCodeTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            active.SetStatus("Le code du journal est obligatoire.", isError: true);
            JournalCodeTextBox.Focus();
            return;
        }

        var label = JournalLabelTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(label))
        {
            active.SetStatus("Le libellé du journal est obligatoire.", isError: true);
            JournalLabelTextBox.Focus();
            return;
        }

        await active.RunAsync(async () =>
        {
            var created = await active.ApiClient.CreateAccountingJournalAsync(
                active.ApiBaseUrl,
                new CreateAccountingJournalRequest(code, label));

            JournalCodeTextBox.Text = string.Empty;
            JournalLabelTextBox.Text = string.Empty;

            // Les listes du sous-onglet Ecritures proposent aussitot le nouveau
            // journal : sans ce rechargement, il faudrait "Tout actualiser" pour
            // pouvoir y saisir la premiere ecriture.
            await ReloadJournalsAsync(active);

            active.SetStatus($"Journal {created.Code} — {created.Label} créé. Il est proposé à la saisie des écritures.");
        });
    }

    // ================================= Ecritures =================================

    private void ResetJournalOptions()
    {
        EntryJournalComboBox.ItemsSource = new[] { new JournalOption(null, AllJournalsLabel) };
        EntryJournalComboBox.SelectedIndex = 0;

        NewEntryJournalComboBox.ItemsSource = new[] { new JournalOption(null, ChooseJournalLabel) };
        NewEntryJournalComboBox.SelectedIndex = 0;
    }

    private async Task ReloadJournalsAsync(ModuleViewContext active)
    {
        // includeInactive: false - un journal desactive ne doit pas etre proposable
        // pour une saisie, et le filtre de recherche suit la meme liste.
        var journals = await active.ApiClient.GetAccountingJournalsAsync(active.ApiBaseUrl, includeInactive: false);

        var ordered = journals
            .OrderBy(journal => journal.Code, StringComparer.Ordinal)
            .Select(journal => new JournalOption(journal.Code, $"{journal.Code} — {journal.Label}"))
            .ToArray();

        var previousFilter = (EntryJournalComboBox.SelectedItem as JournalOption)?.Code;
        var previousNew = (NewEntryJournalComboBox.SelectedItem as JournalOption)?.Code;

        var filterOptions = new List<JournalOption> { new(null, AllJournalsLabel) };
        filterOptions.AddRange(ordered);
        EntryJournalComboBox.ItemsSource = filterOptions;
        EntryJournalComboBox.SelectedItem =
            filterOptions.FirstOrDefault(option => option.Code == previousFilter) ?? filterOptions[0];

        var newOptions = new List<JournalOption> { new(null, ChooseJournalLabel) };
        newOptions.AddRange(ordered);
        NewEntryJournalComboBox.ItemsSource = newOptions;
        NewEntryJournalComboBox.SelectedItem =
            newOptions.FirstOrDefault(option => option.Code == previousNew) ?? newOptions[0];
    }

    private async void RefreshEntriesButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshEntriesAsync();
    }

    private async void EntryAccountTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await RefreshEntriesAsync();
    }

    private async Task RefreshEntriesAsync()
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            if (await ReloadEntriesAsync(active))
            {
                active.SetStatus("Écritures actualisées.");
            }
        });
    }

    /// <summary>
    /// Recharge la liste des ecritures. Renvoie false quand le rechargement est
    /// ABANDONNE (plage de dates invalide, message d'erreur deja affiche) : chaque
    /// appelant doit le savoir pour ne pas conclure par un message de succes qui
    /// contredirait l'ecran.
    /// </summary>
    private async Task<bool> ReloadEntriesAsync(ModuleViewContext active)
    {
        var from = SelectedDate(EntryFromDatePicker);
        var to = SelectedDate(EntryToDatePicker);

        if (from is { } start && to is { } end && start > end)
        {
            active.SetStatus("La date de début est postérieure à la date de fin.", isError: true);
            return false;
        }

        var entries = await active.ApiClient.GetJournalEntriesAsync(
            active.ApiBaseUrl,
            from,
            to,
            (EntryJournalComboBox.SelectedItem as JournalOption)?.Code,
            (EntryStatusComboBox.SelectedItem as EntryStatusOption)?.Value,
            string.IsNullOrWhiteSpace(EntryAccountTextBox.Text) ? null : EntryAccountTextBox.Text.Trim());

        var rows = entries
            .OrderByDescending(entry => entry.EntryDate)
            .ThenBy(entry => entry.JournalCode, StringComparer.Ordinal)
            .Select(entry => new JournalEntryRowView(entry))
            .ToArray();

        EntriesDataGrid.ItemsSource = rows;

        EntriesCountTextBlock.Text = rows.Length switch
        {
            0 => "Aucune écriture.",
            1 => "1 écriture.",
            _ => $"{rows.Length.ToString(CultureInfo.CurrentCulture)} écritures."
        };

        ShowEntryDetail(null);
        UpdateActionState();

        return true;
    }

    private void EntriesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ShowEntryDetail((EntriesDataGrid.SelectedItem as JournalEntryRowView)?.Source);
        UpdateActionState();
    }

    private void ShowEntryDetail(JournalEntryResponse? entry)
    {
        selectedEntry = entry;

        if (entry is null)
        {
            EntryLinesDataGrid.ItemsSource = null;
            EntryDetailTextBlock.Text = "Sélectionnez une écriture pour en voir les lignes.";
            return;
        }

        EntryLinesDataGrid.ItemsSource = entry.Lines
            .OrderBy(line => line.LineNumber)
            .ToArray();

        var balanceNote = entry.IsBalanced
            ? "équilibrée"
            : $"déséquilibrée de {FormatAmount(Math.Abs(entry.TotalDebit - entry.TotalCredit))}";

        var lifecycleNote = entry.Status switch
        {
            EntryStatus.Posted when entry.ReversedByEntryId is not null =>
                " Elle a déjà été extournée : l'écriture inverse existe et la corrige.",
            EntryStatus.Posted =>
                " Comptabilisée, donc immuable : la seule correction possible est une extourne.",
            EntryStatus.Cancelled =>
                " Brouillon abandonné : il n'est jamais entré dans les livres.",
            _ => " Brouillon : modifiable et absent de la balance tant qu'il n'est pas comptabilisé."
        };

        EntryDetailTextBlock.Text =
            $"{entry.JournalCode} du {FormatDate(entry.EntryDate)} — {entry.Label} · " +
            $"débit {FormatAmount(entry.TotalDebit)}, crédit {FormatAmount(entry.TotalCredit)} ({balanceNote})." +
            lifecycleNote;
    }

    private async void PostEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        if (selectedEntry is not { } entry)
        {
            active.SetStatus("Sélectionnez une écriture à comptabiliser.", isError: true);
            return;
        }

        await active.RunAsync(async () =>
        {
            await active.ApiClient.PostJournalEntryAsync(active.ApiBaseUrl, entry.Id);
            var reloaded = await ReloadEntriesAsync(active);

            active.SetStatus(WithReloadNote("Écriture comptabilisée : elle est désormais immuable.", reloaded));
        });
    }

    private async void ReverseEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        if (selectedEntry is not { } entry)
        {
            active.SetStatus("Sélectionnez une écriture comptabilisée à extourner.", isError: true);
            return;
        }

        await active.RunAsync(async () =>
        {
            // Ni date ni reference imposees : le serveur les derive de l'ecriture
            // d'origine, ce qui garde les deux ecritures lisibles cote a cote.
            var reversal = await active.ApiClient.ReverseJournalEntryAsync(
                active.ApiBaseUrl,
                entry.Id,
                new ReverseJournalEntryRequest(null, null));

            var reloaded = await ReloadEntriesAsync(active);

            active.SetStatus(WithReloadNote(
                $"Extourne créée le {FormatDate(reversal.EntryDate)}. L'écriture d'origine reste comptabilisée.",
                reloaded));
        });
    }

    private async void CancelEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        if (selectedEntry is not { } entry)
        {
            active.SetStatus("Sélectionnez un brouillon à abandonner.", isError: true);
            return;
        }

        var reason = CancelEntryReasonTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(reason))
        {
            active.SetStatus("Le motif d'abandon est obligatoire.", isError: true);
            return;
        }

        await active.RunAsync(async () =>
        {
            await active.ApiClient.CancelJournalEntryAsync(
                active.ApiBaseUrl,
                entry.Id,
                new CancelJournalEntryRequest(reason));

            CancelEntryReasonTextBox.Text = string.Empty;
            var reloaded = await ReloadEntriesAsync(active);

            active.SetStatus(WithReloadNote("Brouillon abandonné.", reloaded));
        });
    }

    // ---------------------------- Nouveau brouillon ----------------------------

    private void AddEntryLineButton_Click(object sender, RoutedEventArgs e)
    {
        AddNewEntryLine();
    }

    private void RemoveEntryLineButton_Click(object sender, RoutedEventArgs e)
    {
        if (NewEntryLinesDataGrid.SelectedItem is not JournalEntryLineEditorRow row)
        {
            context?.SetStatus("Sélectionnez la ligne à retirer.", isError: true);
            return;
        }

        row.PropertyChanged -= NewEntryLine_PropertyChanged;
        newEntryLines.Remove(row);

        // Toujours au moins une ligne a l'ecran : une grille vide ne dit pas comment
        // recommencer, alors qu'une ligne vierge, si.
        if (newEntryLines.Count == 0)
        {
            AddNewEntryLine();
        }

        RefreshNewEntryBalance();
    }

    private void ResetNewEntryButton_Click(object sender, RoutedEventArgs e)
    {
        ResetNewEntryForm();
    }

    private void ResetNewEntryForm()
    {
        foreach (var row in newEntryLines)
        {
            row.PropertyChanged -= NewEntryLine_PropertyChanged;
        }

        newEntryLines.Clear();

        NewEntryLabelTextBox.Text = string.Empty;
        NewEntryReferenceTextBox.Text = string.Empty;
        NewEntryDatePicker.SelectedDate = DateTime.Today;

        if (NewEntryJournalComboBox.Items.Count > 0)
        {
            NewEntryJournalComboBox.SelectedIndex = 0;
        }

        // Deux lignes par defaut : une ecriture comptable en compte toujours au moins
        // deux, une au debit et une au credit.
        AddNewEntryLine();
        AddNewEntryLine();

        RefreshNewEntryBalance();
    }

    private void AddNewEntryLine()
    {
        var row = new JournalEntryLineEditorRow();
        row.PropertyChanged += NewEntryLine_PropertyChanged;
        newEntryLines.Add(row);

        RefreshNewEntryBalance();
    }

    private void NewEntryLine_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshNewEntryBalance();
    }

    // Totaux du brouillon en cours de saisie : ils disent, avant tout appel reseau,
    // si l'ecriture pourra etre comptabilisee. Le brouillon reste creable meme
    // desequilibre - c'est la comptabilisation qui exige l'equilibre.
    private void RefreshNewEntryBalance()
    {
        var debit = 0m;
        var credit = 0m;
        var hasUnreadableAmount = false;

        foreach (var row in newEntryLines)
        {
            if (row.TryReadDebit(out var lineDebit))
            {
                debit += lineDebit;
            }
            else
            {
                hasUnreadableAmount = true;
            }

            if (row.TryReadCredit(out var lineCredit))
            {
                credit += lineCredit;
            }
            else
            {
                hasUnreadableAmount = true;
            }
        }

        if (hasUnreadableAmount)
        {
            NewEntryBalanceTextBlock.Text = "Montant illisible";
            NewEntryBalanceTextBlock.Foreground = (Brush)FindResource("DangerBrush");
            return;
        }

        var difference = debit - credit;

        NewEntryBalanceTextBlock.Text =
            $"Débit {FormatAmount(debit)} · Crédit {FormatAmount(credit)}" +
            (difference == 0m ? " · équilibré" : $" · écart {FormatAmount(Math.Abs(difference))}");

        NewEntryBalanceTextBlock.Foreground = (Brush)FindResource(
            difference == 0m ? "TextPrimaryBrush" : "DangerBrush");
    }

    private async void CreateEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        if (NewEntryDatePicker.SelectedDate is not DateTime entryDate)
        {
            active.SetStatus("La date de l'écriture est obligatoire.", isError: true);
            return;
        }

        if (NewEntryJournalComboBox.SelectedItem is not JournalOption { Code: { } journalCode })
        {
            active.SetStatus("Sélectionnez le journal de l'écriture.", isError: true);
            return;
        }

        var label = NewEntryLabelTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(label))
        {
            active.SetStatus("Le libellé de l'écriture est obligatoire.", isError: true);
            return;
        }

        var lines = new List<JournalEntryLineRequest>();

        foreach (var row in newEntryLines)
        {
            // Une ligne entierement vide est un reste du gabarit, pas une omission :
            // on l'ignore au lieu de la refuser.
            if (row.IsEmpty)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.AccountCode))
            {
                active.SetStatus("Chaque ligne renseignée doit porter un compte.", isError: true);
                return;
            }

            if (!row.TryReadDebit(out var debit) || !row.TryReadCredit(out var credit))
            {
                active.SetStatus($"Montant illisible sur la ligne du compte {row.AccountCode.Trim()}.", isError: true);
                return;
            }

            if (debit < 0m || credit < 0m)
            {
                active.SetStatus("Un montant de ligne ne peut pas être négatif.", isError: true);
                return;
            }

            lines.Add(new JournalEntryLineRequest(
                row.AccountCode.Trim(),
                string.IsNullOrWhiteSpace(row.Label) ? label : row.Label.Trim(),
                debit,
                credit));
        }

        if (lines.Count == 0)
        {
            active.SetStatus("Saisissez au moins une ligne.", isError: true);
            return;
        }

        await active.RunAsync(async () =>
        {
            var created = await active.ApiClient.CreateJournalEntryAsync(
                active.ApiBaseUrl,
                new CreateJournalEntryRequest(
                    DateOnly.FromDateTime(entryDate),
                    journalCode,
                    label,
                    string.IsNullOrWhiteSpace(NewEntryReferenceTextBox.Text) ? null : NewEntryReferenceTextBox.Text.Trim(),
                    lines));

            ResetNewEntryForm();
            var reloaded = await ReloadEntriesAsync(active);

            active.SetStatus(WithReloadNote(
                created.IsBalanced
                    ? "Brouillon créé et équilibré : il peut être comptabilisé."
                    : "Brouillon créé. Il devra être équilibré avant d'être comptabilisé.",
                reloaded));
        });
    }

    // ============================== Balance generale ==============================

    private async void RefreshBalanceButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        var from = SelectedDate(BalanceFromDatePicker);
        var to = SelectedDate(BalanceToDatePicker);

        if (from is { } start && to is { } end && start > end)
        {
            active.SetStatus("La date de début est postérieure à la date de fin.", isError: true);
            return;
        }

        await active.RunAsync(async () =>
        {
            var balance = await active.ApiClient.GetTrialBalanceAsync(active.ApiBaseUrl, from, to);

            TrialBalanceDataGrid.ItemsSource = balance.Rows
                .OrderBy(row => row.AccountCode, StringComparer.Ordinal)
                .Select(ToTrialBalanceRow)
                .ToArray();

            BalanceCountTextBlock.Text = balance.AccountCount switch
            {
                0 => "Aucun compte mouvementé.",
                1 => "1 compte mouvementé.",
                _ => $"{balance.AccountCount.ToString(CultureInfo.CurrentCulture)} comptes mouvementés."
            };

            BalancePeriodTextBlock.Text = (balance.From, balance.To) switch
            {
                (null, null) => "Depuis l'origine, sans borne de période.",
                ({ } start2, null) => $"À partir du {FormatDate(start2)}.",
                (null, { } end2) => $"Jusqu'au {FormatDate(end2)} inclus.",
                ({ } start2, { } end2) => $"Du {FormatDate(start2)} au {FormatDate(end2)}, bornes incluses."
            };

            BalanceScopeTextBlock.Text = balance.PostedEntriesOnly
                ? "Écritures comptabilisées uniquement, comme l'indique le serveur."
                : "Périmètre annoncé par le serveur : les brouillons sont inclus.";

            BalanceTotalDebitTextBlock.Text = FormatAmount(balance.TotalDebit);
            BalanceTotalCreditTextBlock.Text = FormatAmount(balance.TotalCredit);
            BalanceDifferenceTextBlock.Text = FormatAmount(balance.Balance);

            // Un ecart non nul sur une balance generale est une anomalie : il se voit.
            BalanceDifferenceTextBlock.Foreground = (Brush)FindResource(
                balance.Balance == 0m ? "TextPrimaryBrush" : "DangerBrush");

            active.SetStatus("Balance générale actualisée.");
        });
    }

    private static TrialBalanceRowView ToTrialBalanceRow(TrialBalanceRow row) => new(
        row.AccountCode,
        row.AccountLabel,
        row.AccountClass?.ToString(CultureInfo.CurrentCulture),
        row.Kind is { } kind ? KindLabel(kind) : null,
        row.TotalDebit,
        row.TotalCredit,
        row.Balance);

    // ================================ Etat des actions ================================

    /// <summary>
    /// Aligne les boutons sur les droits du profil ET sur l'etat de ce qui est
    /// selectionne. Les deux raisons de griser un bouton sont distinctes, et
    /// l'info-bulle dit toujours laquelle s'applique : une action interdite au profil
    /// n'est pas la meme chose qu'une action sans objet sur la ligne choisie.
    /// Le serveur reste la seule autorite en matiere de droits.
    /// </summary>
    private void UpdateActionState()
    {
        var selectedAccount = AccountsDataGrid.SelectedItem as ChartAccountRowView;

        ApplyButtonState(SaveAccountButton, canWrite, WritePermissionHint);
        ApplyButtonState(
            ActivateAccountButton,
            canWrite && selectedAccount is { IsActive: false },
            canWrite ? "Sélectionnez un compte désactivé." : WritePermissionHint);
        ApplyButtonState(
            DeactivateAccountButton,
            canWrite && selectedAccount is { IsActive: true },
            canWrite ? "Sélectionnez un compte actif." : WritePermissionHint);

        ApplyButtonState(CreateJournalButton, canWrite, WritePermissionHint);

        var isDraft = selectedEntry?.Status == EntryStatus.Draft;
        var isPosted = selectedEntry?.Status == EntryStatus.Posted;
        var isReversed = selectedEntry?.ReversedByEntryId is not null;

        ApplyButtonState(CreateEntryButton, canWrite, WritePermissionHint);
        ApplyButtonState(AddEntryLineButton, canWrite, WritePermissionHint);
        ApplyButtonState(RemoveEntryLineButton, canWrite, WritePermissionHint);

        ApplyButtonState(
            PostEntryButton,
            canPost && isDraft,
            canPost ? "Seul un brouillon se comptabilise." : PostPermissionHint);

        ApplyButtonState(
            ReverseEntryButton,
            canPost && isPosted && !isReversed,
            canPost
                ? "Seule une écriture comptabilisée et non encore extournée s'extourne."
                : PostPermissionHint);

        ApplyButtonState(
            CancelEntryButton,
            canWrite && isDraft,
            canWrite ? "Seul un brouillon s'abandonne." : WritePermissionHint);
    }

    private void ApplyButtonState(Button button, bool isEnabled, string disabledHint)
    {
        if (!originalToolTips.ContainsKey(button))
        {
            originalToolTips[button] = button.ToolTip;
        }

        button.IsEnabled = isEnabled;
        button.ToolTip = isEnabled ? originalToolTips[button] : disabledHint;
    }

    private async void RefreshFiscalYearsButton_Click(object sender, RoutedEventArgs e) => await RefreshFiscalYearsAsync();
    private async Task RefreshFiscalYearsAsync()
    {
        if (context is not { } active) return;
        await active.RunAsync(async () => { FiscalYearsDataGrid.ItemsSource = await active.ApiClient.GetFiscalYearsAsync(active.ApiBaseUrl); active.SetStatus("Exercices actualisés."); });
    }
    private async void CreateFiscalYearButton_Click(object sender,RoutedEventArgs e)
    {
        if(context is not { } active || SelectedDate(FiscalYearStartDatePicker) is not { } start || SelectedDate(FiscalYearEndDatePicker) is not { } end)return;
        await active.RunAsync(async()=>{await active.ApiClient.CreateFiscalYearAsync(active.ApiBaseUrl,new CreateFiscalYearRequest(FiscalYearCodeTextBox.Text,start,end));await RefreshFiscalYearsAsync();active.SetStatus("Exercice et périodes créés.");});
    }
    private async void FiscalYearsDataGrid_SelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(context is not { } active || FiscalYearsDataGrid.SelectedItem is not FiscalYearResponse year)return;
        await active.RunAsync(async()=>AccountingPeriodsDataGrid.ItemsSource=await active.ApiClient.GetAccountingPeriodsAsync(active.ApiBaseUrl,year.Id));
    }
    private async void ClosePeriodButton_Click(object sender,RoutedEventArgs e)
    {
        if(context is not { } active || AccountingPeriodsDataGrid.SelectedItem is not AccountingPeriodResponse period)return;
        await active.RunAsync(async()=>{await active.ApiClient.CloseAccountingPeriodAsync(active.ApiBaseUrl,period.Id);AccountingPeriodsDataGrid.ItemsSource=await active.ApiClient.GetAccountingPeriodsAsync(active.ApiBaseUrl,period.FiscalYearId);active.SetStatus("Période clôturée.");});
    }
    private async void CreatePartyButton_Click(object sender,RoutedEventArgs e)
    {
        if(context is not { } active || PartyKindComboBox.SelectedItem is not PartyKind kind)return;
        await active.RunAsync(async()=>{await active.ApiClient.CreateAccountingPartyAsync(active.ApiBaseUrl,new CreatePartyRequest(PartyCodeTextBox.Text,PartyNameTextBox.Text,kind));AccountingPartiesDataGrid.ItemsSource=await active.ApiClient.GetAccountingPartiesAsync(active.ApiBaseUrl);active.SetStatus("Tiers créé.");});
    }
    private async void RefreshAuxiliaryBalanceButton_Click(object sender,RoutedEventArgs e)
    {
        if(context is not { } active)return;
        await active.RunAsync(async()=>{AccountingPartiesDataGrid.ItemsSource=await active.ApiClient.GetAccountingPartiesAsync(active.ApiBaseUrl);AuxiliaryBalanceDataGrid.ItemsSource=await active.ApiClient.GetAuxiliaryBalanceAsync(active.ApiBaseUrl,SelectedDate(BalanceFromDatePicker),SelectedDate(BalanceToDatePicker),null);active.SetStatus("Balance auxiliaire actualisée.");});
    }
    private async void RefreshGeneralLedgerButton_Click(object sender,RoutedEventArgs e)
    {
        if(context is not { } active || string.IsNullOrWhiteSpace(LedgerAccountCodeTextBox.Text))return;
        await active.RunAsync(async()=>{GeneralLedgerDataGrid.ItemsSource=await active.ApiClient.GetGeneralLedgerAsync(active.ApiBaseUrl,LedgerAccountCodeTextBox.Text.Trim(),SelectedDate(LedgerFromDatePicker),SelectedDate(LedgerToDatePicker));active.SetStatus("Grand livre actualisé.");});
    }
    private async void ReconcileButton_Click(object sender,RoutedEventArgs e)
    {
        if(context is not { } active || !Guid.TryParse(ReconcilePartyIdTextBox.Text,out var partyId) || !Guid.TryParse(ReconcileDebitLineIdTextBox.Text,out var debitId) || !Guid.TryParse(ReconcileCreditLineIdTextBox.Text,out var creditId) || !decimal.TryParse(ReconcileAmountTextBox.Text,NumberStyles.Number,CultureInfo.CurrentCulture,out var amount))return;
        await active.RunAsync(async()=>{var request=new CreateReconciliationRequest(ReconcileCodeTextBox.Text,partyId,[new(debitId,amount)],[new(creditId,amount)]);await active.ApiClient.CreateReconciliationAsync(active.ApiBaseUrl,request);active.SetStatus("Lettrage enregistré.");});
    }

    // ==================================== Formats ====================================

    private static DateOnly? SelectedDate(DatePicker picker) =>
        picker.SelectedDate is { } date ? DateOnly.FromDateTime(date) : null;

    /// <summary>
    /// Complete un message de succes d'action quand le rechargement de la liste qui
    /// a suivi a ete abandonne : l'action a bien eu lieu, mais l'ecran ne la montre
    /// pas encore, et le message final doit dire les deux.
    /// </summary>
    private static string WithReloadNote(string message, bool reloaded) =>
        reloaded
            ? message
            : message + " La liste des écritures n'a pas pu être actualisée : corrigez la période du filtre, puis actualisez.";

    private static string FormatAmount(decimal value) => value.ToString("N2", CultureInfo.CurrentCulture);

    private static string FormatDate(DateOnly value) =>
        value.ToDateTime(TimeOnly.MinValue).ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);

    private static string KindLabel(AccountKind kind) =>
        KindOptions.FirstOrDefault(option => option.Value == kind)?.Label ?? kind.ToString();
}

// ================================ Options de listes ================================

public sealed record AccountClassOption(int? Value, string Label);

public sealed record AccountKindOption(AccountKind Value, string Label);

public sealed record JournalOption(string? Code, string Label);

public sealed record EntryStatusOption(EntryStatus? Value, string Label);

// ================================ Lignes affichees ================================

/// <summary>
/// Ligne du plan comptable telle qu'elle s'affiche : classe et nature deja
/// traduites, et la nature d'origine conservee pour re-selectionner la bonne entree
/// du formulaire sans retraduire un libelle.
/// </summary>
public sealed record ChartAccountRowView(
    string Code,
    string Label,
    string AccountClassLabel,
    string KindLabel,
    AccountKind Kind,
    bool IsActive,
    string UpdatedBy);

/// <summary>
/// Ligne de la liste des ecritures. Elle porte la reponse complete du serveur
/// (<see cref="Source"/>) : le detail affiche sous la grille est celui de l'ecriture
/// selectionnee, sans second appel reseau.
/// </summary>
public sealed class JournalEntryRowView(JournalEntryResponse source)
{
    public JournalEntryResponse Source { get; } = source;

    public DateOnly EntryDate => Source.EntryDate;

    public string JournalCode => Source.JournalCode;

    public string Label => Source.Label;

    public string? Reference => Source.Reference;

    public decimal TotalDebit => Source.TotalDebit;

    public decimal TotalCredit => Source.TotalCredit;

    public bool IsPosted => Source.Status == EntryStatus.Posted;

    public string StatusLabel => Source.Status switch
    {
        EntryStatus.Draft => "Brouillon",
        EntryStatus.Posted when Source.ReversedByEntryId is not null => "Comptabilisée, extournée",
        EntryStatus.Posted => "Comptabilisée",
        EntryStatus.Cancelled => "Abandonnée",
        _ => Source.Status.ToString()
    };
}

public sealed record TrialBalanceRowView(
    string AccountCode,
    string? AccountLabel,
    string? AccountClassLabel,
    string? KindLabel,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Balance);

/// <summary>
/// Une ligne du brouillon en cours de composition.
///
/// Les montants sont conserves EN TEXTE, pas en decimal : une saisie en cours
/// ("1 2", "12,") n'est pas encore un nombre, et la convertir a chaque frappe
/// reecrirait sous les doigts de l'operateur. La conversion n'a lieu qu'au moment
/// de totaliser ou d'envoyer, et ce qui reste illisible est signale plutot que
/// silencieusement ramene a zero.
/// </summary>
public sealed class JournalEntryLineEditorRow : INotifyPropertyChanged
{
    private string accountCode = string.Empty;
    private string label = string.Empty;
    private string debitText = "0";
    private string creditText = "0";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string AccountCode
    {
        get => accountCode;
        set => Set(ref accountCode, value);
    }

    public string Label
    {
        get => label;
        set => Set(ref label, value);
    }

    public string DebitText
    {
        get => debitText;
        set => Set(ref debitText, value);
    }

    public string CreditText
    {
        get => creditText;
        set => Set(ref creditText, value);
    }

    /// <summary>
    /// Ligne restee au gabarit : aucun compte, aucun libelle, et deux montants nuls
    /// ou vides. Elle est ignoree a l'envoi plutot que refusee.
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(AccountCode)
        && string.IsNullOrWhiteSpace(Label)
        && IsZeroOrBlank(DebitText)
        && IsZeroOrBlank(CreditText);

    public bool TryReadDebit(out decimal value) => TryReadAmount(DebitText, out value);

    public bool TryReadCredit(out decimal value) => TryReadAmount(CreditText, out value);

    // Une cellule vide vaut zero : sur une ligne de debit, laisser le credit vide est
    // la facon naturelle de dire "rien au credit".
    private static bool TryReadAmount(string text, out decimal value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = 0m;
            return true;
        }

        var trimmed = text.Trim();

        // La culture de l'utilisateur d'abord, l'invariant ensuite : un montant colle
        // depuis un tableur reste lisible quel que soit son separateur decimal.
        return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            || decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsZeroOrBlank(string text) =>
        TryReadAmount(text, out var value) && value == 0m;

    private void Set(ref string field, string? value, [CallerMemberName] string? propertyName = null)
    {
        var incoming = value ?? string.Empty;

        if (string.Equals(field, incoming, StringComparison.Ordinal))
        {
            return;
        }

        field = incoming;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
