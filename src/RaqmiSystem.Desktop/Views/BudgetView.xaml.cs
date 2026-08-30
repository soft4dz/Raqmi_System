using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using RaqmiSystem.Application.Budgeting;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Domain.Budgeting;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module Budget & previsions : le plan budgetaire d'un exercice (12 mois x 4
/// categories de recettes) pour une unite hoteliere, et la confrontation de ce
/// budget au realise, mois par mois et categorie par categorie.
///
/// Vue autonome : elle ne connait ni MainWindow ni les autres modules, et passe
/// par <see cref="ModuleViewContext.RunAsync"/> pour tout appel API (curseur
/// d'attente, barre de progression, traduction des erreurs).
/// </summary>
public partial class BudgetView : UserControl
{
    private const string WritePermissionHint =
        "Permission requise : budget.write. Votre profil ne peut que consulter les budgets.";

    private const string ApprovePermissionHint =
        "Permission requise : budget.approve. Votre profil ne peut pas approuver un budget.";

    /// <summary>
    /// Capacite de la colonne amount_target (numeric(18,2), voir
    /// BudgetLineConfiguration). Au-dela, PostgreSQL refuse la valeur : la saisie
    /// est bornee ici, avec un message explicite plutot qu'une erreur serveur
    /// apres l'aller-retour.
    /// </summary>
    private const decimal MaxMoney = 9_999_999_999_999_999.99m;

    /// <summary>
    /// Libelles francais des mois. Ecrits en clair plutot que tires de la culture
    /// du poste : l'application est en francais quelle que soit la culture
    /// systeme, et un poste en anglais afficherait sinon "January".
    /// </summary>
    private static readonly string[] MonthLabels =
    [
        "Janvier", "Février", "Mars", "Avril", "Mai", "Juin",
        "Juillet", "Août", "Septembre", "Octobre", "Novembre", "Décembre"
    ];

    /// <summary>
    /// Les quatre categories budgetaires, dans l'ordre d'affichage. Elles sont le
    /// miroir exact des quatre colonnes de montant d'une recette journaliere :
    /// c'est ce qui rend la confrontation budget / realise possible.
    /// </summary>
    private static readonly (BudgetCategory Category, string Label)[] CategoryLabels =
    [
        (BudgetCategory.Accommodation, "Hébergement"),
        (BudgetCategory.Food, "Restauration"),
        (BudgetCategory.Beverage, "Boissons"),
        (BudgetCategory.Other, "Autres")
    ];

    private readonly ObservableCollection<BudgetMonthEditorRow> planRows = [];

    // Info-bulles d'origine des boutons, capturees avant toute substitution. Les
    // vues de module survivent a la deconnexion et sont reinitialisees sur les
    // memes instances : un message "permission requise" pose pour un profil doit
    // disparaitre pour le profil suivant, sinon il persiste a tort pour un
    // utilisateur qui, lui, detient le droit.
    private readonly Dictionary<Button, object?> originalToolTips = [];

    private ModuleViewContext? context;

    // Plan de l'exercice affiche, ou null quand l'annee et l'unite retenues n'en
    // ont pas encore. C'est lui qui commande la modifiabilite de la grille.
    private BudgetPlanResponse? currentPlan;

    // Droits du profil connecte, memorises a l'ouverture de la session. Les
    // actions d'ecriture sont grisees quand le droit manque, plutot que de
    // laisser l'utilisateur decouvrir un 403 apres avoir saisi toute une grille.
    // Le serveur reste la seule autorite : ceci n'est qu'un confort d'interface.
    private bool canWrite = true;

    // L'approbation releve d'un droit distinct de l'ecriture (budget.approve) :
    // un profil write-sans-approve saisit la grille mais ne peut pas la figer.
    private bool canApprove = true;

    // Vrai le temps d'un rebind ou d'une remise a zero des filtres : leurs
    // gestionnaires ne doivent en aucun cas relancer un chargement pendant qu'un
    // chargement est justement en train de les reconstruire.
    private bool suspendFilterReload;

    public BudgetView()
    {
        InitializeComponent();

        // Les formats {0:N2} des grilles suivent la culture de l'utilisateur,
        // comme les montants formates dans le code-behind.
        var languageTag = CultureInfo.CurrentCulture.IetfLanguageTag;

        if (!string.IsNullOrEmpty(languageTag))
        {
            Language = XmlLanguage.GetLanguage(languageTag);
        }

        PlanLinesDataGrid.ItemsSource = planRows;

        BuildYearOptions();
        ApplyPlan(null);
    }

