using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using RaqmiSystem.Application.Approvals;
using RaqmiSystem.Application.Treasury;
using RaqmiSystem.Domain.Approvals;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module Encaissements et tresorerie : encaissements des unites, ordres de
/// paiement et comptes bancaires, repartis sur trois onglets internes.
///
/// Vue autonome : elle ne connait ni MainWindow ni les autres vues, tout passe
/// par le ModuleViewContext recu dans Initialize (client API, URL, message
/// d'etat, execution d'un appel avec curseur d'attente).
/// </summary>
public partial class TreasuryView : UserControl
{
    private const string WritePermissionHint =
        "Permission requise : treasury.write. Votre profil ne peut que consulter la trésorerie.";

    private const string ApprovePermissionHint =
        "Permission requise : treasury.approve. Votre profil ne peut pas approuver un ordre de paiement.";

    private const string OpenApprovalPermissionHint =
        "Permission requise : approvals.write. Votre profil ne peut pas ouvrir une demande de validation.";

    private ModuleViewContext? context;

    // Info-bulles d'origine des boutons d'ecriture, capturees avant toute
    // substitution. Les vues de module survivent a la deconnexion et sont
    // reinitialisees a chaque connexion sur les memes instances : un message
    // "permission requise" pose pour un profil doit donc disparaitre pour le
    // profil suivant, sinon il persiste a tort pour un utilisateur qui a le droit.
    private readonly Dictionary<Button, object?> originalToolTips = [];

    // Encaissement repris dans le formulaire (modification d'un brouillon).
    private Guid? editingReceiptId;

    // Compte bancaire repris dans le formulaire (modification).
    private string? editingBankAccountCode;

    // Code du compte desactive rajoute en tete de la liste de saisie pour reprendre
    // un brouillon qui l'utilisait. Memorise pour pouvoir le retirer des que le
    // formulaire est vide : il ne doit pas rester proposable a une nouvelle saisie,
    // que le serveur refuserait (un encaissement ne peut pas viser un compte inactif).
    private string? inactiveBankAccountOptionCode;

    // Droits du profil connecte, memorises a l'ouverture de la session. Les actions
    // d'ecriture sont grisees quand le droit manque, plutot que de laisser
    // l'utilisateur decouvrir un 403 apres avoir saisi tout un formulaire.
    // Le serveur reste la seule autorite : ceci n'est qu'un confort d'interface.
    private bool canWrite = true;

    // L'approbation d'un ordre de paiement releve d'un droit distinct de l'ecriture
    // (POST /payment-orders/{id}/approve exige treasury.approve) : un profil
    // write-sans-approve voit "Approuver" grise et le reste actif.
    private bool canApprove = true;

    // Ouvrir la demande de validation d'un ordre de paiement appartient au module
    // Workflows & validations : c'est son droit approvals.write qui l'autorise
    // (POST /api/v1/approvals/instances), pas les droits de la tresorerie.
    private bool canOpenApproval = true;

    // Vrai le temps de ResetState : la remise a zero des filtres declenche leurs
    // gestionnaires, qui ne doivent en aucun cas relancer un chargement. Rend le
    // contrat "ResetState vide et ne recharge rien" vrai quel que soit l'ordre
    // d'appel a la deconnexion.
    private bool suspendFilterReload;

    public TreasuryView()
    {
        InitializeComponent();
        InitializeDefaults();
    }

    /// <summary>Memorise le contexte prete par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext moduleViewContext)
    {
        context = moduleViewContext;
        canWrite = moduleViewContext.HasPermission(PermissionCatalog.TreasuryWrite);
        canApprove = moduleViewContext.HasPermission(PermissionCatalog.TreasuryApprove);
        canOpenApproval = moduleViewContext.HasPermission(PermissionCatalog.ApprovalsWrite);

        // Recalcule l'etat des trois familles d'actions : les boutons independants
        // d'une selection (enregistrer un encaissement, creer un ordre, enregistrer
        // un compte) doivent eux aussi refleter les droits des maintenant.
        UpdateReceiptActionState();
        UpdatePaymentOrderActionState();
        UpdateBankAccountActionState();
    }

    /// <summary>
    /// (Re)charge les trois sections du module. Sortie silencieuse tant que la vue
    /// n'a pas de contexte ou que personne n'est connecte.
    /// </summary>
    public async Task LoadAsync()
    {
        if (context is null || !context.ApiClient.IsAuthenticated)
        {
            return;
        }

        await context.RunAsync(LoadEverythingAsync);
    }

    /// <summary>Vide grilles et formulaires (appelee a la deconnexion).</summary>
    public void ResetState()
    {
        // Les filtres remis a zero ci-dessous declenchent leurs gestionnaires :
        // le drapeau garantit qu'aucun d'eux ne relance de chargement, sans
        // dependre de l'ordre dans lequel la deconnexion appelle cette methode.
        suspendFilterReload = true;

        try
        {
            ReceiptsDataGrid.ItemsSource = null;
            PaymentOrdersDataGrid.ItemsSource = null;
            BankAccountsDataGrid.ItemsSource = null;

            inactiveBankAccountOptionCode = null;

            ReceiptUnitComboBox.ItemsSource = null;
            ReceiptFilterUnitComboBox.ItemsSource = null;
            ReceiptBankAccountComboBox.ItemsSource = null;
            OrderBankAccountComboBox.ItemsSource = null;
            OrderFilterBankAccountComboBox.ItemsSource = null;

            ReceiptFilterMethodComboBox.SelectedIndex = 0;
            ReceiptFilterStatusComboBox.SelectedIndex = 0;
            OrderFilterStatusComboBox.SelectedIndex = 0;

            ReceiptCancelReasonTextBox.Text = string.Empty;
            OrderCancelReasonTextBox.Text = string.Empty;
            IncludeInactiveBankAccountsCheckBox.IsChecked = false;

            ResetReceiptForm();
            ResetPaymentOrderForm();
            ResetBankAccountForm();
            ClearReceiptSummary();

            UpdateReceiptActionState();
            UpdatePaymentOrderActionState();
            UpdateBankAccountActionState();
        }
        finally
        {
            suspendFilterReload = false;
        }
    }

