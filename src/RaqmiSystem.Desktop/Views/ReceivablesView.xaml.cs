using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using RaqmiSystem.Application.Receivables;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Receivables;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module Creances et recouvrement : balance agee des factures dues, historique et
/// consignation des relances, et risque porte par un client.
///
/// Le module ne cree aucune donnee financiere. La balance et le risque sont
/// recalcules par le serveur a chaque appel ; la seule ecriture est la trace d'une
/// relance qu'un agent a DEJA effectuee. L'application n'envoie ni courriel, ni
/// SMS, ni courrier : l'ecran le rappelle a l'endroit exact ou l'on pourrait s'y
/// tromper, et le libelle du bouton dit "consigner", pas "envoyer".
///
/// La vue est autonome : elle ne connait ni MainWindow ni les autres modules, et
/// passe par <see cref="ModuleViewContext.RunAsync"/> pour tout appel API (curseur
/// d'attente, barre de progression, traduction des erreurs).
/// </summary>
public partial class ReceivablesView : UserControl
{
    private const string WritePermissionHint =
        "Permission receivables.write requise : votre profil peut consulter les créances, mais pas consigner de relance.";

    private const string AllCustomersLabel = "Tous les clients";

    private const string ChooseCustomerLabel = "Sélectionner un client…";

    private const string AllLevelsLabel = "Tous les niveaux";

    // Libelles francais des enumerations du domaine : seul l'affichage est traduit,
    // la valeur envoyee a l'API reste celle du domaine.
    private static readonly ReminderLevelOption[] LevelOptions =
    [
        new(ReminderLevel.First, "1re relance"),
        new(ReminderLevel.Second, "2e relance"),
        new(ReminderLevel.FormalNotice, "Mise en demeure")
    ];

    private static readonly ReminderChannelOption[] ChannelOptions =
    [
        new(ReminderChannel.Phone, "Téléphone"),
        new(ReminderChannel.Email, "Courriel"),
        new(ReminderChannel.Letter, "Courrier"),
        new(ReminderChannel.InPerson, "En personne")
    ];

    // Info-bulles d'origine des boutons, restaurees des que le droit est present :
    // sans cette memoire, un message pose pour un profil restreint survivrait a la
    // reconnexion d'un profil qui, lui, a le droit d'ecrire.
    private readonly Dictionary<Button, object?> originalToolTips = [];

    /// <summary>
    /// Clients connus de l'ecran, alimentes par les reponses deja recues (balance
    /// agee et relances). Le module ne lit pas le fichier clients : il ne
    /// dependrait pas seulement d'une autre permission (customers.read), il
    /// proposerait aussi des clients sans la moindre creance.
    /// </summary>
    private readonly Dictionary<string, string?> knownCustomers = new(StringComparer.OrdinalIgnoreCase);

    private ModuleViewContext? context;

    // Le profil connecte peut-il consigner une relance ? Memorise a l'ouverture de
    // la session : le bouton est grise sinon, plutot que de laisser l'utilisateur
    // decouvrir un 403 apres avoir saisi tout le formulaire. Le serveur reste la
    // seule autorite en matiere de droits.
    private bool canRecordReminders = true;

    // Code du client dont le risque est affiche, pour rafraichir ce panneau quand
    // une relance vient d'etre consignee sur ce meme client.
    private string? displayedRiskCustomerCode;

    public ReceivablesView()
    {
        InitializeComponent();

        // Les formats {0:N2} et {0:dd/MM/yyyy} des grilles suivent la culture de
        // l'utilisateur, comme les valeurs formatees dans le code-behind.
        var languageTag = CultureInfo.CurrentCulture.IetfLanguageTag;

        if (!string.IsNullOrEmpty(languageTag))
        {
            Language = XmlLanguage.GetLanguage(languageTag);
        }

        ReminderLevelComboBox.ItemsSource = LevelOptions;
        ReminderChannelComboBox.ItemsSource = ChannelOptions;

        var levelFilterOptions = new List<ReminderLevelFilterOption> { new(null, AllLevelsLabel) };
        levelFilterOptions.AddRange(LevelOptions.Select(option => new ReminderLevelFilterOption(option.Value, option.Label)));
        ReminderLevelFilterComboBox.ItemsSource = levelFilterOptions;
        ReminderLevelFilterComboBox.SelectedIndex = 0;

        ApplyDefaultDates();
        ClearReminderForm();
        RebuildCustomerOptions();
        UpdateActionState();
    }