    /// <summary>Memorise le contexte prete par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext moduleViewContext)
    {
        context = moduleViewContext;
        canWrite = moduleViewContext.HasPermission(PermissionCatalog.BudgetWrite);
        canApprove = moduleViewContext.HasPermission(PermissionCatalog.BudgetApprove);

        UpdateActionState();
    }

    /// <summary>
    /// (Re)charge les unites, le plan de l'exercice choisi et ses ecarts. Sort
    /// silencieusement tant qu'aucun contexte n'a ete fourni ou que la session
    /// n'est pas ouverte.
    /// </summary>
    public async Task LoadAsync()
    {
        if (context is not { } active || !active.ApiClient.IsAuthenticated)
        {
            return;
        }

        await active.RunAsync(() => LoadEverythingAsync(active));
    }

    /// <summary>
    /// Vide toutes les surfaces de la vue : appelee a la deconnexion pour ne
    /// jamais laisser les donnees d'un utilisateur affichees pour le suivant.
    /// </summary>
    public void ResetState()
    {
        suspendFilterReload = true;

        try
        {
            UnitComboBox.ItemsSource = null;
            PlanLabelTextBox.Text = string.Empty;

            BuildYearOptions();
            ApplyPlan(null);
            ClearVariance("Sélectionnez une année et une unité hôtelière, puis actualisez.");
        }
        finally
        {
            suspendFilterReload = false;
        }
    }

    // ============================== Chargements ==============================

    private async Task LoadEverythingAsync(ModuleViewContext active)
    {
        await LoadHotelUnitsAsync(active);

        var year = SelectedYear();
        var unitCode = (UnitComboBox.SelectedItem as BudgetUnitOption)?.Code;

        if (year is null || string.IsNullOrWhiteSpace(unitCode))
        {
            ApplyPlan(null);
            ClearVariance("Aucune unité hôtelière active : le budget se pilote unité par unité.");
            active.SetStatus("Aucune unité hôtelière active à budgéter.", isError: true);
            return;
        }

        // Le serveur garantit au plus un plan par (annee, unite) : la liste filtree
        // sur ce couple ne peut donc en rendre qu'un seul, ou aucun.
        var plans = await active.ApiClient.GetBudgetPlansAsync(active.ApiBaseUrl, year, unitCode);
        var plan = plans.FirstOrDefault();

        ApplyPlan(plan);

        if (plan is null)
        {
            // Sans plan, l'API des ecarts repond 404 plutot que d'inventer une
            // grille d'objectifs a zero : l'appel n'est pas tente.
            ClearVariance("Sans plan budgétaire pour cette année et cette unité, il n'y a rien à confronter au réalisé. Créez le plan dans l'onglet « Plan budgétaire ».");
            active.SetStatus($"Aucun plan budgétaire pour {year} — unité {unitCode}.");
            return;
        }

        var variance = await active.ApiClient.GetBudgetVarianceAsync(active.ApiBaseUrl, year.Value, unitCode);

        ApplyVariance(variance, unitCode);

        active.SetStatus($"Budget {year} de l'unité {unitCode} chargé : {DescribeStatus(plan.Status).ToLower(CultureInfo.CurrentCulture)}.");
    }

    private async Task LoadHotelUnitsAsync(ModuleViewContext active)
    {
        IReadOnlyList<HotelUnitResponse> units =
            (await active.ApiClient.GetHotelUnitsAsync(active.ApiBaseUrl, includeInactive: false))
            .Where(unit => unit.IsActive)
            .OrderBy(unit => unit.DisplayOrder)
            .ThenBy(unit => unit.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        var options = units
            .Select(unit => new BudgetUnitOption(unit.Code, $"{unit.Code} — {unit.Name}"))
            .ToList();

        // La selection en cours est conservee quand le code existe toujours : un
        // rechargement ne doit pas ramener l'operateur sur une autre unite.
        var previousCode = (UnitComboBox.SelectedItem as BudgetUnitOption)?.Code;

        // Le drapeau est REMIS a sa valeur precedente, pas force a faux : la
        // remise a zero de la deconnexion appelle ce code depuis sa propre
        // suspension, qu'un simple "false" leverait trop tot.
        var previousSuspend = suspendFilterReload;
        suspendFilterReload = true;

        try
        {
            UnitComboBox.ItemsSource = options;
            UnitComboBox.SelectedItem = options.FirstOrDefault(option =>
                string.Equals(option.Code, previousCode, StringComparison.OrdinalIgnoreCase))
                ?? options.FirstOrDefault();
        }
        finally
        {
            suspendFilterReload = previousSuspend;
        }
    }

    /// <summary>
    /// Exercices proposes : de trois ans en arriere a deux ans en avant, l'annee
    /// courante preselectionnee. Le domaine accepte 2000 a 2999 ; proposer toute
    /// la plage n'aiderait personne.
    /// </summary>
    private void BuildYearOptions()
    {
        var currentYear = DateTime.Today.Year;
        var years = Enumerable.Range(currentYear - 3, 6).ToArray();

        var previousSuspend = suspendFilterReload;
        suspendFilterReload = true;

        try
        {
            YearComboBox.ItemsSource = years;
            YearComboBox.SelectedItem = currentYear;
        }
        finally
        {
            suspendFilterReload = previousSuspend;
        }
    }

    private int? SelectedYear()
    {
        return YearComboBox.SelectedItem as int?;
    }

    private async void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suspendFilterReload)
        {
            return;
        }