    // =============================== Initialisation ===============================

    private void InitializeDefaults()
    {
        var today = DateTime.Today;
        var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);

        ReceiptFromDatePicker.SelectedDate = firstDayOfMonth;
        ReceiptToDatePicker.SelectedDate = today;
        OrderFromDatePicker.SelectedDate = firstDayOfMonth;
        OrderToDatePicker.SelectedDate = today;

        ReceiptMethodComboBox.ItemsSource = BuildMethodOptions(includeAll: false);
        ReceiptFilterMethodComboBox.ItemsSource = BuildMethodOptions(includeAll: true);
        ReceiptFilterMethodComboBox.SelectedIndex = 0;

        ReceiptFilterStatusComboBox.ItemsSource = new[]
        {
            new TreasuryReceiptStatusOption(null, "Tous les statuts"),
            new TreasuryReceiptStatusOption(ReceiptStatus.Draft, "Brouillon"),
            new TreasuryReceiptStatusOption(ReceiptStatus.Confirmed, "Confirmé"),
            new TreasuryReceiptStatusOption(ReceiptStatus.Cancelled, "Annulé")
        };
        ReceiptFilterStatusComboBox.SelectedIndex = 0;

        OrderFilterStatusComboBox.ItemsSource = new[]
        {
            new TreasuryOrderStatusOption(null, "Tous les statuts"),
            new TreasuryOrderStatusOption(PaymentOrderStatus.Draft, "Brouillon"),
            new TreasuryOrderStatusOption(PaymentOrderStatus.Approved, "Approuvé"),
            new TreasuryOrderStatusOption(PaymentOrderStatus.Paid, "Payé"),
            new TreasuryOrderStatusOption(PaymentOrderStatus.Cancelled, "Annulé")
        };
        OrderFilterStatusComboBox.SelectedIndex = 0;

        ResetReceiptForm();
        ResetPaymentOrderForm();
        ResetBankAccountForm();
        ClearReceiptSummary();