    /// <summary>Memorise le contexte prete par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext moduleViewContext)
    {
        context = moduleViewContext;
        canRecordReminders = moduleViewContext.HasPermission(PermissionCatalog.ReceivablesWrite);

        UpdateActionState();
    }

    /// <summary>
    /// (Re)charge la balance agee et l'historique des relances. Appelee a la
    /// premiere ouverture de l'onglet et par le bouton "Tout actualiser". Le risque
    /// client n'est pas charge ici : il porte sur un client a choisir.
    /// </summary>
    public async Task LoadAsync()
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            // Chaque rechargement peut etre abandonne (date d'arrete absente,
            // periode inversee) en ayant deja affiche son erreur : le message de
            // succes n'a le droit d'apparaitre que si les deux ont vraiment abouti.
            var agingReloaded = await ReloadAgingAsync(active);
            var remindersReloaded = await ReloadRemindersAsync(active);

            if (agingReloaded && remindersReloaded)
            {
                active.SetStatus("Créances et relances actualisées.");
            }
        });
    }

    /// <summary>
    /// Vide toutes les surfaces de la vue : appelee a la deconnexion pour ne jamais
    /// laisser les donnees d'un utilisateur affichees pour le suivant.
    /// </summary>
    public void ResetState()
    {
        knownCustomers.Clear();
        displayedRiskCustomerCode = null;

        AgingDataGrid.ItemsSource = null;
        RemindersDataGrid.ItemsSource = null;

        AgingCountTextBlock.Text = "Balance non chargée.";
        AgingAsOfTextBlock.Text = "Aucun arrêté chargé.";

        // Reecrit a chaque chargement d'apres le filtre client : sans cette remise a
        // zero, le perimetre choisi par le profil precedent resterait affiche au
        // profil suivant sur la meme instance de vue.
        TotalsScopeTextBlock.Text = "Tous les clients de l'arrêté affiché.";
        AgingScopeNoticeTextBlock.Text = "Elles s'afficheront ici après le premier chargement.";
        AgingBasisNoticeTextBlock.Text = string.Empty;
        ReminderCountTextBlock.Text = "Historique non chargé.";
        ReminderInvoiceFilterTextBox.Text = string.Empty;
        ReminderLevelFilterComboBox.SelectedIndex = 0;

        ShowAgingTotals(null);
        ApplyAgingEmptyState(null);
        HideRisk();

        ApplyDefaultDates();
        ClearReminderForm();
        RebuildCustomerOptions();
        UpdateActionState();
    }

    private void ApplyDefaultDates()
    {
        var today = DateTime.Today;

        // La balance est lue tres majoritairement pour aujourd'hui ; l'historique
        // des relances part du premier jour du mois en cours, comme les autres
        // ecrans de periode.
        AsOfDatePicker.SelectedDate = today;
        ReminderFromDatePicker.SelectedDate = new DateTime(today.Year, today.Month, 1);
        ReminderToDatePicker.SelectedDate = today;
        ReminderSentAtPicker.SelectedDate = today;
    }

    // ============================== Balance agee ==============================

    private async void RefreshAllButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    private async void RefreshAgingButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            if (await ReloadAgingAsync(active))
            {
                active.SetStatus("Balance âgée actualisée.");
            }
        });
    }

    /// <summary>
    /// Recharge la balance agee. Renvoie false quand le rechargement est ABANDONNE
    /// (date d'arrete absente, message d'erreur deja affiche) : l'appelant ne doit
    /// alors pas conclure par un message de succes.
    /// </summary>
    private async Task<bool> ReloadAgingAsync(ModuleViewContext active)
    {
        if (AsOfDatePicker.SelectedDate is not DateTime asOfDate)
        {
            active.SetStatus("La date d'arrêté est obligatoire.", isError: true);
            return false;
        }

        var customerCode = SelectedCustomerCode(AgingCustomerComboBox);

        var balance = await active.ApiClient.GetAgingBalanceAsync(
            active.ApiBaseUrl,
            DateOnly.FromDateTime(asOfDate),
            customerCode);

        var rows = balance.Customers.Select(ToAgingRow).ToArray();
        AgingDataGrid.ItemsSource = rows;

        AgingAsOfTextBlock.Text = customerCode is null
            ? $"Situation arrêtée au {FormatDate(balance.AsOfDate)}, tous clients confondus."
            : $"Situation arrêtée au {FormatDate(balance.AsOfDate)}, limitée au client {customerCode}.";

        AgingCountTextBlock.Text = rows.Length switch
        {
            0 => "Aucun client débiteur.",
            1 => $"1 client débiteur — total dû {FormatAmount(balance.Total.Total)}",
            _ => $"{rows.Length.ToString(CultureInfo.CurrentCulture)} clients débiteurs — total dû {FormatAmount(balance.Total.Total)}"
        };

        TotalsScopeTextBlock.Text = customerCode is null
            ? "Tous les clients de l'arrêté affiché."
            : $"Client {customerCode} uniquement.";

        ShowAgingTotals(balance.Total);
        ApplyAgingEmptyState(customerCode);

        // Perimetre et base d'anciennete : le serveur les joint aux chiffres, et ils
        // sont affiches tels quels. Les reformuler ici reviendrait a laisser l'ecran
        // affirmer autre chose que ce que le calcul a fait.
        AgingScopeNoticeTextBlock.Text = balance.Scope;
        AgingBasisNoticeTextBlock.Text = balance.AgingBasis;

        MergeKnownCustomers(balance.Customers.Select(customer => (customer.CustomerCode, customer.CustomerName)));
        RebuildCustomerOptions();

        return true;
    }

    private void ShowAgingTotals(AgingBucketsResponse? totals)
    {
        TotalNotDueTextBlock.Text = FormatAmountOrDash(totals?.NotDue);
        TotalDays1To30TextBlock.Text = FormatAmountOrDash(totals?.Days1To30);
        TotalDays31To60TextBlock.Text = FormatAmountOrDash(totals?.Days31To60);
        TotalDays61To90TextBlock.Text = FormatAmountOrDash(totals?.Days61To90);
        TotalOver90TextBlock.Text = FormatAmountOrDash(totals?.Over90);
        TotalDueTextBlock.Text = FormatAmountOrDash(totals?.Total);
    }

    private void ApplyAgingEmptyState(string? customerCode)
    {
        AgingEmptyTitleTextBlock.Text = customerCode is null
            ? "Aucune créance à cette date"
            : $"Aucune créance pour le client {customerCode}";

        AgingEmptyHintTextBlock.Text = customerCode is null
            ? "Aucune facture émise et non payée à la date d'arrêté. Changez la date, puis actualisez."
            : "Ce client n'a aucune facture émise et non payée à cette date. Retirez le filtre client pour voir toute la balance.";
    }

    // Selectionner une ligne de la balance prepare l'onglet "Risque client" sur ce
    // meme client : rien n'est charge tant que l'utilisateur ne le demande pas.
    private void AgingDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AgingDataGrid.SelectedItem is not AgingRowView selected)
        {
            return;
        }

        SelectCustomer(RiskCustomerComboBox, selected.CustomerCode);
    }

    // ================================ Relances ================================

    private async void RefreshRemindersButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            if (await ReloadRemindersAsync(active))
            {
                active.SetStatus("Historique des relances actualisé.");
            }
        });
    }

    /// <summary>
    /// Recharge l'historique des relances. Renvoie false quand le rechargement est
    /// ABANDONNE (periode inversee, message d'erreur deja affiche) : l'appelant ne
    /// doit alors pas conclure par un message de succes.
    /// </summary>
    private async Task<bool> ReloadRemindersAsync(ModuleViewContext active)
    {
        var from = ToDateOnly(ReminderFromDatePicker.SelectedDate);
        var to = ToDateOnly(ReminderToDatePicker.SelectedDate);

        // Le serveur refuse une periode inversee : autant le dire ici, sans
        // aller-retour.
        if (from.HasValue && to.HasValue && from > to)
        {
            active.SetStatus("La date de début ne peut pas être postérieure à la date de fin.", isError: true);
            return false;
        }

        var invoiceNumber = ReadOptional(ReminderInvoiceFilterTextBox);

        var reminders = await active.ApiClient.GetRemindersAsync(
            active.ApiBaseUrl,
            SelectedCustomerCode(ReminderCustomerFilterComboBox),
            invoiceNumber,
            from,
            to,
            (ReminderLevelFilterComboBox.SelectedItem as ReminderLevelFilterOption)?.Value);

        var rows = reminders
            .OrderByDescending(reminder => reminder.SentAt)
            .ThenByDescending(reminder => reminder.CreatedAt)
            .Select(ToReminderRow)
            .ToArray();

        RemindersDataGrid.ItemsSource = rows;

        ReminderCountTextBlock.Text = rows.Length switch
        {
            0 => "Aucune relance pour ces critères.",
            1 => "1 relance consignée.",
            _ => $"{rows.Length.ToString(CultureInfo.CurrentCulture)} relances consignées."
        };

        MergeKnownCustomers(reminders.Select(reminder => (reminder.CustomerCode, reminder.CustomerName)));
        RebuildCustomerOptions();

        return true;
    }

    private void ReminderLevelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Le bloc d'avertissement est declare apres la liste deroulante dans le
        // XAML : l'evenement peut se declencher avant qu'il ne soit construit.
        if (FormalNoticeHintTextBlock is null)
        {
            return;
        }

        FormalNoticeHintTextBlock.Visibility = ReminderLevelComboBox.SelectedValue is ReminderLevel.FormalNotice
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ClearReminderFormButton_Click(object sender, RoutedEventArgs e)
    {
        ClearReminderForm();
        context?.SetStatus("Formulaire de relance vidé.");
    }

    private async void RecordReminderButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active)
        {
            return;
        }

        var invoiceNumber = ReminderInvoiceNumberTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            active.SetStatus("Le numéro de la facture relancée est obligatoire.", isError: true);
            ReminderInvoiceNumberTextBox.Focus();
            return;
        }

        if (ReminderLevelComboBox.SelectedValue is not ReminderLevel level)
        {
            active.SetStatus("Sélectionnez le niveau de la relance.", isError: true);
            return;
        }

        if (ReminderChannelComboBox.SelectedValue is not ReminderChannel channel)
        {
            active.SetStatus("Sélectionnez le canal par lequel la relance a été effectuée.", isError: true);
            return;
        }

        if (ReminderSentAtPicker.SelectedDate is not DateTime sentAtDate)
        {
            active.SetStatus("La date de la relance est obligatoire.", isError: true);
            return;
        }

        var sentAt = DateOnly.FromDateTime(sentAtDate);

        // Une relance consigne une action deja faite : une date future decrirait
        // quelque chose que personne n'a encore fait. Le serveur applique cette
        // regle sur SA date du jour, obtenue en UTC (ReceivablesService), et elle
        // fait foi : le controle local reprend donc la meme horloge, plutot que
        // DateTime.Today qui, sur un poste en avance sur UTC (UTC+1 apres minuit,
        // par exemple), accepterait une date que le serveur refuserait ensuite.
        // Miroir de la regle serveur, jamais une regle differente (charte, 3.9).
        if (sentAt > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            active.SetStatus(
                "Une relance ne peut pas être datée dans le futur : elle consigne une action déjà effectuée. La date du jour s'apprécie en temps universel, comme sur le serveur.",
                isError: true);
            return;
        }

        var notes = ReadOptional(ReminderNotesTextBox);

        // Acte engageant : la mise en demeure est le dernier degre avant
        // recouvrement contentieux, et cette trace ne se retire pas.
        if (level == ReminderLevel.FormalNotice && !ConfirmFormalNotice(invoiceNumber, sentAt, channel))
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            var created = await active.ApiClient.CreateReminderAsync(
                active.ApiBaseUrl,
                new CreateReminderRequest(invoiceNumber, level, sentAt, channel, notes));

            ClearReminderForm();

            MergeKnownCustomers([(created.CustomerCode, created.CustomerName)]);
            var remindersReloaded = await ReloadRemindersAsync(active);

            // Le panneau de risque affiche des chiffres qui viennent de changer pour
            // ce client : il serait faux de le laisser sur son etat precedent.
            if (string.Equals(displayedRiskCustomerCode, created.CustomerCode, StringComparison.OrdinalIgnoreCase))
            {
                await ReloadRiskAsync(active, created.CustomerCode);
            }

            // La consignation a bien eu lieu meme si le rechargement de l'historique
            // a ete abandonne (periode filtree inversee) : le message final dit les
            // deux faits plutot que de laisser croire que l'ecran est a jour.
            var recordedMessage =
                $"Relance consignée : {DescribeLevel(created.Level)} du {FormatDate(created.SentAt)} sur la facture {created.InvoiceNumber} ({DescribeChannel(created.Channel)}). Aucun envoi n'a été effectué par le système.";

            active.SetStatus(remindersReloaded
                ? recordedMessage
                : recordedMessage + " L'historique n'a pas pu être actualisé : corrigez la période du filtre, puis actualisez.");
        });
    }

    private bool ConfirmFormalNotice(string invoiceNumber, DateOnly sentAt, ReminderChannel channel)
    {
        return Confirm(
            $"Consigner une MISE EN DEMEURE sur la facture {invoiceNumber.ToUpperInvariant()} ?"
            + Environment.NewLine + Environment.NewLine
            + "La mise en demeure est le niveau le plus grave de l'échelle de relance, le dernier avant recouvrement contentieux."
            + Environment.NewLine + Environment.NewLine
            + $"Date déclarée : {FormatDate(sentAt)}"
            + Environment.NewLine
            + $"Canal : {DescribeChannel(channel)}"
            + Environment.NewLine + Environment.NewLine
            + "Cette trace est conservée dans le dossier du client et dans le journal d'audit, et un même niveau ne peut pas être consigné deux fois pour la même facture."
            + Environment.NewLine + Environment.NewLine
            + "Rappel : le système n'envoie rien. Vous déclarez ici une mise en demeure déjà effectuée.",
            "Consigner une mise en demeure");
    }

    private void ClearReminderForm()
    {
        ReminderInvoiceNumberTextBox.Text = string.Empty;
        ReminderNotesTextBox.Text = string.Empty;
        ReminderSentAtPicker.SelectedDate = DateTime.Today;

        // Le niveau et le canal repartent sur la valeur la plus courante et la moins
        // grave : une mise en demeure ne doit jamais etre le choix par defaut.
        ReminderLevelComboBox.SelectedValue = ReminderLevel.First;
        ReminderChannelComboBox.SelectedValue = ReminderChannel.Phone;

        if (FormalNoticeHintTextBlock is not null)
        {
            FormalNoticeHintTextBlock.Visibility = Visibility.Collapsed;
        }
    }

    // ============================== Risque client ==============================

    private async void RefreshRiskButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        var customerCode = SelectedCustomerCode(RiskCustomerComboBox);

        if (customerCode is null)
        {
            active.SetStatus("Sélectionnez le client dont vous voulez examiner le risque.", isError: true);
            return;
        }

        await active.RunAsync(async () =>
        {
            await ReloadRiskAsync(active, customerCode);
            active.SetStatus($"Risque du client {customerCode} affiché.");
        });
    }

    private async Task ReloadRiskAsync(ModuleViewContext active, string customerCode)
    {
        var risk = await active.ApiClient.GetCustomerRiskAsync(active.ApiBaseUrl, customerCode);

        ShowRisk(risk);
    }

    private void ShowRisk(CustomerRiskResponse risk)
    {
        displayedRiskCustomerCode = risk.CustomerCode;

        RiskContentPanel.Visibility = Visibility.Visible;
        RiskEmptyPanel.Visibility = Visibility.Collapsed;

        RiskCustomerTextBlock.Text = $"{risk.CustomerCode} — {risk.CustomerName}";
        RiskAsOfTextBlock.Text = $"Risque arrêté par le serveur au {FormatDate(risk.AsOfDate)}.";

        ApplyCustomerStateBadge(risk.CustomerIsActive);

        RiskOutstandingTotalTextBlock.Text = FormatAmount(risk.OutstandingTotal);
        RiskOutstandingCountTextBlock.Text = risk.OutstandingInvoiceCount switch
        {
            0 => "Aucune",
            1 => "1 facture",
            _ => $"{risk.OutstandingInvoiceCount.ToString(CultureInfo.CurrentCulture)} factures"
        };
        RiskOver90TextBlock.Text = FormatAmount(risk.Buckets.Over90);

        // La facture la plus ancienne n'existe que si le client doit encore quelque
        // chose : sans encours, ces trois champs restent vides plutot que de
        // presenter des tirets comme s'il manquait une donnee.
        if (risk.OldestOutstandingInvoiceNumber is { } oldestNumber && risk.OldestOutstandingInvoiceDate is { } oldestDate)
        {
            RiskOldestInvoiceTextBlock.Text = $"{oldestNumber} du {FormatDate(oldestDate)}";
            RiskOldestAgeTextBlock.Text = DescribeAge(risk.OldestOutstandingInvoiceAgeInDays);
            RiskOldestAmountTextBlock.Text = risk.OldestOutstandingInvoiceAmount is { } amount
                ? FormatAmount(amount)
                : "—";
        }
        else
        {
            RiskOldestInvoiceTextBlock.Text = "Aucune facture émise non payée.";
            RiskOldestAgeTextBlock.Text = "—";
            RiskOldestAmountTextBlock.Text = "—";
        }

        RiskNotDueTextBlock.Text = FormatAmount(risk.Buckets.NotDue);
        RiskDays1To30TextBlock.Text = FormatAmount(risk.Buckets.Days1To30);
        RiskDays31To60TextBlock.Text = FormatAmount(risk.Buckets.Days31To60);
        RiskDays61To90TextBlock.Text = FormatAmount(risk.Buckets.Days61To90);
        RiskBucketOver90TextBlock.Text = FormatAmount(risk.Buckets.Over90);

        RiskReminderCountTextBlock.Text = risk.ReminderCount switch
        {
            0 => "Aucune relance consignée",
            1 => "1 relance",
            _ => $"{risk.ReminderCount.ToString(CultureInfo.CurrentCulture)} relances"
        };

        RiskLastReminderTextBlock.Text = risk.LastReminderLevel is { } lastLevel
            ? $"{DescribeLevel(lastLevel)}{DescribeReminderDate(risk.LastReminderSentAt)}"
            : "Aucune";

        RiskHighestLevelTextBlock.Text = risk.HighestReminderLevel is { } highestLevel
            ? DescribeLevel(highestLevel)
            : "Aucun";

        RiskScopeNoticeTextBlock.Text = risk.Scope;
        RiskBasisNoticeTextBlock.Text = risk.AgingBasis;
    }

    private void HideRisk()
    {
        displayedRiskCustomerCode = null;
        RiskContentPanel.Visibility = Visibility.Collapsed;
        RiskEmptyPanel.Visibility = Visibility.Visible;
        RiskScopeNoticeTextBlock.Text = string.Empty;
        RiskBasisNoticeTextBlock.Text = string.Empty;
    }

    private void ApplyCustomerStateBadge(bool isActive)
    {
        var (background, foreground, label) = isActive
            ? ("StatusValidatedBackgroundBrush", "StatusValidatedForegroundBrush", "Actif")
            : ("StatusDraftBackgroundBrush", "StatusDraftForegroundBrush", "Inactif");

        if (TryFindResource(background) is Brush backgroundBrush)
        {
            RiskCustomerStateBadge.Background = backgroundBrush;
        }

        if (TryFindResource(foreground) is Brush foregroundBrush)
        {
            RiskCustomerStateBadgeText.Foreground = foregroundBrush;
        }

        RiskCustomerStateBadgeText.Text = label;
    }

    // ============================ Droits et actions ============================

    private void UpdateActionState()
    {
        RecordReminderButton.IsEnabled = canRecordReminders;

        ApplyPermissionHint(RecordReminderButton, canRecordReminders, WritePermissionHint);
    }

    // Pose le message d'explication quand le droit manque, et RESTAURE l'info-bulle
    // d'origine du bouton quand il est present : l'affectation doit etre symetrique.
    private void ApplyPermissionHint(Button button, bool allowed, string hint)
    {
        if (!originalToolTips.ContainsKey(button))
        {
            originalToolTips[button] = button.ToolTip;
        }

        button.ToolTip = allowed ? originalToolTips[button] : hint;
    }

    // ============================ Clients de l'ecran ============================

    private void MergeKnownCustomers(IEnumerable<(string Code, string? Name)> customers)
    {
        foreach (var (code, name) in customers)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            // Un nom deja connu n'est jamais remplace par une absence de nom.
            if (!knownCustomers.TryGetValue(code, out var existing) || string.IsNullOrWhiteSpace(existing))
            {
                knownCustomers[code] = name;
            }
        }
    }

    private void RebuildCustomerOptions()
    {
        var customers = knownCustomers
            .Select(pair => new ReceivableCustomerOption(pair.Key, FormatCustomerLabel(pair.Key, pair.Value)))
            .OrderBy(option => option.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        RebindCustomerOptions(AgingCustomerComboBox, AllCustomersLabel, customers);
        RebindCustomerOptions(ReminderCustomerFilterComboBox, AllCustomersLabel, customers);
        RebindCustomerOptions(RiskCustomerComboBox, ChooseCustomerLabel, customers);
    }

    // La selection en cours est conservee quand le code est toujours propose ;
    // sinon la liste retombe sur son entree de tete.
    private static void RebindCustomerOptions(
        ComboBox comboBox,
        string headLabel,
        List<ReceivableCustomerOption> customers)
    {
        var previousCode = (comboBox.SelectedItem as ReceivableCustomerOption)?.Code;

        var options = new List<ReceivableCustomerOption>(customers.Count + 1) { new(null, headLabel) };
        options.AddRange(customers);

        comboBox.ItemsSource = options;
        comboBox.SelectedItem = options.FirstOrDefault(option =>
            string.Equals(option.Code, previousCode, StringComparison.OrdinalIgnoreCase)) ?? options[0];
    }

    private static void SelectCustomer(ComboBox comboBox, string code)
    {
        if (comboBox.ItemsSource is IEnumerable<ReceivableCustomerOption> options)
        {
            var match = options.FirstOrDefault(option =>
                string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                comboBox.SelectedItem = match;
            }
        }
    }

    private static string? SelectedCustomerCode(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ReceivableCustomerOption)?.Code;
    }

    private static string FormatCustomerLabel(string code, string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? code : $"{code} — {name}";
    }

    // ================================= Outils =================================

    private static AgingRowView ToAgingRow(CustomerAgingResponse customer)
    {
        return new AgingRowView(
            customer.CustomerCode,
            FormatCustomerLabel(customer.CustomerCode, customer.CustomerName),
            customer.InvoiceCount,
            customer.OldestInvoiceDate,
            DescribeAge(customer.OldestInvoiceAgeInDays),
            customer.Buckets.NotDue,
            customer.Buckets.Days1To30,
            customer.Buckets.Days31To60,
            customer.Buckets.Days61To90,
            customer.Buckets.Over90,
            customer.Buckets.Total);
    }

    private static ReminderRowView ToReminderRow(ReminderResponse reminder)
    {
        return new ReminderRowView(
            reminder.SentAt,
            FormatCustomerLabel(reminder.CustomerCode, reminder.CustomerName),
            reminder.InvoiceNumber,
            reminder.Level,
            DescribeLevel(reminder.Level),
            DescribeChannel(reminder.Channel),
            reminder.Notes,
            $"le {FormatMoment(reminder.CreatedAt)} par {reminder.CreatedBy}");
    }

    private static string DescribeLevel(ReminderLevel level)
    {
        return LevelOptions.FirstOrDefault(option => option.Value == level)?.Label ?? level.ToString();
    }

    private static string DescribeChannel(ReminderChannel channel)
    {
        return ChannelOptions.FirstOrDefault(option => option.Value == channel)?.Label ?? channel.ToString();
    }

    private static string DescribeReminderDate(DateOnly? sentAt)
    {
        return sentAt is { } value ? $" du {FormatDate(value)}" : string.Empty;
    }

    // L'anciennete est un nombre de jours depuis la date de facture. Zero ou moins
    // signifie une facture datee du jour de l'arrete ou apres : elle n'est pas
    // echue, et parler de "0 jour" laisserait croire a un retard qui commence.
    private static string DescribeAge(int? ageInDays)
    {
        if (ageInDays is not { } age)
        {
            return "—";
        }

        return age switch
        {
            <= 0 => "Non échu",
            1 => "1 jour",
            _ => $"{age.ToString(CultureInfo.CurrentCulture)} jours"
        };
    }

    private bool Confirm(string message, string caption)
    {
        var owner = Window.GetWindow(this);

        var result = owner is null
            ? MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(owner, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    private static string? ReadOptional(TextBox textBox)
    {
        var value = textBox.Text.Trim();

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static DateOnly? ToDateOnly(DateTime? value)
    {
        return value.HasValue ? DateOnly.FromDateTime(value.Value) : null;
    }

    private static string FormatAmount(decimal value)
    {
        return value.ToString("N2", CultureInfo.CurrentCulture);
    }

    private static string FormatAmountOrDash(decimal? value)
    {
        return value is { } amount ? FormatAmount(amount) : "—";
    }

    private static string FormatDate(DateOnly value)
    {
        return value.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);
    }

    private static string FormatMoment(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
    }

    /// <summary>Option de client des listes deroulantes. Code null : entree de tete.</summary>
    private sealed record ReceivableCustomerOption(string? Code, string Label);

    private sealed record ReminderLevelOption(ReminderLevel Value, string Label);

    /// <summary>Niveau du filtre d'historique. Valeur null : tous les niveaux.</summary>
    private sealed record ReminderLevelFilterOption(ReminderLevel? Value, string Label);

    private sealed record ReminderChannelOption(ReminderChannel Value, string Label);

    /// <summary>
    /// Ligne de la balance agee. Les deux indicateurs de tranche nulle servent aux
    /// styles des colonnes 61-90 jours et plus de 90 jours : un montant nul y
    /// repasse en gris, pour que seule une creance reellement agee attire l'oeil.
    /// </summary>
    private sealed record AgingRowView(
        string CustomerCode,
        string CustomerLabel,
        int InvoiceCount,
        DateOnly OldestInvoiceDate,
        string OldestInvoiceAgeLabel,
        decimal NotDue,
        decimal Days1To30,
        decimal Days31To60,
        decimal Days61To90,
        decimal Over90,
        decimal Total)
    {
        public bool Days61To90IsZero => Days61To90 == 0m;

        public bool Over90IsZero => Over90 == 0m;
    }

    private sealed record ReminderRowView(
        DateOnly SentAt,
        string CustomerLabel,
        string InvoiceNumber,
        ReminderLevel Level,
        string LevelLabel,
        string ChannelLabel,
        string? Notes,
        string RecordedLabel);
}
