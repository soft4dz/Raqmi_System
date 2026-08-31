using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RaqmiSystem.Application.Approvals;
using RaqmiSystem.Domain.Approvals;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module Workflows et validations : decisions en attente du profil connecte,
/// configuration des circuits de validation, historique des instances par sujet
/// et periode. Vue autonome : elle ne connait que le ModuleViewContext que la
/// fenetre lui prete, jamais MainWindow ni une autre vue.
/// </summary>
public partial class ApprovalsView : UserControl
{
    // NOTE INTEGRATEUR : cles de permission en chaines litterales, a remplacer par
    // les constantes PermissionCatalog une fois les cles ajoutees au catalogue.
    private const string WritePermission = "approvals.write";

    private const string DecidePermission = "approvals.decide";

    private const string WritePermissionHint = "Permission approvals.write requise : votre profil ne peut que consulter les circuits.";

    private const string DecidePermissionHint = "Permission approvals.decide requise : votre profil ne peut pas décider.";

    private ModuleViewContext? context;

    private bool canWriteApprovals = true;

    private bool canDecideApprovals = true;

    // Null en mode creation, code du circuit edite en mode modification.
    private string? editingCircuitCode;

    // Etapes en cours d'edition dans la fiche circuit : l'ordre visuel est l'ordre
    // de decision, les rangs sont recalcules localement apres chaque ajout/retrait
    // (le serveur reattribue de toute facon des rangs contigus depuis 1).
    private readonly ObservableCollection<StepDraftRow> draftSteps = [];

    // Info-bulles d'origine des boutons d'ecriture, capturees avant toute
    // substitution par le message de permission : l'affectation doit rester
    // symetrique (voir ApplyPermissionHint), car les vues survivent a la
    // deconnexion et resservent au profil suivant.
    private readonly Dictionary<Button, object?> originalToolTips = [];

    public ApprovalsView()
    {
        InitializeComponent();

        SubjectTypeComboBox.ItemsSource = ApprovalDisplay.SubjectOptions;
        SubjectTypeComboBox.SelectedValue = ApprovalSubjectType.PaymentOrder;

        // La liste des roles eligibles vient du domaine (ApprovalStep.AllowedRoles) :
        // aucune valeur recopiee, l'ecran ne promet jamais une regle differente de
        // celle du serveur.
        StepRoleComboBox.ItemsSource = ApprovalDisplay.RoleOptions;
        StepRoleComboBox.SelectedIndex = 0;

        HistorySubjectComboBox.ItemsSource = ApprovalDisplay.SubjectFilterOptions;
        HistorySubjectComboBox.SelectedIndex = 0;
        HistoryStatusComboBox.ItemsSource = ApprovalDisplay.StatusFilterOptions;
        HistoryStatusComboBox.SelectedIndex = 0;

        StepsDataGrid.ItemsSource = draftSteps;

        ResetCircuitForm();
        UpdateActionButtons();
    }

    /// <summary>Memorise le contexte fourni par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext context)
    {
        this.context = context;
        canWriteApprovals = context.HasPermission(WritePermission);
        canDecideApprovals = context.HasPermission(DecidePermission);

        UpdateActionButtons();
    }