        await LoadAsync();
    }

    private async void RefreshBudgetButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    // ============================ Plan budgetaire ============================

    /// <summary>
    /// Applique le plan (ou son absence) a tout l'onglet : badge d'etat, bandeau
    /// de budget fige, formulaire de creation, grille des objectifs et totaux.
    /// </summary>
    private void ApplyPlan(BudgetPlanResponse? plan)
    {
        currentPlan = plan;

        ApplyPlanBadge(plan);

        CreatePlanPanel.Visibility = plan is null ? Visibility.Visible : Visibility.Collapsed;

        // Un budget approuve est en lecture seule. Le dire a l'ecran, en toutes
        // lettres : des champs grises sans explication laissent croire a une
        // panne, alors qu'il s'agit d'une regle - la reference contre laquelle
        // les ecarts sont mesures ne peut plus etre reecrite.
        var frozen = plan is not null && !plan.CanEdit;

        FrozenPlanPanel.Visibility = frozen ? Visibility.Visible : Visibility.Collapsed;

        if (frozen)
        {
            FrozenPlanTextBlock.Text = plan!.Status == BudgetStatus.Closed
                ? $"Cet exercice budgétaire est clôturé{DescribeActor(plan.ClosedAt, plan.ClosedBy)} : le budget est définitivement figé. La grille ci-dessous est présentée en lecture seule."
                : $"Ce budget est approuvé{DescribeActor(plan.ApprovedAt, plan.ApprovedBy)} : il est figé et ne peut plus être modifié. La grille ci-dessous est présentée en lecture seule — les écarts mesurés contre lui perdraient leur sens si la référence pouvait encore être réécrite.";
        }

        PlanHintTextBlock.Text = plan switch
        {
            null => "Créez le plan de l'exercice pour saisir les objectifs mensuels.",
            { CanEdit: true } => "Un objectif par mois et par catégorie de recettes. Une cellule laissée vide ne crée aucun objectif ; saisissez 0 pour fixer explicitement un objectif nul.",
            _ => "Budget figé : les objectifs sont présentés en lecture seule."
        };

        BuildPlanRows(plan);
        UpdatePlanTotals();
        UpdateActionState();
    }

    private void ApplyPlanBadge(BudgetPlanResponse? plan)
    {
        var (background, foreground, label) = plan?.Status switch
        {
            BudgetStatus.Draft => ("StatusDraftBackgroundBrush", "StatusDraftForegroundBrush", "Brouillon"),
            BudgetStatus.Approved => ("StatusValidatedBackgroundBrush", "StatusValidatedForegroundBrush", "Approuvé"),
            BudgetStatus.Closed => ("AccentSoftBrush", "ModuleStatusApiForegroundBrush", "Clôturé"),
            _ => ("StatusDraftBackgroundBrush", "StatusDraftForegroundBrush", "Aucun plan")
        };

        if (TryFindResource(background) is Brush backgroundBrush)
        {
            PlanStatusBadge.Background = backgroundBrush;
        }

        if (TryFindResource(foreground) is Brush foregroundBrush)
        {
            PlanStatusBadgeText.Foreground = foregroundBrush;
        }

        PlanStatusBadgeText.Text = label;

        PlanSummaryTextBlock.Text = plan is null
            ? "Aucun plan budgétaire pour cette année et cette unité."
            : $"{plan.Label} — objectifs {FormatAmount(plan.TotalTarget)}";
        PlanSummaryTextBlock.ToolTip = plan is null ? null : plan.Label;
    }

    /// <summary>
    /// Reconstruit les douze lignes de saisie a partir des objectifs du plan. Les
    /// mois et les categories absents du plan restent vides : une cellule vide
    /// dit "aucun objectif fixe", ce qu'un 0 affiche ne dirait pas.
    /// </summary>
    private void BuildPlanRows(BudgetPlanResponse? plan)
    {
        DetachPlanRows();
        planRows.Clear();

        if (plan is null)
        {
            return;
        }

        var editable = plan.CanEdit && canWrite;

        for (var month = 1; month <= 12; month++)
        {
            var row = new BudgetMonthEditorRow(month, MonthLabels[month - 1]) { IsEditable = editable };

            foreach (var line in plan.Lines.Where(current => current.Month == month))
            {
                row.SetAmount(line.Category, line.AmountTarget);
            }

            row.PropertyChanged += PlanRow_PropertyChanged;
            planRows.Add(row);
        }
    }

    private void DetachPlanRows()
    {
        foreach (var row in planRows)
        {
            row.PropertyChanged -= PlanRow_PropertyChanged;
        }
    }

    private void PlanRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdatePlanTotals();
    }

    /// <summary>
    /// Totaux par categorie sur l'exercice, recalcules a chaque frappe. Les
    /// totaux par mois sont portes par la colonne "Total du mois" de la grille.
    /// Une cellule illisible compte pour zero ici : le controle de saisie la
    /// refuse a l'enregistrement, avec un message qui la designe.
    /// </summary>
    private void UpdatePlanTotals()
    {
        var accommodation = 0m;
        var food = 0m;
        var beverage = 0m;
        var other = 0m;

        foreach (var row in planRows)
        {
            accommodation += row.AmountOrZero(BudgetCategory.Accommodation);
            food += row.AmountOrZero(BudgetCategory.Food);
            beverage += row.AmountOrZero(BudgetCategory.Beverage);
            other += row.AmountOrZero(BudgetCategory.Other);
        }

        PlanAccommodationTotalTextBlock.Text = FormatAmount(accommodation);
        PlanFoodTotalTextBlock.Text = FormatAmount(food);
        PlanBeverageTotalTextBlock.Text = FormatAmount(beverage);
        PlanOtherTotalTextBlock.Text = FormatAmount(other);
        PlanGrandTotalTextBlock.Text = FormatAmount(accommodation + food + beverage + other);
    }

    private async void CreatePlanButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active)
        {
            return;
        }

        var year = SelectedYear();
        var unitCode = (UnitComboBox.SelectedItem as BudgetUnitOption)?.Code;

        if (year is null || string.IsNullOrWhiteSpace(unitCode))
        {
            active.SetStatus("Sélectionnez l'année et l'unité hôtelière du plan.", isError: true);
            return;
        }

        var label = PlanLabelTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(label))
        {
            active.SetStatus("Le libellé du plan budgétaire est obligatoire.", isError: true);
            PlanLabelTextBox.Focus();
            return;
        }

        await active.RunAsync(async () =>
        {
            var created = await active.ApiClient.CreateBudgetPlanAsync(
                active.ApiBaseUrl,
                new CreateBudgetPlanRequest(year.Value, unitCode, label));

            PlanLabelTextBox.Text = string.Empty;

            ApplyPlan(created);

            var variance = await active.ApiClient.GetBudgetVarianceAsync(active.ApiBaseUrl, year.Value, unitCode);
            ApplyVariance(variance, unitCode);

            active.SetStatus($"Plan budgétaire {created.Year} créé en brouillon pour l'unité {created.HotelUnitCode}.");
        });
    }

    private async void SavePlanLinesButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || currentPlan is not { } plan)
        {
            return;
        }

        if (!plan.CanEdit)
        {
            active.SetStatus("Ce budget est approuvé : il est figé et ne peut plus être modifié.", isError: true);
            return;
        }

        await active.RunAsync(async () =>
        {
            if (!TryBuildLines(active, out var lines))
            {
                return;
            }

            var updated = await active.ApiClient.ReplaceBudgetPlanLinesAsync(
                active.ApiBaseUrl,
                plan.Id,
                new ReplaceBudgetLinesRequest(lines));

            ApplyPlan(updated);

            var variance = await active.ApiClient.GetBudgetVarianceAsync(
                active.ApiBaseUrl,
                updated.Year,
                updated.HotelUnitCode);

            ApplyVariance(variance, updated.HotelUnitCode);

            active.SetStatus($"Grille enregistrée : {lines.Count} objectif(s), total {FormatAmount(updated.TotalTarget)}.");
        });
    }

    /// <summary>
    /// Controle de saisie aligne sur les regles du domaine (BudgetLine) : montant
    /// positif ou nul, deux decimales au maximum, dans la capacite de la colonne.
    /// Une cellule vide ne produit aucune ligne - c'est ce qui distingue "aucun
    /// objectif fixe" d'un objectif volontairement nul.
    /// </summary>
    private bool TryBuildLines(ModuleViewContext active, out IReadOnlyCollection<BudgetLineRequest> lines)
    {
        lines = [];

        var result = new List<BudgetLineRequest>();

        foreach (var row in planRows)
        {
            foreach (var (category, text) in row.Cells())
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var cellName = $"{row.MonthLabel} — {DescribeCategory(category)}";

                if (!BudgetMonthEditorRow.TryParseAmount(text, out var amount))
                {
                    active.SetStatus($"{cellName} : l'objectif doit être un montant valide.", isError: true);
                    return false;
                }

                if (amount < 0)
                {
                    active.SetStatus($"{cellName} : un objectif ne peut pas être négatif.", isError: true);
                    return false;
                }

                if (decimal.Round(amount, 2) != amount)
                {
                    active.SetStatus($"{cellName} : l'objectif accepte 2 décimales au maximum.", isError: true);
                    return false;
                }

                if (amount > MaxMoney)
                {
                    active.SetStatus(
                        $"{cellName} : l'objectif ne peut pas dépasser {MaxMoney.ToString("N2", CultureInfo.CurrentCulture)}.",
                        isError: true);
                    return false;
                }

                result.Add(new BudgetLineRequest(row.Month, category, amount));
            }
        }

        if (result.Count == 0)
        {
            active.SetStatus("Saisissez au moins un objectif : un budget sans aucune ligne n'engage à rien.", isError: true);
            return false;
        }

        lines = result;
        return true;
    }

    private async void ApprovePlanButton_Click(object sender, RoutedEventArgs e)
    {
        if (context is not { } active || currentPlan is not { } plan)
        {
            return;
        }

        if (!plan.CanEdit)
        {
            active.SetStatus("Seul un budget en brouillon peut être approuvé.", isError: true);
            return;
        }

        if (plan.Lines.Count == 0)
        {
            active.SetStatus("Un budget doit porter au moins un objectif enregistré pour être approuvé.", isError: true);
            return;
        }

        var confirmed = Confirm(
            $"Approuver le budget « {plan.Label} » de l'exercice {plan.Year} pour l'unité {plan.HotelUnitCode} ?"
            + Environment.NewLine + Environment.NewLine
            + "L'approbation fige le budget :"
            + Environment.NewLine
            + "• les objectifs ne pourront plus être modifiés ;"
            + Environment.NewLine
            + "• tous les écarts de l'exercice seront mesurés contre ces montants ;"
            + Environment.NewLine
            + "• revenir en arrière supposerait de réécrire la référence, ce que le système n'autorise pas."
            + Environment.NewLine + Environment.NewLine
            + $"Total des objectifs : {FormatAmount(plan.TotalTarget)}"
            + Environment.NewLine + Environment.NewLine
            + "Approuver ce budget ?",
            "Approuver le budget");

        if (!confirmed)
        {
            return;
        }

        await active.RunAsync(async () =>
        {
            var approved = await active.ApiClient.ApproveBudgetPlanAsync(active.ApiBaseUrl, plan.Id);

            ApplyPlan(approved);

            var variance = await active.ApiClient.GetBudgetVarianceAsync(
                active.ApiBaseUrl,
                approved.Year,
                approved.HotelUnitCode);

            ApplyVariance(variance, approved.HotelUnitCode);

            active.SetStatus($"Budget {approved.Year} de l'unité {approved.HotelUnitCode} approuvé et figé.");
        });
    }

    // =========================== Realise et ecarts ===========================

    /// <summary>
    /// Deplie la reponse d'ecarts en lignes de tableau : les quatre categories de
    /// chaque mois, suivies du total du mois. Les totaux de l'exercice sont
    /// portes par le bandeau du bas.
    /// </summary>
    private void ApplyVariance(BudgetVarianceResponse variance, string unitCode)
    {
        var rows = new List<BudgetVarianceRowView>();

        foreach (var month in variance.Months.OrderBy(current => current.Month))
        {
            var monthLabel = MonthLabels[Math.Clamp(month.Month, 1, 12) - 1];

            foreach (var (category, label) in CategoryLabels)
            {
                var cell = month.Categories.FirstOrDefault(current => current.Category == category);

                if (cell is null)
                {
                    continue;
                }

                rows.Add(BuildVarianceRow(
                    monthLabel,
                    label,
                    cell.BudgetAmount,
                    cell.ActualAmount,
                    cell.VarianceAmount,
                    cell.VariancePercentage,
                    isMonthTotal: false));
            }

            rows.Add(BuildVarianceRow(
                monthLabel,
                "Total du mois",
                month.BudgetAmount,
                month.ActualAmount,
                month.VarianceAmount,
                month.VariancePercentage,
                isMonthTotal: true));
        }

        VarianceDataGrid.ItemsSource = rows;

        // Un budget encore en brouillon peut changer a tout moment : les ecarts
        // mesures contre lui sont des chiffres de travail, pas une mesure contre
        // un engagement. Le lecteur doit pouvoir faire la difference.
        DraftPlanNoticePanel.Visibility = variance.PlanStatus == BudgetStatus.Draft
            ? Visibility.Visible
            : Visibility.Collapsed;

        VarianceScopeTextBlock.Text =
            $"Exercice {variance.Year}, unité {unitCode} — budget {DescribeStatus(variance.PlanStatus).ToLower(CultureInfo.CurrentCulture)}. Le réalisé ne retient que les recettes journalières validées.";

        TotalBudgetTextBlock.Text = FormatAmount(variance.BudgetAmount);
        TotalActualTextBlock.Text = FormatAmount(variance.ActualAmount);
        TotalVarianceTextBlock.Text = FormatSignedAmount(variance.VarianceAmount);
        TotalVariancePercentageTextBlock.Text = FormatPercentage(variance.VariancePercentage);

        ApplyVarianceTotalColour(variance.VarianceAmount);
    }

    /// <summary>
    /// Couleur semantique du total : rejet quand l'exercice est sous son
    /// objectif, validation quand il est au-dessus, neutre a l'equilibre. Les
    /// pinceaux viennent du theme, aucune couleur n'est ecrite ici.
    /// </summary>
    private void ApplyVarianceTotalColour(decimal varianceAmount)
    {
        var key = varianceAmount switch
        {
            < 0 => "StatusRejectedForegroundBrush",
            > 0 => "StatusValidatedForegroundBrush",
            _ => "TextPrimaryBrush"
        };

        if (TryFindResource(key) is Brush brush)
        {
            TotalVarianceTextBlock.Foreground = brush;
            TotalVariancePercentageTextBlock.Foreground = brush;
        }
    }

    private void ClearVariance(string emptyHint)
    {
        VarianceDataGrid.ItemsSource = null;
        DraftPlanNoticePanel.Visibility = Visibility.Collapsed;

        VarianceEmptyTitleTextBlock.Text = "Aucun écart à afficher";
        VarianceEmptyHintTextBlock.Text = emptyHint;

        VarianceScopeTextBlock.Text = "Aucun budget de référence pour cette année et cette unité.";
        TotalBudgetTextBlock.Text = "—";
        TotalActualTextBlock.Text = "—";
        TotalVarianceTextBlock.Text = "—";
        TotalVariancePercentageTextBlock.Text = "—";

        ApplyVarianceTotalColour(0m);
    }

    private static BudgetVarianceRowView BuildVarianceRow(
        string monthLabel,
        string categoryLabel,
        decimal budgetAmount,
        decimal actualAmount,
        decimal varianceAmount,
        decimal? variancePercentage,
        bool isMonthTotal)
    {
        // Ce sont des recettes : un ecart negatif signifie que l'unite est restee
        // sous son objectif, donc defavorable. Le signe suffit a le dire, la
        // couleur ne fait que le rendre reperable d'un coup d'oeil.
        var unfavourable = varianceAmount < 0;
        var favourable = varianceAmount > 0;

        var varianceTooltip = unfavourable
            ? "Écart défavorable : le réalisé validé est inférieur à l'objectif du mois."
            : favourable
                ? "Écart favorable : le réalisé validé dépasse l'objectif du mois."
                : "Le réalisé validé est exactement à l'objectif.";

        var percentageTooltip = variancePercentage is null
            ? "Aucun objectif n'a été fixé pour cette cellule : un écart relatif n'aurait aucune référence, il n'est donc pas calculé. L'écart en valeur, lui, reste affiché."
            : "Écart rapporté à l'objectif du mois.";

        return new BudgetVarianceRowView(
            monthLabel,
            categoryLabel,
            FormatAmount(budgetAmount),
            FormatAmount(actualAmount),
            FormatSignedAmount(varianceAmount),
            FormatPercentage(variancePercentage),
            varianceTooltip,
            percentageTooltip,
            unfavourable,
            favourable,
            isMonthTotal);
    }

    // ============================ Etat des actions ============================

    /// <summary>
    /// Etat metier du plan croise avec les droits du profil. Pose le message
    /// d'explication quand le droit manque et RESTAURE l'info-bulle d'origine
    /// quand il est present : l'affectation doit etre symetrique, sinon un
    /// message pose pour un profil restreint survit a la reconnexion d'un profil
    /// qui, lui, detient le droit.
    /// </summary>
    private void ApplyPermissionHint(Button button, bool allowed, string hint)
    {
        if (!originalToolTips.ContainsKey(button))
        {
            originalToolTips[button] = button.ToolTip;
        }

        button.ToolTip = allowed ? originalToolTips[button] : hint;
    }

    private void UpdateActionState()
    {
        var plan = currentPlan;
        var hasUnit = UnitComboBox.SelectedItem is BudgetUnitOption;

        CreatePlanButton.IsEnabled = canWrite && plan is null && hasUnit;
        PlanLabelTextBox.IsEnabled = canWrite && plan is null;

        SavePlanLinesButton.IsEnabled = canWrite && plan is { CanEdit: true };

        // Le domaine refuse d'approuver un plan sans aucune ligne : le bouton
        // reste gris tant que la grille enregistree est vide, plutot que d'ouvrir
        // une confirmation vouee a un refus.
        ApprovePlanButton.IsEnabled = canApprove && plan is { CanEdit: true, Lines.Count: > 0 };

        ApplyPermissionHint(CreatePlanButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(SavePlanLinesButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(ApprovePlanButton, canApprove, ApprovePermissionHint);
    }

    // ================================ Outils =================================

    private bool Confirm(string message, string caption)
    {
        var owner = Window.GetWindow(this);

        var result = owner is null
            ? MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(owner, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    private static string DescribeStatus(BudgetStatus status)
    {
        return status switch
        {
            BudgetStatus.Draft => "Brouillon",
            BudgetStatus.Approved => "Approuvé",
            BudgetStatus.Closed => "Clôturé",
            _ => status.ToString()
        };
    }

    private static string DescribeCategory(BudgetCategory category)
    {
        foreach (var (value, label) in CategoryLabels)
        {
            if (value == category)
            {
                return label;
            }
        }

        return category.ToString();
    }

    // Complement "le JJ/MM/AAAA HH:mm par X" quand la tracabilite est connue, et
    // rien du tout sinon : une phrase avec des tirets a la place des valeurs
    // manquantes se lit plus mal qu'une phrase plus courte.
    private static string DescribeActor(DateTimeOffset? moment, string? actor)
    {
        if (moment is not DateTimeOffset value)
        {
            return string.Empty;
        }

        var formatted = value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);

        return string.IsNullOrWhiteSpace(actor)
            ? $" le {formatted}"
            : $" le {formatted} par {actor}";
    }

    private static string FormatAmount(decimal value)
    {
        return value.ToString("N2", CultureInfo.CurrentCulture);
    }

    // Le signe est explicite y compris quand l'ecart est favorable : "+12 500,00"
    // se lit sans ambiguite, "12 500,00" pourrait passer pour un montant realise.
    private static string FormatSignedAmount(decimal value)
    {
        var formatted = value.ToString("N2", CultureInfo.CurrentCulture);

        return value > 0 ? "+" + formatted : formatted;
    }

    /// <summary>
    /// Pourcentage d'ecart, ou un tiret quand il n'existe pas. Le serveur rend
    /// null lorsque l'objectif est nul : il n'y a alors aucune reference a
    /// laquelle rapporter l'ecart, et afficher 0 % laisserait croire que la
    /// cellule est "dans les clous".
    /// </summary>
    private static string FormatPercentage(decimal? value)
    {
        if (value is not decimal percentage)
        {
            return "—";
        }

        var formatted = percentage.ToString("N2", CultureInfo.CurrentCulture);

        return (percentage > 0 ? "+" + formatted : formatted) + " %";
    }
}

/// <summary>Option de la liste des unites hotelieres de cet ecran.</summary>
public sealed record BudgetUnitOption(string Code, string Label);

/// <summary>
/// Une ligne de la grille de saisie : un mois, et l'objectif de chacune des
/// quatre categories de recettes. Les montants sont conserves sous forme de
/// texte pour accepter la virgule comme le point pendant la frappe, et pour
/// distinguer une cellule vide (aucun objectif fixe) d'un objectif nul saisi
/// volontairement ; la conversion et les controles ont lieu a l'enregistrement.
/// </summary>
public sealed class BudgetMonthEditorRow(int month, string monthLabel) : INotifyPropertyChanged
{
    private string accommodationText = string.Empty;
    private string foodText = string.Empty;
    private string beverageText = string.Empty;
    private string otherText = string.Empty;
    private bool isEditable;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Month { get; } = month;

    public string MonthLabel { get; } = monthLabel;

    /// <summary>
    /// Faux quand le plan est approuve ou que le profil n'a pas budget.write :
    /// les champs de la ligne cessent alors de repondre. Le bandeau de l'ecran
    /// dit pourquoi - un champ gris seul n'explique rien.
    /// </summary>
    public bool IsEditable
    {
        get => isEditable;
        set => SetField(ref isEditable, value);
    }

    public string AccommodationText
    {
        get => accommodationText;
        set => SetAmountField(ref accommodationText, value);
    }

    public string FoodText
    {
        get => foodText;
        set => SetAmountField(ref foodText, value);
    }

    public string BeverageText
    {
        get => beverageText;
        set => SetAmountField(ref beverageText, value);
    }

    public string OtherText
    {
        get => otherText;
        set => SetAmountField(ref otherText, value);
    }

    /// <summary>
    /// Total du mois recalcule a chaque frappe. Une cellule illisible compte pour
    /// zero : l'apercu reste affichable, et le controle de saisie refuse la
    /// cellule a l'enregistrement avec un message qui la designe.
    /// </summary>
    public string MonthTotalText => (AmountOrZero(BudgetCategory.Accommodation)
        + AmountOrZero(BudgetCategory.Food)
        + AmountOrZero(BudgetCategory.Beverage)
        + AmountOrZero(BudgetCategory.Other))
        .ToString("N2", CultureInfo.CurrentCulture);

    public void SetAmount(BudgetCategory category, decimal amount)
    {
        var text = amount.ToString("0.00", CultureInfo.CurrentCulture);

        switch (category)
        {
            case BudgetCategory.Accommodation:
                AccommodationText = text;
                break;
            case BudgetCategory.Food:
                FoodText = text;
                break;
            case BudgetCategory.Beverage:
                BeverageText = text;
                break;
            case BudgetCategory.Other:
                OtherText = text;
                break;
        }
    }

    public IEnumerable<(BudgetCategory Category, string Text)> Cells()
    {
        yield return (BudgetCategory.Accommodation, AccommodationText);
        yield return (BudgetCategory.Food, FoodText);
        yield return (BudgetCategory.Beverage, BeverageText);
        yield return (BudgetCategory.Other, OtherText);
    }

    public decimal AmountOrZero(BudgetCategory category)
    {
        var text = category switch
        {
            BudgetCategory.Accommodation => AccommodationText,
            BudgetCategory.Food => FoodText,
            BudgetCategory.Beverage => BeverageText,
            BudgetCategory.Other => OtherText,
            _ => string.Empty
        };

        return TryParseAmount(text, out var amount) ? amount : 0m;
    }

    // Meme tolerance de saisie que les recettes journalieres et la facturation :
    // la virgule et le point sont acceptes, quelle que soit la culture du poste.
    public static bool TryParseAmount(string text, out decimal value)
    {
        var trimmed = text.Trim();

        return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            || decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private void SetAmountField(ref string field, string? value, [CallerMemberName] string? propertyName = null)
    {
        if (SetField(ref field, value ?? string.Empty, propertyName))
        {
            OnPropertyChanged(nameof(MonthTotalText));
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Une ligne du tableau des ecarts : tout y est deja mis en forme, y compris les
/// deux drapeaux de sens (defavorable / favorable) sur lesquels le theme accroche
/// ses couleurs semantiques.
/// </summary>
public sealed record BudgetVarianceRowView(
    string MonthLabel,
    string CategoryLabel,
    string BudgetText,
    string ActualText,
    string VarianceText,
    string VariancePercentageText,
    string VarianceTooltip,
    string VariancePercentageTooltip,
    bool IsUnfavourable,
    bool IsFavourable,
    bool IsMonthTotal);