        UpdateReceiptActionState();
        UpdatePaymentOrderActionState();
        UpdateBankAccountActionState();
    }

    private static TreasuryMethodOption[] BuildMethodOptions(bool includeAll)
    {
        var methods = Enum.GetValues<PaymentMethod>()
            .Select(method => new TreasuryMethodOption(method, TreasuryLabels.Method(method)));

        return includeAll
            ? new[] { new TreasuryMethodOption(null, "Tous les modes") }.Concat(methods).ToArray()
            : methods.ToArray();
    }

    // ============================== Chargement des donnees =========================

    private async Task LoadEverythingAsync()
    {
        await LoadHotelUnitsAsync();
        await LoadBankAccountsAsync();
        await LoadReceiptsAsync();
        await LoadPaymentOrdersAsync();
    }

    private async Task LoadHotelUnitsAsync()
    {
        var moduleContext = context!;
        var units = await moduleContext.ApiClient.GetHotelUnitsAsync(moduleContext.ApiBaseUrl, includeInactive: false);

        var options = units
            .Where(unit => unit.IsActive)
            .OrderBy(unit => unit.DisplayOrder)
            .ThenBy(unit => unit.Name, StringComparer.CurrentCulture)
            .Select(unit => new TreasuryCodeOption(unit.Code, $"{unit.Code} — {unit.Name}"))
            .ToArray();

        var entryCode = SelectedCode(ReceiptUnitComboBox);
        ReceiptUnitComboBox.ItemsSource = options;
        SelectCode(ReceiptUnitComboBox, entryCode);

        if (ReceiptUnitComboBox.SelectedItem is null && options.Length > 0)
        {
            ReceiptUnitComboBox.SelectedIndex = 0;
        }

        var filterCode = SelectedCode(ReceiptFilterUnitComboBox);
        ReceiptFilterUnitComboBox.ItemsSource = new[] { new TreasuryCodeOption(null, "Toutes les unités") }
            .Concat(options)
            .ToArray();
        SelectCode(ReceiptFilterUnitComboBox, filterCode);
    }

    private async Task LoadBankAccountsAsync()
    {
        var moduleContext = context!;
        var accounts = await moduleContext.ApiClient.GetBankAccountsAsync(
            moduleContext.ApiBaseUrl,
            IncludeInactiveBankAccountsCheckBox.IsChecked == true);

        BankAccountsDataGrid.ItemsSource = accounts
            .OrderBy(account => account.Code, StringComparer.CurrentCulture)
            .ToArray();

        // Seuls les comptes actifs sont proposes a la saisie ; la liste complete
        // reste disponible comme filtre des ordres de paiement.
        var activeOptions = accounts
            .Where(account => account.IsActive)
            .OrderBy(account => account.Code, StringComparer.CurrentCulture)
            .Select(account => new TreasuryCodeOption(account.Code, $"{account.Code} — {account.Label}"))
            .ToArray();

        var receiptCode = SelectedCode(ReceiptBankAccountComboBox);
        ReceiptBankAccountComboBox.ItemsSource = new[] { new TreasuryCodeOption(null, "Aucun (espèces)") }
            .Concat(activeOptions)
            .ToArray();

        // La liste vient d'etre reconstruite sur les seuls comptes actifs : si la
        // saisie en cours porte sur un compte desactive, il est remis en tete plutot
        // que de disparaitre du formulaire sans rien dire.
        inactiveBankAccountOptionCode = null;
        SelectReceiptBankAccount(receiptCode);

        var orderCode = SelectedCode(OrderBankAccountComboBox);
        OrderBankAccountComboBox.ItemsSource = activeOptions;
        SelectCode(OrderBankAccountComboBox, orderCode);

        if (OrderBankAccountComboBox.SelectedItem is null && activeOptions.Length > 0)
        {
            OrderBankAccountComboBox.SelectedIndex = 0;
        }

        var orderFilterCode = SelectedCode(OrderFilterBankAccountComboBox);
        OrderFilterBankAccountComboBox.ItemsSource = new[] { new TreasuryCodeOption(null, "Tous les comptes") }
            .Concat(accounts
                .OrderBy(account => account.Code, StringComparer.CurrentCulture)
                .Select(account => new TreasuryCodeOption(account.Code, $"{account.Code} — {account.Label}")))
            .ToArray();
        SelectCode(OrderFilterBankAccountComboBox, orderFilterCode);

        UpdateBankAccountActionState();
    }

    private async Task LoadReceiptsAsync()
    {
        var moduleContext = context!;
        var from = SelectedDate(ReceiptFromDatePicker);
        var to = SelectedDate(ReceiptToDatePicker);

        if (from.HasValue && to.HasValue && from > to)
        {
            // Sortie anticipee : grille ET resume sont vides ensemble. En ne vidant
            // que le resume, on laisserait les lignes de la periode precedente a
            // l'ecran sous un en-tete decrivant une periode qui n'a jamais ete
            // chargee - deux informations contradictoires cote a cote.
            ReceiptsDataGrid.ItemsSource = null;
            UpdateReceiptActionState();
            ClearReceiptSummary();
            SetStatus("La date de début ne peut pas être postérieure à la date de fin.", isError: true);
            return;
        }

        var unitCode = SelectedCode(ReceiptFilterUnitComboBox);
        var method = (ReceiptFilterMethodComboBox.SelectedItem as TreasuryMethodOption)?.Value;
        var status = (ReceiptFilterStatusComboBox.SelectedItem as TreasuryReceiptStatusOption)?.Value;

        var rows = await moduleContext.ApiClient.GetCashReceiptsAsync(
            moduleContext.ApiBaseUrl,
            from,
            to,
            unitCode,
            method,
            status);

        ReceiptsDataGrid.ItemsSource = rows
            .OrderByDescending(row => row.ReceiptDate)
            .ThenBy(row => row.HotelUnitCode, StringComparer.CurrentCulture)
            .ToArray();

        UpdateReceiptActionState();

        // La grille vient d'etre remplacee : le resume est vide avant d'etre
        // recalcule, pour qu'un echec du second appel laisse un resume
        // manifestement vide plutot que les totaux de la periode precedente.
        ClearReceiptSummary();

        var summary = await moduleContext.ApiClient.GetCashReceiptSummaryAsync(
            moduleContext.ApiBaseUrl,
            from,
            to,
            unitCode,
            status);

        ApplyReceiptSummary(summary);

        // L'API du resume n'expose pas le mode de paiement : quand ce filtre est
        // actif, la grille et le resume ne portent pas sur le meme perimetre.
        ReceiptSummaryMethodWarningBorder.Visibility = method.HasValue
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async Task LoadPaymentOrdersAsync()
    {
        var moduleContext = context!;
        var from = SelectedDate(OrderFromDatePicker);
        var to = SelectedDate(OrderToDatePicker);

        if (from.HasValue && to.HasValue && from > to)
        {
            SetStatus("La date de début ne peut pas être postérieure à la date de fin.", isError: true);
            return;
        }

        var bankAccountCode = SelectedCode(OrderFilterBankAccountComboBox);
        var status = (OrderFilterStatusComboBox.SelectedItem as TreasuryOrderStatusOption)?.Value;

        var rows = await moduleContext.ApiClient.GetPaymentOrdersAsync(
            moduleContext.ApiBaseUrl,
            from,
            to,
            bankAccountCode,
            status);

        PaymentOrdersDataGrid.ItemsSource = rows
            .OrderBy(row => row.DueDate)
            .ThenBy(row => row.Beneficiary, StringComparer.CurrentCulture)
            .ToArray();

        UpdatePaymentOrderActionState();
    }

    private void ApplyReceiptSummary(CashReceiptSummaryResponse summary)
    {
        SummaryCashTextBlock.Text = summary.CashTotal.ToString("N2", CultureInfo.CurrentCulture);
        SummaryCardTextBlock.Text = summary.CardTotal.ToString("N2", CultureInfo.CurrentCulture);
        SummaryChequeTextBlock.Text = summary.ChequeTotal.ToString("N2", CultureInfo.CurrentCulture);
        SummaryBankTransferTextBlock.Text = summary.BankTransferTotal.ToString("N2", CultureInfo.CurrentCulture);
        SummaryGrandTotalTextBlock.Text = summary.GrandTotal.ToString("N2", CultureInfo.CurrentCulture);
        SummaryCountedTextBlock.Text = summary.TotalCount.ToString(CultureInfo.CurrentCulture);

        // Le statut retenu par l'API est rappele a l'ecran : sans filtre explicite,
        // seuls les encaissements confirmes alimentent les totaux.
        SummaryStatusTextBlock.Text = summary.Status switch
        {
            ReceiptStatus.Draft => "Brouillons",
            ReceiptStatus.Confirmed => "Confirmés",
            ReceiptStatus.Cancelled => "Annulés",
            _ => "Tous"
        };

        var brushKey = summary.Status switch
        {
            ReceiptStatus.Confirmed => "StatusValidatedForegroundBrush",
            ReceiptStatus.Cancelled => "StatusRejectedForegroundBrush",
            _ => "StatusDraftForegroundBrush"
        };

        SummaryStatusTextBlock.Foreground = (Brush)FindResource(brushKey);
    }

    private void ClearReceiptSummary()
    {
        var zero = 0m.ToString("N2", CultureInfo.CurrentCulture);

        SummaryCashTextBlock.Text = zero;
        SummaryCardTextBlock.Text = zero;
        SummaryChequeTextBlock.Text = zero;
        SummaryBankTransferTextBlock.Text = zero;
        SummaryGrandTotalTextBlock.Text = zero;
        SummaryCountedTextBlock.Text = "0";
        SummaryStatusTextBlock.Text = "Confirmés";
        SummaryStatusTextBlock.Foreground = (Brush)FindResource("StatusValidatedForegroundBrush");

        // Sans chiffres a l'ecran, l'avertissement sur le perimetre du resume
        // n'a plus d'objet.
        ReceiptSummaryMethodWarningBorder.Visibility = Visibility.Collapsed;
    }

    // ============================ Gestionnaires - communs =========================

    private async void RefreshAllButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    // ========================= Gestionnaires - encaissements ======================

    private async void RefreshReceiptsButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        await moduleContext.RunAsync(LoadReceiptsAsync);
    }

    private void ReceiptMethodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateReceiptFieldRequirements();
    }

    private void ReceiptsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateReceiptActionState();
    }

    private void ResetReceiptFormButton_Click(object sender, RoutedEventArgs e)
    {
        ResetReceiptForm();
        SetStatus("Saisie d'encaissement réinitialisée.");
    }

    private async void SaveReceiptButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        var built = BuildReceiptRequest();

        if (built is null)
        {
            return;
        }

        // Type explicite : la valeur capturee par la fonction ci-dessous doit etre
        // non nullable pour l'analyse de nullabilite.
        CreateCashReceiptRequest request = built;
        var receiptId = editingReceiptId;

        await moduleContext.RunAsync(async () =>
        {
            if (receiptId.HasValue)
            {
                var update = new UpdateCashReceiptRequest(
                    request.ReceiptDate,
                    request.HotelUnitCode,
                    request.Method,
                    request.Amount,
                    request.Reference,
                    request.BankAccountCode,
                    request.Notes);

                await moduleContext.ApiClient.UpdateCashReceiptAsync(moduleContext.ApiBaseUrl, receiptId.Value, update);
            }
            else
            {
                await moduleContext.ApiClient.CreateCashReceiptAsync(moduleContext.ApiBaseUrl, request);
            }

            await LoadReceiptsAsync();
            ResetReceiptForm();

            SetStatus(receiptId.HasValue
                ? "Encaissement modifié."
                : "Encaissement créé en brouillon.");
        });
    }

    private void EditReceiptButton_Click(object sender, RoutedEventArgs e)
    {
        if (ReceiptsDataGrid.SelectedItem is not CashReceiptResponse selected)
        {
            return;
        }

        if (!selected.CanEdit)
        {
            SetStatus(
                "Seul un encaissement en brouillon peut être modifié : un encaissement confirmé ne peut plus être modifié, il peut seulement être annulé avec motif.",
                isError: true);
            return;
        }

        editingReceiptId = selected.Id;

        ReceiptDatePicker.SelectedDate = selected.ReceiptDate.ToDateTime(TimeOnly.MinValue);
        SelectCode(ReceiptUnitComboBox, selected.HotelUnitCode);
        ReceiptMethodComboBox.SelectedValue = selected.Method;
        ReceiptAmountTextBox.Text = selected.Amount.ToString("N2", CultureInfo.CurrentCulture);
        ReceiptReferenceTextBox.Text = selected.Reference ?? string.Empty;
        var bankAccountStillActive = SelectReceiptBankAccount(selected.BankAccountCode);
        ReceiptNotesTextBox.Text = selected.Notes ?? string.Empty;

        SaveReceiptButton.Content = "Enregistrer les modifications";
        ReceiptFormModeTextBlock.Text = string.Format(
            CultureInfo.CurrentCulture,
            "Modification du brouillon du {0} — {1}",
            selected.ReceiptDate.ToString("d", CultureInfo.CurrentCulture),
            selected.HotelUnitCode);

        UpdateReceiptFieldRequirements();

        if (bankAccountStillActive)
        {
            SetStatus("Encaissement repris dans le formulaire.");
        }
        else
        {
            SetStatus(
                string.Format(
                    CultureInfo.CurrentCulture,
                    "Encaissement repris dans le formulaire. Le compte {0} a été désactivé depuis : choisissez un compte actif avant d'enregistrer.",
                    selected.BankAccountCode),
                isError: true);
        }
    }

    /// <summary>
    /// Reprend le compte bancaire d'un brouillon. La liste ne propose que les
    /// comptes actifs : si le compte du brouillon a ete desactive depuis, il est
    /// remis en tete de liste avec une mention explicite et selectionne, plutot que
    /// de disparaitre sans bruit et d'etre perdu au prochain enregistrement.
    /// Renvoie faux quand le compte a du etre rajoute ainsi.
    /// </summary>
    private bool SelectReceiptBankAccount(string? code)
    {
        SelectCode(ReceiptBankAccountComboBox, code);

        if (ReceiptBankAccountComboBox.SelectedItem is not null || string.IsNullOrWhiteSpace(code))
        {
            return true;
        }

        var options = ReceiptBankAccountComboBox.Items
            .OfType<TreasuryCodeOption>()
            .ToList();

        options.Insert(0, new TreasuryCodeOption(code, $"{code} — compte désactivé"));

        ReceiptBankAccountComboBox.ItemsSource = options;
        inactiveBankAccountOptionCode = code;
        SelectCode(ReceiptBankAccountComboBox, code);

        return false;
    }

    // Retire l'entree "compte désactivé" eventuellement ajoutee pour reprendre un
    // brouillon : une fois le formulaire vide, seule la liste des comptes actifs
    // doit rester proposee.
    private void RemoveInactiveBankAccountOption()
    {
        var stale = inactiveBankAccountOptionCode;

        if (stale is null)
        {
            return;
        }

        inactiveBankAccountOptionCode = null;

        ReceiptBankAccountComboBox.ItemsSource = ReceiptBankAccountComboBox.Items
            .OfType<TreasuryCodeOption>()
            .Where(option => !string.Equals(option.Code, stale, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async void ConfirmReceiptButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null || ReceiptsDataGrid.SelectedItem is not CashReceiptResponse selected)
        {
            return;
        }

        var question = string.Format(
            CultureInfo.CurrentCulture,
            "Confirmer l'encaissement de {0} du {1} ({2}) ?{3}{3}Un encaissement confirmé ne peut plus être modifié : il ne pourra plus qu'être annulé avec motif.",
            selected.Amount.ToString("N2", CultureInfo.CurrentCulture),
            selected.ReceiptDate.ToString("d", CultureInfo.CurrentCulture),
            selected.HotelUnitCode,
            Environment.NewLine);

        if (!Confirm(question, "Confirmation d'un encaissement"))
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.ConfirmCashReceiptAsync(moduleContext.ApiBaseUrl, selected.Id);
            await LoadReceiptsAsync();
            SetStatus("Encaissement confirmé.");
        });
    }

    private async void CancelReceiptButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null || ReceiptsDataGrid.SelectedItem is not CashReceiptResponse selected)
        {
            return;
        }

        var reason = ReceiptCancelReasonTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(reason))
        {
            SetStatus("Le motif d'annulation est obligatoire.", isError: true);
            ReceiptCancelReasonTextBox.Focus();
            return;
        }

        var question = string.Format(
            CultureInfo.CurrentCulture,
            "Annuler l'encaissement de {0} du {1} ({2}) ?{3}{3}Motif : {4}",
            selected.Amount.ToString("N2", CultureInfo.CurrentCulture),
            selected.ReceiptDate.ToString("d", CultureInfo.CurrentCulture),
            selected.HotelUnitCode,
            Environment.NewLine,
            reason);

        if (!Confirm(question, "Annulation d'un encaissement"))
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.CancelCashReceiptAsync(
                moduleContext.ApiBaseUrl,
                selected.Id,
                new CancelCashReceiptRequest(reason));

            ReceiptCancelReasonTextBox.Text = string.Empty;
            await LoadReceiptsAsync();
            SetStatus("Encaissement annulé.");
        });
    }

    private CreateCashReceiptRequest? BuildReceiptRequest()
    {
        if (ReceiptUnitComboBox.SelectedItem is not TreasuryCodeOption { Code: { } unitCode })
        {
            SetStatus("Sélectionnez une unité hôtelière.", isError: true);
            return null;
        }

        if (ReceiptMethodComboBox.SelectedItem is not TreasuryMethodOption { Value: { } method })
        {
            SetStatus("Sélectionnez un mode de paiement.", isError: true);
            return null;
        }

        if (!TryReadAmount(ReceiptAmountTextBox, "Le montant", out var amount))
        {
            return null;
        }

        var reference = ReceiptReferenceTextBox.Text.Trim();
        var bankAccountCode = SelectedCode(ReceiptBankAccountComboBox);
        var notes = ReceiptNotesTextBox.Text.Trim();

        // Regles portees par le domaine (CashReceipt) : la vue les applique avant
        // l'envoi pour ne pas laisser l'API refuser une saisie previsible.
        if (CashReceipt.RequiresReference(method) && string.IsNullOrWhiteSpace(reference))
        {
            SetStatus("La référence est obligatoire pour un chèque ou un virement.", isError: true);
            ReceiptReferenceTextBox.Focus();
            return null;
        }

        if (CashReceipt.RequiresBankAccount(method) && string.IsNullOrWhiteSpace(bankAccountCode))
        {
            SetStatus("Le compte bancaire est obligatoire pour tout mode de paiement autre que les espèces.", isError: true);
            ReceiptBankAccountComboBox.Focus();
            return null;
        }

        return new CreateCashReceiptRequest(
            SelectedDate(ReceiptDatePicker) ?? DateOnly.FromDateTime(DateTime.Today),
            unitCode,
            method,
            amount,
            string.IsNullOrWhiteSpace(reference) ? null : reference,
            bankAccountCode,
            string.IsNullOrWhiteSpace(notes) ? null : notes);
    }

    private void ResetReceiptForm()
    {
        editingReceiptId = null;

        ReceiptDatePicker.SelectedDate = DateTime.Today;
        ReceiptAmountTextBox.Text = string.Empty;
        ReceiptReferenceTextBox.Text = string.Empty;
        ReceiptNotesTextBox.Text = string.Empty;

        if (ReceiptMethodComboBox.Items.Count > 0)
        {
            ReceiptMethodComboBox.SelectedIndex = 0;
        }

        RemoveInactiveBankAccountOption();
        SelectCode(ReceiptBankAccountComboBox, null);

        SaveReceiptButton.Content = "Créer l'encaissement";
        ReceiptFormModeTextBlock.Text = "Nouvel encaissement";

        UpdateReceiptFieldRequirements();
    }

    // Marque a l'ecran les champs rendus obligatoires par le mode de paiement
    // choisi, plutot que d'attendre le refus de l'API.
    private void UpdateReceiptFieldRequirements()
    {
        var method = (ReceiptMethodComboBox.SelectedItem as TreasuryMethodOption)?.Value;

        var requiresReference = method.HasValue && CashReceipt.RequiresReference(method.Value);
        var requiresBankAccount = method.HasValue && CashReceipt.RequiresBankAccount(method.Value);

        ReceiptReferenceLabel.Text = requiresReference ? "Référence *" : "Référence";
        ReceiptBankAccountLabel.Text = requiresBankAccount ? "Compte bancaire *" : "Compte bancaire";
    }

    // Etat metier (statut de la ligne selectionnee) croise avec le droit
    // treasury.write, qui commande les quatre ecritures de cet onglet.
    // Pose le message d'explication quand le droit manque, et RESTAURE l'info-bulle
    // d'origine du bouton quand il est present : l'affectation doit etre symetrique,
    // sinon un message pose pour un profil restreint survit a la reconnexion d'un
    // profil qui, lui, a le droit.
    private void ApplyPermissionHint(Button button, bool allowed, string hint)
    {
        if (!originalToolTips.ContainsKey(button))
        {
            originalToolTips[button] = button.ToolTip;
        }

        button.ToolTip = allowed ? originalToolTips[button] : hint;
    }

    private void UpdateReceiptActionState()
    {
        var selected = ReceiptsDataGrid.SelectedItem as CashReceiptResponse;

        SaveReceiptButton.IsEnabled = canWrite;
        ConfirmReceiptButton.IsEnabled = canWrite && selected is { Status: ReceiptStatus.Draft };
        EditReceiptButton.IsEnabled = canWrite && selected is { CanEdit: true };
        CancelReceiptButton.IsEnabled = canWrite && selected is not null && selected.Status != ReceiptStatus.Cancelled;

        ApplyPermissionHint(SaveReceiptButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(ConfirmReceiptButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(EditReceiptButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(CancelReceiptButton, canWrite, WritePermissionHint);
    }

    // ====================== Gestionnaires - ordres de paiement ====================

    private async void RefreshPaymentOrdersButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        await moduleContext.RunAsync(LoadPaymentOrdersAsync);
    }

    private void PaymentOrdersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePaymentOrderActionState();
    }

    // DatePicker.SelectedDateChanged est un EventHandler<T> : son expediteur est
    // declare nullable, d'ou la signature "object?".
    private void OrderDatePicker_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (OrderDueDatePicker is null)
        {
            return;
        }

        var orderDate = OrderDatePicker.SelectedDate;

        // L'echeance ne peut pas preceder la date d'ordre : le calendrier est
        // borne et une echeance devenue anterieure est ramenee a la date d'ordre.
        OrderDueDatePicker.DisplayDateStart = orderDate;

        if (orderDate.HasValue && OrderDueDatePicker.SelectedDate < orderDate)
        {
            OrderDueDatePicker.SelectedDate = orderDate;
        }
    }

    private void ResetPaymentOrderFormButton_Click(object sender, RoutedEventArgs e)
    {
        ResetPaymentOrderForm();
        SetStatus("Saisie d'ordre de paiement réinitialisée.");
    }

    private async void CreatePaymentOrderButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        var built = BuildPaymentOrderRequest();

        if (built is null)
        {
            return;
        }

        CreatePaymentOrderRequest request = built;

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.CreatePaymentOrderAsync(moduleContext.ApiBaseUrl, request);
            await LoadPaymentOrdersAsync();
            ResetPaymentOrderForm();
            SetStatus("Ordre de paiement créé en brouillon.");
        });
    }

    /// <summary>
    /// Ouvre la demande de validation de l'ordre selectionne dans le module Workflows &amp;
    /// validations. La reference du sujet est l'identifiant de l'ordre, ecrit exactement
    /// comme la tresorerie l'interroge ensuite (Guid en minuscules, format "D") : c'est la
    /// meme clef des deux cotes, sinon la demande approuvee n'ouvrirait jamais la barriere.
    ///
    /// Sans circuit actif sur les ordres de paiement, le serveur refuse l'ouverture - et il a
    /// raison : dans ce cas l'approbation n'est bloquee par rien et la demande n'a pas d'objet.
    /// </summary>
    private async void OpenOrderApprovalButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null || PaymentOrdersDataGrid.SelectedItem is not PaymentOrderResponse selected)
        {
            return;
        }

        var question = string.Format(
            CultureInfo.CurrentCulture,
            "Ouvrir une demande de validation pour l'ordre de {0} au profit de {1} ?{2}{2}" +
            "Elle suivra le circuit actif des ordres de paiement, étape par étape, avant que l'ordre puisse être approuvé.",
            selected.Amount.ToString("N2", CultureInfo.CurrentCulture),
            selected.Beneficiary,
            Environment.NewLine);

        if (!Confirm(question, "Demande de validation d'un ordre de paiement"))
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var instance = await moduleContext.ApiClient.OpenApprovalInstanceAsync(
                moduleContext.ApiBaseUrl,
                new OpenApprovalInstanceRequest(ApprovalSubjectType.PaymentOrder, selected.Id.ToString()));

            SetStatus(string.Format(
                CultureInfo.CurrentCulture,
                "Demande de validation ouverte sur le circuit « {0} ». Étape courante : {1}.",
                instance.CircuitLabel,
                instance.CurrentStepLabel ?? "—"));
        });
    }

    private async void ApprovePaymentOrderButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null || PaymentOrdersDataGrid.SelectedItem is not PaymentOrderResponse selected)
        {
            return;
        }

        var question = string.Format(
            CultureInfo.CurrentCulture,
            "Approuver l'ordre de paiement de {0} au profit de {1} ?{2}{2}Échéance : {3}.",
            selected.Amount.ToString("N2", CultureInfo.CurrentCulture),
            selected.Beneficiary,
            Environment.NewLine,
            selected.DueDate.ToString("d", CultureInfo.CurrentCulture));

        if (!Confirm(question, "Approbation d'un ordre de paiement"))
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.ApprovePaymentOrderAsync(moduleContext.ApiBaseUrl, selected.Id);
            await LoadPaymentOrdersAsync();
            SetStatus("Ordre de paiement approuvé.");
        });
    }

    private async void PayPaymentOrderButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null || PaymentOrdersDataGrid.SelectedItem is not PaymentOrderResponse selected)
        {
            return;
        }

        var question = string.Format(
            CultureInfo.CurrentCulture,
            "Marquer comme payé l'ordre de {0} au profit de {1} ?{2}{2}Un ordre payé ne peut plus être annulé.",
            selected.Amount.ToString("N2", CultureInfo.CurrentCulture),
            selected.Beneficiary,
            Environment.NewLine);

        if (!Confirm(question, "Paiement d'un ordre"))
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.PayPaymentOrderAsync(moduleContext.ApiBaseUrl, selected.Id);
            await LoadPaymentOrdersAsync();
            SetStatus("Ordre de paiement marqué payé.");
        });
    }

    private async void CancelPaymentOrderButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null || PaymentOrdersDataGrid.SelectedItem is not PaymentOrderResponse selected)
        {
            return;
        }

        var reason = OrderCancelReasonTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(reason))
        {
            SetStatus("Le motif d'annulation est obligatoire.", isError: true);
            OrderCancelReasonTextBox.Focus();
            return;
        }

        var question = string.Format(
            CultureInfo.CurrentCulture,
            "Annuler l'ordre de paiement de {0} au profit de {1} ?{2}{2}Motif : {3}",
            selected.Amount.ToString("N2", CultureInfo.CurrentCulture),
            selected.Beneficiary,
            Environment.NewLine,
            reason);

        if (!Confirm(question, "Annulation d'un ordre de paiement"))
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.CancelPaymentOrderAsync(
                moduleContext.ApiBaseUrl,
                selected.Id,
                new CancelPaymentOrderRequest(reason));

            OrderCancelReasonTextBox.Text = string.Empty;
            await LoadPaymentOrdersAsync();
            SetStatus("Ordre de paiement annulé.");
        });
    }

    private CreatePaymentOrderRequest? BuildPaymentOrderRequest()
    {
        var beneficiary = OrderBeneficiaryTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(beneficiary))
        {
            SetStatus("Le bénéficiaire est obligatoire.", isError: true);
            OrderBeneficiaryTextBox.Focus();
            return null;
        }

        if (!TryReadAmount(OrderAmountTextBox, "Le montant", out var amount))
        {
            return null;
        }

        if (OrderBankAccountComboBox.SelectedItem is not TreasuryCodeOption { Code: { } bankAccountCode })
        {
            SetStatus("Sélectionnez un compte bancaire actif.", isError: true);
            return null;
        }

        var orderDate = SelectedDate(OrderDatePicker) ?? DateOnly.FromDateTime(DateTime.Today);
        var dueDate = SelectedDate(OrderDueDatePicker) ?? orderDate;

        if (dueDate < orderDate)
        {
            SetStatus("L'échéance ne peut pas précéder la date d'ordre.", isError: true);
            return null;
        }

        var reference = OrderReferenceTextBox.Text.Trim();

        return new CreatePaymentOrderRequest(
            orderDate,
            beneficiary,
            amount,
            dueDate,
            bankAccountCode,
            string.IsNullOrWhiteSpace(reference) ? null : reference);
    }

    private void ResetPaymentOrderForm()
    {
        var today = DateTime.Today;

        OrderDatePicker.SelectedDate = today;
        OrderDueDatePicker.DisplayDateStart = today;
        OrderDueDatePicker.SelectedDate = today;
        OrderBeneficiaryTextBox.Text = string.Empty;
        OrderAmountTextBox.Text = string.Empty;
        OrderReferenceTextBox.Text = string.Empty;

        if (OrderBankAccountComboBox.Items.Count > 0)
        {
            OrderBankAccountComboBox.SelectedIndex = 0;
        }
    }

    // Etat metier croise avec les droits : l'approbation exige treasury.approve,
    // les trois autres ecritures treasury.write.
    private void UpdatePaymentOrderActionState()
    {
        var selected = PaymentOrdersDataGrid.SelectedItem as PaymentOrderResponse;

        CreatePaymentOrderButton.IsEnabled = canWrite;

        // Une demande de validation ne se conçoit que sur un ordre encore approuvable :
        // le meme etat metier que "Approuver", avec le droit du module des validations.
        OpenOrderApprovalButton.IsEnabled = canOpenApproval && selected is { Status: PaymentOrderStatus.Draft };
        ApprovePaymentOrderButton.IsEnabled = canApprove && selected is { Status: PaymentOrderStatus.Draft };
        PayPaymentOrderButton.IsEnabled = canWrite && selected is { Status: PaymentOrderStatus.Approved };
        CancelPaymentOrderButton.IsEnabled = canWrite
            && selected is not null
            && selected.Status != PaymentOrderStatus.Paid
            && selected.Status != PaymentOrderStatus.Cancelled;

        ApplyPermissionHint(CreatePaymentOrderButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(PayPaymentOrderButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(CancelPaymentOrderButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(ApprovePaymentOrderButton, canApprove, ApprovePermissionHint);
        ApplyPermissionHint(OpenOrderApprovalButton, canOpenApproval, OpenApprovalPermissionHint);
    }

    // ======================= Gestionnaires - comptes bancaires ====================

    private async void RefreshBankAccountsButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        await moduleContext.RunAsync(LoadBankAccountsAsync);
    }

    private async void IncludeInactiveBankAccountsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // ResetState remet cette case a false : ce n'est pas une demande de
        // rechargement de l'utilisateur.
        if (suspendFilterReload)
        {
            return;
        }

        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        await moduleContext.RunAsync(LoadBankAccountsAsync);
    }

    private void BankAccountsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateBankAccountActionState();
    }

    private void NewBankAccountButton_Click(object sender, RoutedEventArgs e)
    {
        ResetBankAccountForm();
        SetStatus("Saisie d'un nouveau compte bancaire.");
    }

    private void EditBankAccountButton_Click(object sender, RoutedEventArgs e)
    {
        if (BankAccountsDataGrid.SelectedItem is not BankAccountResponse selected)
        {
            return;
        }

        editingBankAccountCode = selected.Code;

        BankAccountCodeTextBox.Text = selected.Code;
        BankAccountCodeTextBox.IsEnabled = false;
        BankAccountLabelTextBox.Text = selected.Label;
        BankAccountBankNameTextBox.Text = selected.BankName;
        BankAccountNumberTextBox.Text = selected.AccountNumber;

        SaveBankAccountButton.Content = "Enregistrer les modifications";
        BankAccountFormModeTextBlock.Text = $"Modification du compte {selected.Code}";

        SetStatus("Compte bancaire repris dans le formulaire.");
    }

    private async void SaveBankAccountButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null)
        {
            return;
        }

        var code = BankAccountCodeTextBox.Text.Trim();
        var label = BankAccountLabelTextBox.Text.Trim();
        var bankName = BankAccountBankNameTextBox.Text.Trim();
        var accountNumber = BankAccountNumberTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(code) ||
            string.IsNullOrWhiteSpace(label) ||
            string.IsNullOrWhiteSpace(bankName) ||
            string.IsNullOrWhiteSpace(accountNumber))
        {
            SetStatus("Code, libellé, banque et numéro de compte sont obligatoires.", isError: true);
            return;
        }

        var editedCode = editingBankAccountCode;

        await moduleContext.RunAsync(async () =>
        {
            if (editedCode is null)
            {
                await moduleContext.ApiClient.CreateBankAccountAsync(
                    moduleContext.ApiBaseUrl,
                    new CreateBankAccountRequest(code, label, bankName, accountNumber));
            }
            else
            {
                await moduleContext.ApiClient.UpdateBankAccountAsync(
                    moduleContext.ApiBaseUrl,
                    editedCode,
                    new UpdateBankAccountRequest(label, bankName, accountNumber));
            }

            await LoadBankAccountsAsync();
            ResetBankAccountForm();

            SetStatus(editedCode is null
                ? "Compte bancaire créé."
                : "Compte bancaire modifié.");
        });
    }

    private async void ActivateBankAccountButton_Click(object sender, RoutedEventArgs e)
    {
        await SetBankAccountActiveAsync(isActive: true);
    }

    private async void DeactivateBankAccountButton_Click(object sender, RoutedEventArgs e)
    {
        await SetBankAccountActiveAsync(isActive: false);
    }

    private async Task SetBankAccountActiveAsync(bool isActive)
    {
        var moduleContext = RequireContext();

        if (moduleContext is null || BankAccountsDataGrid.SelectedItem is not BankAccountResponse selected)
        {
            return;
        }

        if (!isActive)
        {
            var question = string.Format(
                CultureInfo.CurrentCulture,
                "Désactiver le compte {0} ({1}) ?{2}{2}Il ne sera plus proposé à la saisie des encaissements ni des ordres de paiement.",
                selected.Code,
                selected.Label,
                Environment.NewLine);

            if (!Confirm(question, "Désactivation d'un compte bancaire"))
            {
                return;
            }
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.SetBankAccountActiveAsync(moduleContext.ApiBaseUrl, selected.Code, isActive);
            await LoadBankAccountsAsync();
            SetStatus(isActive ? "Compte bancaire activé." : "Compte bancaire désactivé.");
        });
    }

    private void ResetBankAccountForm()
    {
        editingBankAccountCode = null;

        BankAccountCodeTextBox.Text = string.Empty;
        BankAccountCodeTextBox.IsEnabled = true;
        BankAccountLabelTextBox.Text = string.Empty;
        BankAccountBankNameTextBox.Text = string.Empty;
        BankAccountNumberTextBox.Text = string.Empty;

        SaveBankAccountButton.Content = "Créer le compte";
        BankAccountFormModeTextBlock.Text = "Nouveau compte";
    }

    // Etat metier croise avec le droit treasury.write. La reprise d'un compte dans
    // le formulaire est grisee elle aussi : sans droit d'ecriture, elle ne menerait
    // qu'a un bouton d'enregistrement inactif.
    private void UpdateBankAccountActionState()
    {
        var selected = BankAccountsDataGrid.SelectedItem as BankAccountResponse;

        SaveBankAccountButton.IsEnabled = canWrite;
        EditBankAccountButton.IsEnabled = canWrite && selected is not null;
        ActivateBankAccountButton.IsEnabled = canWrite && selected is { IsActive: false };
        DeactivateBankAccountButton.IsEnabled = canWrite && selected is { IsActive: true };

        ApplyPermissionHint(SaveBankAccountButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(EditBankAccountButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(ActivateBankAccountButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(DeactivateBankAccountButton, canWrite, WritePermissionHint);
    }

    // ================================== Utilitaires ===============================

    private ModuleViewContext? RequireContext()
    {
        if (context is null || !context.ApiClient.IsAuthenticated)
        {
            return null;
        }

        return context;
    }

    private void SetStatus(string message, bool isError = false)
    {
        context?.SetStatus(message, isError);
    }

    // Ces cinq confirmations couvrent les actes les plus engageants du module,
    // dont "Marquer paye" qui est definitif. La boite est donc modale a la fenetre
    // proprietaire, marquee d'un avertissement, et son bouton par defaut est "Non" :
    // une frappe Entree ne doit jamais suffire a declencher l'action.
    private bool Confirm(string question, string title)
    {
        var owner = Window.GetWindow(this);

        var result = owner is null
            ? MessageBox.Show(question, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(owner, question, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    // Meme lecture des montants que la saisie des recettes journalieres : la
    // virgule (culture courante) comme le point (culture invariante) sont acceptes.
    private bool TryReadAmount(TextBox textBox, string label, out decimal value)
    {
        var text = textBox.Text.Trim();

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value) ||
            decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            if (value <= 0)
            {
                SetStatus($"{label} doit être strictement positif.", isError: true);
                textBox.Focus();
                return false;
            }

            return true;
        }

        SetStatus($"{label} doit être un montant valide.", isError: true);
        textBox.Focus();
        return false;
    }

    private static DateOnly? SelectedDate(DatePicker picker)
    {
        return picker.SelectedDate.HasValue
            ? DateOnly.FromDateTime(picker.SelectedDate.Value)
            : null;
    }

    private static string? SelectedCode(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as TreasuryCodeOption)?.Code;
    }

    // Selectionne l'entree portant ce code ; un code nul designe l'entree neutre
    // ("Toutes les unités", "Aucun", "Tous les comptes") quand elle existe.
    private static void SelectCode(ComboBox comboBox, string? code)
    {
        comboBox.SelectedItem = comboBox.Items
            .OfType<TreasuryCodeOption>()
            .FirstOrDefault(option => string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Entree de liste deroulante identifiee par un code (unite, compte bancaire).</summary>
public sealed class TreasuryCodeOption(string? code, string label)
{
    public string? Code { get; } = code;

    public string Label { get; } = label;
}

/// <summary>Entree de liste deroulante pour un mode de paiement (valeur nulle = tous).</summary>
public sealed class TreasuryMethodOption(PaymentMethod? value, string label)
{
    public PaymentMethod? Value { get; } = value;

    public string Label { get; } = label;
}

/// <summary>Entree de liste deroulante pour un statut d'encaissement (valeur nulle = tous).</summary>
public sealed class TreasuryReceiptStatusOption(ReceiptStatus? value, string label)
{
    public ReceiptStatus? Value { get; } = value;

    public string Label { get; } = label;
}

/// <summary>Entree de liste deroulante pour un statut d'ordre de paiement (valeur nulle = tous).</summary>
public sealed class TreasuryOrderStatusOption(PaymentOrderStatus? value, string label)
{
    public PaymentOrderStatus? Value { get; } = value;

    public string Label { get; } = label;
}

/// <summary>Libelles francais du module, partages par les listes et les grilles.</summary>
public static class TreasuryLabels
{
    public static string Method(PaymentMethod method)
    {
        return method switch
        {
            PaymentMethod.Cash => "Espèces",
            PaymentMethod.Card => "Carte",
            PaymentMethod.Cheque => "Chèque",
            PaymentMethod.BankTransfer => "Virement",
            _ => method.ToString()
        };
    }
}

/// <summary>Affiche le mode de paiement en francais dans la grille des encaissements.</summary>
public sealed class PaymentMethodLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is PaymentMethod method ? TreasuryLabels.Method(method) : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