    /// <summary>
    /// (Re)charge les trois sections. Appelee a la premiere ouverture de l'onglet et
    /// par le bouton Tout actualiser. Sort silencieusement tant qu'aucun contexte
    /// n'est disponible ou qu'aucune session n'est ouverte.
    /// </summary>
    public async Task LoadAsync()
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await ReloadPendingAsync();
            await ReloadCircuitsAsync();
            await ReloadHistoryAsync();
        });
    }

    /// <summary>Vide grilles, formulaires et compteurs (appelee a la deconnexion).</summary>
    public void ResetState()
    {
        PendingDataGrid.ItemsSource = null;
        PendingCountTextBlock.Text = string.Empty;
        DecisionTitleTextBlock.Text = "Décision — sélectionnez une instance";
        DecisionCommentTextBox.Text = string.Empty;

        CircuitsDataGrid.ItemsSource = null;
        IncludeInactiveCircuitsCheckBox.IsChecked = false;
        ResetCircuitForm();

        HistoryDataGrid.ItemsSource = null;
        DecisionsDataGrid.ItemsSource = null;
        DecisionsTitleTextBlock.Text = "Décisions — sélectionnez une instance";
        HistorySubjectComboBox.SelectedIndex = 0;
        HistoryStatusComboBox.SelectedIndex = 0;
        HistoryReferenceTextBox.Text = string.Empty;
        HistoryFromDatePicker.SelectedDate = null;
        HistoryToDatePicker.SelectedDate = null;

        UpdateActionButtons();
    }

    private async void RefreshAllButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await ReloadPendingAsync();
            await ReloadCircuitsAsync();
            await ReloadHistoryAsync();
            moduleContext.SetStatus("Workflows actualisés.");
        });
    }

    // ======================== 1. En attente de ma decision ========================

    private async Task ReloadPendingAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        // Sans le droit de decider, le serveur refuserait l'appel (403) : la section
        // reste simplement vide, avec l'explication en compteur.
        if (!canDecideApprovals)
        {
            PendingDataGrid.ItemsSource = null;
            PendingCountTextBlock.Text = "Permission approvals.decide requise";
            return;
        }

        var selectedId = (PendingDataGrid.SelectedItem as PendingRowView)?.Id;

        var pending = await moduleContext.ApiClient.GetPendingApprovalInstancesAsync(moduleContext.ApiBaseUrl);

        var rows = pending.Select(ToPendingRow).ToArray();
        PendingDataGrid.ItemsSource = rows;

        PendingCountTextBlock.Text = pending.Count == 1
            ? "1 décision en attente"
            : $"{pending.Count.ToString(CultureInfo.CurrentCulture)} décisions en attente";

        RestorePendingSelection(rows, selectedId);
        UpdateActionButtons();
    }

    private void RestorePendingSelection(IReadOnlyList<PendingRowView> rows, Guid? id)
    {
        if (id is null)
        {
            return;
        }

        var restored = rows.FirstOrDefault(row => row.Id == id.Value);

        if (restored is null)
        {
            return;
        }

        PendingDataGrid.SelectedItem = restored;
        PendingDataGrid.ScrollIntoView(restored);
    }

    private void PendingDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Le commentaire saisi vise l'instance selectionnee : il est efface a tout
        // changement de contexte pour ne jamais etre envoye sur la mauvaise ligne.
        DecisionCommentTextBox.Text = string.Empty;

        DecisionTitleTextBlock.Text = PendingDataGrid.SelectedItem is PendingRowView selected
            ? $"Décision — {selected.SubjectLabel} {selected.SubjectReference}, étape {selected.CurrentStepDisplay}"
            : "Décision — sélectionnez une instance";

        UpdateActionButtons();
    }

    private async void ApproveButton_Click(object sender, RoutedEventArgs e)
    {
        await DecideAsync(approved: true);
    }

    private async void RejectButton_Click(object sender, RoutedEventArgs e)
    {
        await DecideAsync(approved: false);
    }

    private async Task DecideAsync(bool approved)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        if (PendingDataGrid.SelectedItem is not PendingRowView selected)
        {
            moduleContext.SetStatus("Sélectionnez une instance en attente.", isError: true);
            return;
        }

        var comment = ReadOptional(DecisionCommentTextBox);

        // Regle du domaine (ApprovalDecision) rappelee avant l'aller-retour : un
        // rejet sans motif n'est pas auditable, le serveur le refuserait de toute facon.
        if (!approved && comment is null)
        {
            moduleContext.SetStatus("Un commentaire est obligatoire pour rejeter.", isError: true);
            return;
        }

        // Acte engageant dans les deux sens : approuver la derniere etape libere le
        // sujet (l'ordre de paiement devient approuvable), rejeter clot l'instance
        // definitivement. Confirmation gabarit : proprietaire, avertissement, defaut Non.
        var question = approved
            ? $"Approuver l'étape {selected.CurrentStepDisplay} de {selected.SubjectLabel} {selected.SubjectReference} ?\n" +
              "Une étape approuvée ne peut plus être reprise."
            : $"Rejeter {selected.SubjectLabel} {selected.SubjectReference} ?\n" +
              $"Le rejet clôt définitivement l'instance de validation.\nMotif : {comment}";

        if (!Confirm(question, approved ? "Approuver l'étape" : "Rejeter l'instance"))
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var decided = approved
                ? await moduleContext.ApiClient.ApproveApprovalInstanceAsync(moduleContext.ApiBaseUrl, selected.Id, comment)
                : await moduleContext.ApiClient.RejectApprovalInstanceAsync(moduleContext.ApiBaseUrl, selected.Id, comment!);

            DecisionCommentTextBox.Text = string.Empty;

            await ReloadPendingAsync();
            await ReloadHistoryAsync();

            moduleContext.SetStatus(decided.Status switch
            {
                ApprovalInstanceStatus.Approved => $"Instance {decided.SubjectReference} approuvée — toutes les étapes sont validées.",
                ApprovalInstanceStatus.Rejected => $"Instance {decided.SubjectReference} rejetée.",
                _ => $"Étape approuvée — l'instance {decided.SubjectReference} attend l'étape {decided.CurrentRank}."
            });
        });
    }

    // ================================ 2. Circuits ================================

    private async void IncludeInactiveCircuitsCheckBox_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(ReloadCircuitsAsync);
    }

    private async Task ReloadCircuitsAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        var selectedCode = (CircuitsDataGrid.SelectedItem as CircuitRowView)?.Code;

        var circuits = await moduleContext.ApiClient.GetApprovalCircuitsAsync(
            moduleContext.ApiBaseUrl,
            subjectType: null,
            IncludeInactiveCircuitsCheckBox.IsChecked == true);

        var rows = circuits.Select(ToCircuitRow).ToArray();
        CircuitsDataGrid.ItemsSource = rows;

        RestoreCircuitSelection(rows, selectedCode);
        UpdateActionButtons();
    }

    // Le code du circuit est la cle stable d'une ligne a l'autre : la selection est
    // rendue sur ce code, ou abandonnee quand la ligne n'est plus dans la liste.
    private void RestoreCircuitSelection(IReadOnlyList<CircuitRowView> rows, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        var restored = rows.FirstOrDefault(row => string.Equals(row.Code, code, StringComparison.OrdinalIgnoreCase));

        if (restored is null)
        {
            return;
        }

        CircuitsDataGrid.SelectedItem = restored;
        CircuitsDataGrid.ScrollIntoView(restored);
    }

    private void CircuitsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateActionButtons();

        if (CircuitsDataGrid.SelectedItem is not CircuitRowView selected)
        {
            return;
        }

        // Selectionner une ligne bascule le formulaire en mode modification : le code
        // identifie le circuit cote API, il n'est donc plus modifiable, et le sujet
        // est fixe a la creation (regle du domaine).
        var circuit = selected.Source;

        editingCircuitCode = circuit.Code;
        CircuitFormTitleTextBlock.Text = $"Modifier {circuit.Code}";
        CircuitCodeTextBox.Text = circuit.Code;
        CircuitCodeTextBox.IsEnabled = false;
        CircuitLabelTextBox.Text = circuit.Label;
        SubjectTypeComboBox.SelectedValue = circuit.SubjectType;
        SubjectTypeComboBox.IsEnabled = false;
        SaveCircuitButton.Content = "Enregistrer les modifications";

        draftSteps.Clear();

        foreach (var step in circuit.Steps.OrderBy(step => step.Rank))
        {
            draftSteps.Add(new StepDraftRow(
                step.Rank,
                step.Label,
                step.RequiredRole,
                ApprovalDisplay.RoleLabel(step.RequiredRole)));
        }
    }

    private void NewCircuitButton_Click(object sender, RoutedEventArgs e)
    {
        ResetCircuitForm();
        UpdateActionButtons();
    }

    private void AddStepButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;
        var label = StepLabelTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(label))
        {
            moduleContext?.SetStatus("Le libellé de l'étape est requis.", isError: true);
            return;
        }

        if (StepRoleComboBox.SelectedValue is not string role)
        {
            moduleContext?.SetStatus("Sélectionnez le rôle requis par l'étape.", isError: true);
            return;
        }

        draftSteps.Add(new StepDraftRow(draftSteps.Count + 1, label, role, ApprovalDisplay.RoleLabel(role)));
        StepLabelTextBox.Text = string.Empty;
    }

    private void RemoveStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (StepsDataGrid.SelectedItem is not StepDraftRow selected)
        {
            context?.SetStatus("Sélectionnez l'étape à retirer.", isError: true);
            return;
        }

        draftSteps.Remove(selected);
        RenumberDraftSteps();
    }

    // Apercu local des rangs uniquement : la source de verite reste le serveur, qui
    // reattribue des rangs contigus depuis 1 a l'enregistrement.
    private void RenumberDraftSteps()
    {
        var reranked = draftSteps
            .Select((step, index) => step with { Rank = index + 1 })
            .ToArray();

        draftSteps.Clear();

        foreach (var step in reranked)
        {
            draftSteps.Add(step);
        }
    }

    private async void SaveCircuitButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var code = CircuitCodeTextBox.Text.Trim();
            var label = CircuitLabelTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(label))
            {
                moduleContext.SetStatus("Le code et le libellé du circuit sont requis.", isError: true);
                return;
            }

            if (SubjectTypeComboBox.SelectedValue is not ApprovalSubjectType subjectType)
            {
                moduleContext.SetStatus("Sélectionnez le sujet couvert par le circuit.", isError: true);
                return;
            }

            var steps = draftSteps
                .Select(step => new ApprovalStepRequest(step.Label, step.Role))
                .ToArray();

            var existingCode = editingCircuitCode;

            // Le serveur normalise le code (majuscules) : c'est la valeur qu'il
            // renvoie qui est affichee et reutilisee, jamais la saisie brute.
            if (existingCode is null)
            {
                var created = await moduleContext.ApiClient.CreateApprovalCircuitAsync(
                    moduleContext.ApiBaseUrl,
                    new CreateApprovalCircuitRequest(code, label, subjectType, steps));

                moduleContext.SetStatus($"Circuit {created.Code} créé. Activez-le pour qu'il gouverne son sujet.");
            }
            else
            {
                var updated = await moduleContext.ApiClient.UpdateApprovalCircuitAsync(
                    moduleContext.ApiBaseUrl,
                    existingCode,
                    new UpdateApprovalCircuitRequest(label, steps));

                moduleContext.SetStatus($"Circuit {updated.Code} mis à jour. Les instances déjà ouvertes conservent leurs étapes.");
            }

            ResetCircuitForm();
            await ReloadCircuitsAsync();
        });
    }

    private async void ActivateCircuitButton_Click(object sender, RoutedEventArgs e)
    {
        await SetCircuitActiveAsync(isActive: true);
    }

    private async void DeactivateCircuitButton_Click(object sender, RoutedEventArgs e)
    {
        await SetCircuitActiveAsync(isActive: false);
    }

    private async Task SetCircuitActiveAsync(bool isActive)
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        if (CircuitsDataGrid.SelectedItem is not CircuitRowView selected)
        {
            moduleContext.SetStatus("Sélectionnez un circuit.", isError: true);
            return;
        }

        // Acte engageant sur tout le perimetre du sujet : confirmation explicite.
        var question = isActive
            ? $"Activer le circuit {selected.Code} ({selected.Label}) ?\n" +
              $"Tous les sujets « {selected.SubjectLabel} » exigeront désormais son approbation complète."
            : $"Désactiver le circuit {selected.Code} ({selected.Label}) ?\n" +
              $"Les sujets « {selected.SubjectLabel} » ne seront plus bloqués par la validation ; les instances déjà ouvertes restent consultables.";

        if (!Confirm(question, isActive ? "Activer le circuit" : "Désactiver le circuit"))
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var changed = await moduleContext.ApiClient.SetApprovalCircuitActiveAsync(
                moduleContext.ApiBaseUrl,
                selected.Code,
                isActive);

            await ReloadCircuitsAsync();

            moduleContext.SetStatus(isActive
                ? $"Circuit {changed.Code} activé."
                : $"Circuit {changed.Code} désactivé.");
        });
    }

    private void ResetCircuitForm()
    {
        editingCircuitCode = null;
        CircuitFormTitleTextBlock.Text = "Nouveau circuit";
        CircuitCodeTextBox.Text = string.Empty;
        CircuitCodeTextBox.IsEnabled = true;
        CircuitLabelTextBox.Text = string.Empty;
        SubjectTypeComboBox.SelectedValue = ApprovalSubjectType.PaymentOrder;
        SubjectTypeComboBox.IsEnabled = true;
        StepLabelTextBox.Text = string.Empty;
        draftSteps.Clear();
        SaveCircuitButton.Content = "Créer le circuit";
        CircuitsDataGrid.SelectedItem = null;
    }

    // ================================ 3. Historique ================================

    private async void SearchHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        await ReloadHistoryWithStatusAsync();
    }

    private async void HistoryReferenceTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await ReloadHistoryWithStatusAsync();
    }

    private async Task ReloadHistoryWithStatusAsync()
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await ReloadHistoryAsync();
            moduleContext.SetStatus("Historique des validations actualisé.");
        });
    }

    private async Task ReloadHistoryAsync()
    {
        var moduleContext = context;

        if (moduleContext is null)
        {
            return;
        }

        // Les bornes de calendrier raisonnent en local (DateTime.Today cote
        // DatePicker) ; seules les DATES partent au serveur, qui filtre l'ouverture
        // des instances au jour pres.
        var from = ToDateOnly(HistoryFromDatePicker.SelectedDate);
        var to = ToDateOnly(HistoryToDatePicker.SelectedDate);

        if (from.HasValue && to.HasValue && from > to)
        {
            moduleContext.SetStatus("La date de début ne peut pas dépasser la date de fin.", isError: true);
            return;
        }

        var selectedId = (HistoryDataGrid.SelectedItem as HistoryRowView)?.Id;

        var instances = await moduleContext.ApiClient.GetApprovalInstancesAsync(
            moduleContext.ApiBaseUrl,
            HistorySubjectComboBox.SelectedValue as ApprovalSubjectType?,
            ReadOptional(HistoryReferenceTextBox),
            HistoryStatusComboBox.SelectedValue as ApprovalInstanceStatus?,
            from,
            to);

        var rows = instances.Select(ToHistoryRow).ToArray();
        HistoryDataGrid.ItemsSource = rows;

        if (selectedId.HasValue)
        {
            var restored = rows.FirstOrDefault(row => row.Id == selectedId.Value);

            if (restored is not null)
            {
                HistoryDataGrid.SelectedItem = restored;
                HistoryDataGrid.ScrollIntoView(restored);
            }
        }

        RefreshDecisionsPanel();
    }

    private void HistoryDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshDecisionsPanel();
    }

    private void RefreshDecisionsPanel()
    {
        if (HistoryDataGrid.SelectedItem is not HistoryRowView selected)
        {
            DecisionsDataGrid.ItemsSource = null;
            DecisionsTitleTextBlock.Text = "Décisions — sélectionnez une instance";
            return;
        }

        DecisionsTitleTextBlock.Text = $"Décisions — {selected.SubjectLabel} {selected.SubjectReference} ({selected.CircuitLabel})";

        DecisionsDataGrid.ItemsSource = selected.Source.Decisions
            .OrderBy(decision => decision.Rank)
            .Select(decision => new DecisionRowView(
                decision.Rank,
                decision.StepLabel,
                decision.Approved ? "Approuvée" : "Rejetée",
                decision.DecidedBy,
                FormatLocal(decision.DecidedAt),
                decision.Comment))
            .ToArray();
    }

    // ================================ Transverse ================================

    // Les actions d'ecriture sont grisees quand le droit manque, avec info-bulle
    // posee ET restauree (motif ApplyPermissionHint) ; decider est un droit
    // distinct de configurer.
    private void UpdateActionButtons()
    {
        var pendingSelected = PendingDataGrid.SelectedItem is PendingRowView;
        var circuitSelected = CircuitsDataGrid.SelectedItem as CircuitRowView;

        ApproveButton.IsEnabled = canDecideApprovals && pendingSelected;
        RejectButton.IsEnabled = canDecideApprovals && pendingSelected;

        SaveCircuitButton.IsEnabled = canWriteApprovals;
        ActivateCircuitButton.IsEnabled = canWriteApprovals && circuitSelected is { IsActive: false };
        DeactivateCircuitButton.IsEnabled = canWriteApprovals && circuitSelected is { IsActive: true };

        ApplyPermissionHint(ApproveButton, canDecideApprovals, DecidePermissionHint);
        ApplyPermissionHint(RejectButton, canDecideApprovals, DecidePermissionHint);
        ApplyPermissionHint(SaveCircuitButton, canWriteApprovals, WritePermissionHint);
        ApplyPermissionHint(ActivateCircuitButton, canWriteApprovals, WritePermissionHint);
        ApplyPermissionHint(DeactivateCircuitButton, canWriteApprovals, WritePermissionHint);
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

    private static string? ReadOptional(TextBox textBox)
    {
        var value = textBox.Text.Trim();

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static DateOnly? ToDateOnly(DateTime? date)
    {
        return date.HasValue ? DateOnly.FromDateTime(date.Value) : null;
    }

    // Tout horodatage UTC renvoye par l'API est converti en heure du poste avant
    // affichage.
    private static string FormatLocal(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
    }

    private static PendingRowView ToPendingRow(ApprovalInstanceResponse instance)
    {
        return new PendingRowView(
            instance.Id,
            ApprovalDisplay.SubjectLabel(instance.SubjectType),
            instance.SubjectReference,
            instance.CircuitLabel,
            instance.CurrentRank.HasValue
                ? $"{instance.CurrentRank} — {instance.CurrentStepLabel}"
                : string.Empty,
            ApprovalDisplay.RoleLabel(instance.CurrentStepRequiredRole),
            FormatLocal(instance.CreatedAt));
    }

    private static CircuitRowView ToCircuitRow(ApprovalCircuitResponse circuit)
    {
        var orderedSteps = circuit.Steps.OrderBy(step => step.Rank).ToArray();

        // "1. Visa DEC › 2. Signature direction" : l'ordre des etapes se lit dans la grille.
        var stepsDisplay = orderedSteps.Length == 0
            ? "Aucune étape"
            : string.Join(" › ", orderedSteps.Select(step => $"{step.Rank}. {step.Label}"));

        return new CircuitRowView(
            circuit,
            circuit.Code,
            circuit.Label,
            ApprovalDisplay.SubjectLabel(circuit.SubjectType),
            stepsDisplay,
            circuit.IsActive);
    }

    private static HistoryRowView ToHistoryRow(ApprovalInstanceResponse instance)
    {
        var decidedCount = instance.Steps.Count(step => step.IsDecided);

        return new HistoryRowView(
            instance,
            instance.Id,
            ApprovalDisplay.SubjectLabel(instance.SubjectType),
            instance.SubjectReference,
            instance.CircuitLabel,
            ApprovalDisplay.StatusLabel(instance.Status),
            $"{decidedCount}/{instance.Steps.Count} étapes",
            FormatLocal(instance.CreatedAt),
            instance.ClosedAt.HasValue ? FormatLocal(instance.ClosedAt.Value) : string.Empty);
    }

    private sealed record PendingRowView(
        Guid Id,
        string SubjectLabel,
        string SubjectReference,
        string CircuitLabel,
        string CurrentStepDisplay,
        string CurrentRoleLabel,
        string OpenedAtDisplay);

    private sealed record CircuitRowView(
        ApprovalCircuitResponse Source,
        string Code,
        string Label,
        string SubjectLabel,
        string StepsDisplay,
        bool IsActive);

    private sealed record HistoryRowView(
        ApprovalInstanceResponse Source,
        Guid Id,
        string SubjectLabel,
        string SubjectReference,
        string CircuitLabel,
        string StatusLabel,
        string ProgressDisplay,
        string OpenedAtDisplay,
        string ClosedAtDisplay);

    private sealed record DecisionRowView(
        int Rank,
        string StepLabel,
        string VerdictLabel,
        string DecidedBy,
        string DecidedAtDisplay,
        string? Comment);

    private sealed record StepDraftRow(int Rank, string Label, string Role, string RoleLabel);
}

/// <summary>
/// Source unique des libelles francais du module : la grille, les messages et les
/// confirmations rendent le meme mot pour une meme valeur (statuts, sujets, roles).
/// Seul l'affichage est traduit, la valeur envoyee a l'API reste celle du domaine.
/// </summary>
internal static class ApprovalDisplay
{
    public sealed record SubjectOption(ApprovalSubjectType Value, string Label);

    public sealed record SubjectFilterOption(ApprovalSubjectType? Value, string Label);

    public sealed record StatusFilterOption(ApprovalInstanceStatus? Value, string Label);

    public sealed record RoleOption(string Value, string Label);

    public static readonly IReadOnlyList<SubjectOption> SubjectOptions =
    [
        new(ApprovalSubjectType.PaymentOrder, "Ordre de paiement")
    ];

    public static readonly IReadOnlyList<SubjectFilterOption> SubjectFilterOptions =
    [
        new(null, "Tous les sujets"),
        new(ApprovalSubjectType.PaymentOrder, "Ordre de paiement")
    ];

    public static readonly IReadOnlyList<StatusFilterOption> StatusFilterOptions =
    [
        new(null, "Tous les statuts"),
        new(ApprovalInstanceStatus.InProgress, "En cours"),
        new(ApprovalInstanceStatus.Approved, "Approuvée"),
        new(ApprovalInstanceStatus.Rejected, "Rejetée")
    ];

    // Les roles eligibles viennent du domaine (ApprovalStep.AllowedRoles) : la liste
    // n'est jamais recopiee, seuls les libelles sont traduits ici.
    public static readonly IReadOnlyList<RoleOption> RoleOptions = ApprovalStep.AllowedRoles
        .Select(role => new RoleOption(role, RoleLabel(role)))
        .ToArray();

    public static string StatusLabel(ApprovalInstanceStatus status)
    {
        return status switch
        {
            ApprovalInstanceStatus.InProgress => "En cours",
            ApprovalInstanceStatus.Approved => "Approuvée",
            ApprovalInstanceStatus.Rejected => "Rejetée",
            _ => status.ToString()
        };
    }

    public static string SubjectLabel(ApprovalSubjectType subjectType)
    {
        var option = SubjectOptions.FirstOrDefault(item => item.Value == subjectType);

        return option?.Label ?? subjectType.ToString();
    }

    public static string RoleLabel(string? role)
    {
        return role switch
        {
            RoleCatalog.SystemAdministrator => "Administrateur système",
            RoleCatalog.Direction => "Direction",
            RoleCatalog.ExploitationControl => "Exploitation et contrôle",
            RoleCatalog.UnitManager => "Responsable unité",
            RoleCatalog.Cashier => "Caissier",
            RoleCatalog.Reader => "Lecture seule",
            null => string.Empty,
            _ => role
        };
    }
}
