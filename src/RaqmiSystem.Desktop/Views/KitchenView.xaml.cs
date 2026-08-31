using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using RaqmiSystem.Application.Kitchen;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Kitchen;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Module Cuisine, production &amp; qualite (11.5), en trois sections internes :
/// fiches techniques (avec le cout matiere par portion calcule par le serveur sur
/// les PMP courants du stock), releves de temperature HACCP et administration des
/// points de controle.
///
/// Vue autonome : elle ne connait ni MainWindow ni les autres vues, tout passe par
/// le ModuleViewContext recu dans Initialize (client API, URL, message d'etat,
/// execution d'un appel avec curseur d'attente).
///
/// La cle d'ecriture vient de PermissionCatalog (KitchenWrite), la meme source que les
/// policies de KitchenEndpoints. La lecture (KitchenRead) est portee par l'onglet lui-meme,
/// verrouille par ApplyModulePermissions.
/// </summary>
public partial class KitchenView : UserControl
{
    private const string KitchenWritePermission = PermissionCatalog.KitchenWrite;

    private const string WritePermissionHint =
        "Permission requise : kitchen.write. Votre profil ne peut que consulter les fiches techniques et les relevés.";

    // Le bouton d'enregistrement d'un releve est aussi grise pour une raison METIER, pas
    // seulement par manque de droit : sans cette explication, l'utilisateur voit un bouton
    // eteint sans savoir qu'il lui manque un point de controle, une temperature lisible ou
    // l'action corrective qu'une valeur hors plage rend obligatoire.
    private const string ReadingIncompleteHint =
        "Relevé incomplet : choisissez un point de contrôle, saisissez une température lisible, "
        + "et décrivez l'action corrective si la valeur sort de la plage de conformité.";

    // Les couts affichés proviennent tous du serveur : ce texte n'est qu'un
    // remplacement lisible tant qu'aucun calcul n'a ete demande.
    private const string NoValuePlaceholder = "—";

    private ModuleViewContext? context;

    // Info-bulles d'origine des boutons d'ecriture, capturees avant toute
    // substitution. Les vues de module survivent a la deconnexion et resservent au
    // profil suivant sur les memes instances : un message "permission requise" pose
    // pour un profil restreint ne doit pas survivre a la reconnexion d'un profil
    // qui, lui, a le droit (motif ApplyPermissionHint).
    private readonly Dictionary<Button, object?> originalToolTips = [];

    // Droit d'ecriture du profil connecte, releve a l'ouverture de la session. Les
    // actions d'ecriture sont grisees quand il manque, plutot que de laisser
    // decouvrir un 403 apres la saisie. Le serveur reste la seule autorite.
    private bool canWrite = true;

    // Lignes d'ingredients en cours de saisie (grille editable).
    private readonly ObservableCollection<KitchenIngredientEditorRow> ingredientRows = [];

    // Null : le formulaire cree une fiche. Renseigne : il modifie la fiche de ce code.
    private string? editingRecipeCode;

    // Null : le formulaire cree un point de controle. Renseigne : il le modifie.
    private string? editingCheckpointCode;

    // Points de controle charges, utilises pour alimenter les listes deroulantes de
    // saisie (actifs seulement) et de filtre (tous ceux qui sont charges).
    private IReadOnlyList<TemperatureCheckpointResponse> checkpoints = [];

    // Vrai le temps de ResetState et des remises a zero de filtres : leurs
    // gestionnaires ne doivent en aucun cas relancer un chargement. Rend le contrat
    // "ResetState vide et ne recharge rien" vrai quel que soit l'ordre d'appel.
    private bool suspendFilterReload;

    // Vrai pendant la restauration de la selection de la grille des fiches : le
    // gestionnaire de selection remplit alors le formulaire sans declencher un
    // second appel reseau imbrique - l'appelant enchaine lui-meme sur le cout.
    private bool suspendSelectionReload;

    public KitchenView()
    {
        InitializeComponent();

        // Les formats {0:N2} / {0:N1} / {0:N3} des grilles suivent la culture de
        // l'utilisateur, comme les montants formates dans le code-behind.
        var languageTag = CultureInfo.CurrentCulture.IetfLanguageTag;

        if (!string.IsNullOrEmpty(languageTag))
        {
            Language = XmlLanguage.GetLanguage(languageTag);
        }

        IngredientsDataGrid.ItemsSource = ingredientRows;

        InitializeDefaults();
        ResetRecipeForm();
        ResetReadingForm();
        ResetCheckpointForm();
        UpdateAllActionStates();
    }

    /// <summary>Memorise le contexte prete par la fenetre. Aucun appel reseau ici.</summary>
    public void Initialize(ModuleViewContext moduleViewContext)
    {
        context = moduleViewContext;
        canWrite = moduleViewContext.HasPermission(KitchenWritePermission);

        UpdateAllActionStates();
    }

    /// <summary>
    /// (Re)charge les trois sections du module. Sortie silencieuse tant que la vue
    /// n'a pas de contexte ou que personne n'est connecte.
    /// </summary>
    public async Task LoadAsync()
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(LoadEverythingAsync);
    }

    /// <summary>Vide grilles, formulaires et compteurs (appelee a la deconnexion).</summary>
    public void ResetState()
    {
        suspendFilterReload = true;

        try
        {
            RecipesDataGrid.ItemsSource = null;
            ReadingsDataGrid.ItemsSource = null;
            CheckpointsDataGrid.ItemsSource = null;
            IngredientCostsDataGrid.ItemsSource = null;

            checkpoints = [];
            ReadingCheckpointComboBox.ItemsSource = null;
            ReadingFilterCheckpointComboBox.ItemsSource = null;

            RecipeSearchTextBox.Text = string.Empty;
            RecipeCategoryFilterComboBox.SelectedIndex = 0;
            IncludeInactiveRecipesCheckBox.IsChecked = false;
            IncludeInactiveCheckpointsCheckBox.IsChecked = false;
            NonCompliantOnlyCheckBox.IsChecked = false;

            RecipeCountTextBlock.Text = "Aucune fiche chargée.";
            ReadingCountTextBlock.Text = "Aucun relevé chargé.";
            CheckpointCountTextBlock.Text = "Aucun point de contrôle chargé.";

            InitializeDefaults();

            ResetRecipeForm();
            ResetReadingForm();
            ResetCheckpointForm();
            ClearRecipeCost();

            UpdateAllActionStates();
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

        // Bornes de calendrier en heure locale du poste (jamais l'horloge UTC).
        ReadingFromDatePicker.SelectedDate = firstDayOfMonth;
        ReadingToDatePicker.SelectedDate = today;
        ReadingMeasuredOnDatePicker.SelectedDate = today;

        if (RecipeCategoryFilterComboBox.ItemsSource is null)
        {
            RecipeCategoryFilterComboBox.ItemsSource = BuildCategoryOptions(includeAll: true);
            RecipeCategoryComboBox.ItemsSource = BuildCategoryOptions(includeAll: false);
        }

        RecipeCategoryFilterComboBox.SelectedIndex = 0;

        if (RecipeCategoryComboBox.SelectedItem is null && RecipeCategoryComboBox.Items.Count > 0)
        {
            RecipeCategoryComboBox.SelectedIndex = 0;
        }
    }

    private static KitchenCategoryOption[] BuildCategoryOptions(bool includeAll)
    {
        var options = new List<KitchenCategoryOption>();

        if (includeAll)
        {
            options.Add(new KitchenCategoryOption(null, "Toutes les catégories"));
        }

        foreach (var category in Enum.GetValues<RecipeCategory>())
        {
            options.Add(new KitchenCategoryOption(category, KitchenLabels.Category(category)));
        }

        return [.. options];
    }

    // =============================== Chargements ===============================

    private async Task LoadEverythingAsync()
    {
        // Les points de controle sont charges en premier : les listes deroulantes de
        // la saisie et du filtre des releves en dependent.
        await LoadCheckpointsAsync();
        await LoadRecipesAsync();
        await LoadReadingsAsync();
    }

    private async Task LoadRecipesAsync()
    {
        var moduleContext = context!;

        var search = RecipeSearchTextBox.Text;
        var category = (RecipeCategoryFilterComboBox.SelectedItem as KitchenCategoryOption)?.Value;
        var includeInactive = IncludeInactiveRecipesCheckBox.IsChecked == true;

        var rows = await moduleContext.ApiClient.GetRecipesAsync(
            moduleContext.ApiBaseUrl,
            search,
            category,
            includeInactive);

        var previouslySelected = (RecipesDataGrid.SelectedItem as RecipeResponse)?.Code ?? editingRecipeCode;

        // Le serveur trie deja par categorie puis par code : la grille ne reordonne
        // rien, elle affiche l'ordre que l'API a decide.
        suspendSelectionReload = true;

        try
        {
            RecipesDataGrid.ItemsSource = rows;
            RestoreRecipeSelection(previouslySelected);
        }
        finally
        {
            suspendSelectionReload = false;
        }

        RecipeCountTextBlock.Text = rows.Count switch
        {
            0 => "Aucune fiche pour ces critères.",
            1 => "1 fiche technique affichée.",
            _ => $"{rows.Count.ToString("N0", CultureInfo.CurrentCulture)} fiches techniques affichées."
        };

        UpdateRecipeActionState();

        await LoadSelectedRecipeCostAsync();
    }

    /// <summary>
    /// Retrouve la ligne selectionnee par sa cle stable (le code de la fiche) apres
    /// un rechargement ; si elle a disparu, le formulaire repart proprement en
    /// creation plutot que de rester sur une fiche qui n'existe plus.
    /// </summary>
    private void RestoreRecipeSelection(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            RecipesDataGrid.SelectedItem = null;
            ApplySelectedRecipeToForm();
            return;
        }

        var match = RecipesDataGrid.Items
            .OfType<RecipeResponse>()
            .FirstOrDefault(recipe => string.Equals(recipe.Code, code, StringComparison.OrdinalIgnoreCase));

        RecipesDataGrid.SelectedItem = match;

        if (match is not null)
        {
            RecipesDataGrid.ScrollIntoView(match);
        }

        ApplySelectedRecipeToForm();
    }

    private async Task LoadSelectedRecipeCostAsync()
    {
        var moduleContext = context!;

        if (RecipesDataGrid.SelectedItem is not RecipeResponse selected)
        {
            ClearRecipeCost();
            return;
        }

        // Le cout est vide avant d'etre recalcule : un echec de l'appel laisse un
        // encart manifestement vide plutot que les chiffres de la fiche precedente.
        ClearRecipeCost();

        var cost = await moduleContext.ApiClient.GetRecipeCostAsync(moduleContext.ApiBaseUrl, selected.Code);

        ApplyRecipeCost(cost);
    }

    private async Task LoadCheckpointsAsync()
    {
        var moduleContext = context!;

        var includeInactive = IncludeInactiveCheckpointsCheckBox.IsChecked == true;

        var rows = await moduleContext.ApiClient.GetTemperatureCheckpointsAsync(
            moduleContext.ApiBaseUrl,
            includeInactive);

        checkpoints = [.. rows];

        var previouslySelectedCheckpoint = (CheckpointsDataGrid.SelectedItem as TemperatureCheckpointResponse)?.Code;

        CheckpointsDataGrid.ItemsSource = rows;

        if (!string.IsNullOrWhiteSpace(previouslySelectedCheckpoint))
        {
            CheckpointsDataGrid.SelectedItem = rows
                .FirstOrDefault(checkpoint => string.Equals(
                    checkpoint.Code,
                    previouslySelectedCheckpoint,
                    StringComparison.OrdinalIgnoreCase));
        }

        CheckpointCountTextBlock.Text = rows.Count switch
        {
            0 => "Aucun point de contrôle déclaré.",
            1 => "1 point de contrôle.",
            _ => $"{rows.Count.ToString("N0", CultureInfo.CurrentCulture)} points de contrôle."
        };

        RefreshCheckpointOptions();
        UpdateCheckpointActionState();
        UpdateReadingVerdict();
    }

    /// <summary>
    /// Alimente les deux listes deroulantes de points de controle. La saisie ne
    /// propose que les points ACTIFS : le serveur refuse un releve sur un point
    /// desactive, l'ecran ne le propose donc pas. Le filtre de l'historique, lui,
    /// propose tous les points charges, car des releves passes leur restent
    /// rattaches.
    /// </summary>
    private void RefreshCheckpointOptions()
    {
        var previousEntryCode = (ReadingCheckpointComboBox.SelectedItem as KitchenCheckpointOption)?.Code;
        var previousFilterCode = (ReadingFilterCheckpointComboBox.SelectedItem as KitchenCheckpointOption)?.Code;

        var entryOptions = checkpoints
            .Where(checkpoint => checkpoint.IsActive)
            .Select(checkpoint => new KitchenCheckpointOption(
                checkpoint.Code,
                $"{checkpoint.Code} — {checkpoint.Label}",
                checkpoint.MinTemp,
                checkpoint.MaxTemp))
            .ToArray();

        var filterOptions = new List<KitchenCheckpointOption>
        {
            new(null, "Tous les points de contrôle", null, null)
        };

        filterOptions.AddRange(checkpoints.Select(checkpoint => new KitchenCheckpointOption(
            checkpoint.Code,
            $"{checkpoint.Code} — {checkpoint.Label}",
            checkpoint.MinTemp,
            checkpoint.MaxTemp)));

        ReadingCheckpointComboBox.ItemsSource = entryOptions;
        ReadingFilterCheckpointComboBox.ItemsSource = filterOptions;

        ReadingCheckpointComboBox.SelectedItem = entryOptions
            .FirstOrDefault(option => string.Equals(option.Code, previousEntryCode, StringComparison.OrdinalIgnoreCase));

        ReadingFilterCheckpointComboBox.SelectedItem = filterOptions
            .FirstOrDefault(option => string.Equals(option.Code, previousFilterCode, StringComparison.OrdinalIgnoreCase))
            ?? filterOptions[0];
    }

    private async Task LoadReadingsAsync()
    {
        var moduleContext = context!;

        var fromDate = ReadingFromDatePicker.SelectedDate;
        var toDate = ReadingToDatePicker.SelectedDate;

        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
        {
            // Sortie anticipee : la grille est vidée en meme temps que le compteur,
            // pour ne pas laisser les lignes d'une periode qui n'a jamais ete chargee
            // sous un en-tete decrivant la nouvelle.
            ReadingsDataGrid.ItemsSource = null;
            ReadingCountTextBlock.Text = "Aucun relevé chargé.";
            SetStatus("La date de début ne peut pas être postérieure à la date de fin.", isError: true);
            return;
        }

        var checkpointCode = (ReadingFilterCheckpointComboBox.SelectedItem as KitchenCheckpointOption)?.Code;
        var nonCompliantOnly = NonCompliantOnlyCheckBox.IsChecked == true;

        var rows = await moduleContext.ApiClient.GetTemperatureReadingsAsync(
            moduleContext.ApiBaseUrl,
            ToInstantStart(fromDate),
            ToInstantEnd(toDate),
            checkpointCode,
            nonCompliantOnly);

        ReadingsDataGrid.ItemsSource = rows;

        var nonCompliantCount = rows.Count(reading => !reading.IsCompliant);

        ReadingCountTextBlock.Text = rows.Count == 0
            ? "Aucun relevé sur cette période."
            : $"{rows.Count.ToString("N0", CultureInfo.CurrentCulture)} relevé(s) sur la période, dont {nonCompliantCount.ToString("N0", CultureInfo.CurrentCulture)} non conforme(s).";
    }

    // =============================== Fiches techniques ===============================

    private async void RefreshRecipesButton_Click(object sender, RoutedEventArgs e)
    {
        await ReloadRecipesWithStatusAsync();
    }

    private async void IncludeInactiveRecipesCheckBox_Click(object sender, RoutedEventArgs e)
    {
        await ReloadRecipesWithStatusAsync();
    }

    private async void RecipeCategoryFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suspendFilterReload)
        {
            return;
        }

        await ReloadRecipesWithStatusAsync();
    }

    private async void RecipeSearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await ReloadRecipesWithStatusAsync();
    }

    private async Task ReloadRecipesWithStatusAsync()
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await LoadRecipesAsync();
            SetStatus("Fiches techniques actualisées.");
        });
    }

    private async void RecipesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplySelectedRecipeToForm();
        UpdateRecipeActionState();

        // La restauration de selection et la remise a zero de la vue changent la
        // selection sans qu'aucun appel reseau ne doive partir : l'appelant enchaine
        // lui-meme sur le cout, dans le meme RunAsync.
        if (suspendSelectionReload || suspendFilterReload)
        {
            return;
        }

        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(LoadSelectedRecipeCostAsync);
    }

    /// <summary>
    /// Bascule creation / modification sur la selection de la grille, comme le
    /// formulaire du fichier clients : le titre le dit et le bouton est renomme.
    /// Aucun appel reseau : la fiche complete est deja dans la reponse de liste.
    /// </summary>
    private void ApplySelectedRecipeToForm()
    {
        if (RecipesDataGrid.SelectedItem is not RecipeResponse selected)
        {
            ResetRecipeForm();
            return;
        }

        editingRecipeCode = selected.Code;

        RecipeCodeTextBox.Text = selected.Code;
        RecipeCodeTextBox.IsEnabled = false;
        RecipeNameTextBox.Text = selected.Name;
        RecipeYieldPortionsTextBox.Text = selected.YieldPortions.ToString(CultureInfo.CurrentCulture);
        RecipeAllergensTextBox.Text = selected.Allergens ?? string.Empty;
        RecipeInstructionsTextBox.Text = selected.Instructions ?? string.Empty;

        RecipeCategoryComboBox.SelectedItem = RecipeCategoryComboBox.Items
            .OfType<KitchenCategoryOption>()
            .FirstOrDefault(option => option.Value == selected.Category);

        ingredientRows.Clear();

        foreach (var ingredient in selected.Ingredients.OrderBy(current => current.LineNumber))
        {
            ingredientRows.Add(new KitchenIngredientEditorRow
            {
                ItemCode = ingredient.ItemCode,
                QuantityText = ingredient.Quantity.ToString("0.###", CultureInfo.CurrentCulture),
                Notes = ingredient.Notes ?? string.Empty
            });
        }

        RenumberIngredients();

        RecipeFormTitleTextBlock.Text = $"Modifier la fiche {selected.Code}";
        RecipeFormModeTextBlock.Text = "Modification";
        SaveRecipeButton.Content = "Enregistrer la fiche";
    }

    private void ResetRecipeForm()
    {
        editingRecipeCode = null;

        RecipeCodeTextBox.Text = string.Empty;
        RecipeCodeTextBox.IsEnabled = true;
        RecipeNameTextBox.Text = string.Empty;
        RecipeYieldPortionsTextBox.Text = "1";
        RecipeAllergensTextBox.Text = string.Empty;
        RecipeInstructionsTextBox.Text = string.Empty;

        if (RecipeCategoryComboBox.Items.Count > 0)
        {
            RecipeCategoryComboBox.SelectedIndex = 0;
        }

        ingredientRows.Clear();

        RecipeFormTitleTextBlock.Text = "Nouvelle fiche technique";
        RecipeFormModeTextBlock.Text = "Nouvelle fiche";
        SaveRecipeButton.Content = "Créer la fiche";
    }

    private void ResetRecipeFormButton_Click(object sender, RoutedEventArgs e)
    {
        RecipesDataGrid.SelectedItem = null;
        ResetRecipeForm();
        ClearRecipeCost();
        UpdateRecipeActionState();
        SetStatus("Formulaire vidé : la saisie repart sur une nouvelle fiche.");
    }

    private void AddIngredientButton_Click(object sender, RoutedEventArgs e)
    {
        ingredientRows.Add(new KitchenIngredientEditorRow());
        RenumberIngredients();

        IngredientsDataGrid.ScrollIntoView(ingredientRows[^1]);
    }

    private void RemoveIngredientButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: KitchenIngredientEditorRow row })
        {
            ingredientRows.Remove(row);
            RenumberIngredients();
        }
    }

    private void RenumberIngredients()
    {
        var lineNumber = 1;

        foreach (var row in ingredientRows)
        {
            row.LineNumber = lineNumber++;
        }
    }

    private void RecipeYieldPortionsTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Le nombre de portions divise le cout matiere : l'ecran ne recalcule rien
        // localement, il se contente de rappeler que le cout affiche porte sur la
        // valeur ENREGISTREE, pas sur celle en cours de frappe.
        UpdateRecipeActionState();
    }

    private async void SaveRecipeButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        if (!TryBuildRecipeIngredients(out var ingredients))
        {
            return;
        }

        var name = RecipeNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Le nom de la fiche est obligatoire.", isError: true);
            RecipeNameTextBox.Focus();
            return;
        }

        if (RecipeCategoryComboBox.SelectedItem is not KitchenCategoryOption { Value: RecipeCategory category })
        {
            SetStatus("La catégorie de la fiche est obligatoire.", isError: true);
            RecipeCategoryComboBox.Focus();
            return;
        }

        // Borne du domaine (RecipeSheet : au moins une portion) verifiee avant
        // l'envoi, avec un message explicite plutot qu'une erreur serveur apres
        // l'aller-retour.
        if (!int.TryParse(
                RecipeYieldPortionsTextBox.Text.Trim(),
                NumberStyles.Integer,
                CultureInfo.CurrentCulture,
                out var yieldPortions)
            || yieldPortions < 1)
        {
            SetStatus("Le nombre de portions doit être un entier d'au moins 1.", isError: true);
            RecipeYieldPortionsTextBox.Focus();
            return;
        }

        var allergens = NullIfBlank(RecipeAllergensTextBox.Text);
        var instructions = NullIfBlank(RecipeInstructionsTextBox.Text);

        if (editingRecipeCode is string existingCode)
        {
            await moduleContext.RunAsync(async () =>
            {
                await moduleContext.ApiClient.UpdateRecipeAsync(
                    moduleContext.ApiBaseUrl,
                    existingCode,
                    new UpdateRecipeRequest(name, category, yieldPortions, allergens, instructions, ingredients));

                await LoadRecipesAsync();
                SetStatus($"Fiche technique {existingCode} enregistrée.");
            });

            return;
        }

        var code = RecipeCodeTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            SetStatus("Le code de la fiche est obligatoire.", isError: true);
            RecipeCodeTextBox.Focus();
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var created = await moduleContext.ApiClient.CreateRecipeAsync(
                moduleContext.ApiBaseUrl,
                new CreateRecipeRequest(code, name, category, yieldPortions, allergens, instructions, ingredients));

            editingRecipeCode = created.Code;

            await LoadRecipesAsync();
            SetStatus($"Fiche technique {created.Code} créée.");
        });
    }

    /// <summary>
    /// Controle des lignes d'ingredients avant l'envoi. Les regles verifiees ici
    /// sont le MIROIR de celles du domaine (RecipeSheet.ReplaceIngredients et
    /// RecipeIngredient) : au moins une ligne, un code d'article par ligne, une
    /// quantite strictement positive a 3 decimales au maximum, et pas deux fois le
    /// meme article. Le serveur reste l'autorite ; ceci evite un aller-retour.
    /// </summary>
    private bool TryBuildRecipeIngredients(out RecipeIngredientRequest[] ingredients)
    {
        ingredients = [];

        if (ingredientRows.Count == 0)
        {
            SetStatus("Une fiche technique doit comporter au moins un ingrédient.", isError: true);
            return false;
        }

        var built = new List<RecipeIngredientRequest>(ingredientRows.Count);
        var seenItemCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in ingredientRows)
        {
            var itemCode = row.ItemCode.Trim();

            if (string.IsNullOrWhiteSpace(itemCode))
            {
                SetStatus($"Ligne {row.LineNumber} : le code de l'article est obligatoire.", isError: true);
                return false;
            }

            if (!seenItemCodes.Add(itemCode))
            {
                SetStatus($"L'article {itemCode.ToUpperInvariant()} apparaît plusieurs fois dans la liste des ingrédients.", isError: true);
                return false;
            }

            if (!TryReadQuantity(row.QuantityText, out var quantity))
            {
                SetStatus($"Ligne {row.LineNumber} : la quantité doit être strictement positive, avec 3 décimales au maximum.", isError: true);
                return false;
            }

            built.Add(new RecipeIngredientRequest(itemCode, quantity, NullIfBlank(row.Notes)));
        }

        ingredients = [.. built];
        return true;
    }

    private async void ActivateRecipeButton_Click(object sender, RoutedEventArgs e)
    {
        await SetRecipeActiveAsync(isActive: true);
    }

    private async void DeactivateRecipeButton_Click(object sender, RoutedEventArgs e)
    {
        await SetRecipeActiveAsync(isActive: false);
    }

    private async Task SetRecipeActiveAsync(bool isActive)
    {
        var moduleContext = context;

        if (moduleContext is null || RecipesDataGrid.SelectedItem is not RecipeResponse selected)
        {
            return;
        }

        if (!isActive)
        {
            var question = string.Format(
                CultureInfo.CurrentCulture,
                "Désactiver la fiche technique {0} ({1}) ?{2}{2}Elle ne sera plus proposée dans la liste courante et son coût matière ne sera plus suivi.",
                selected.Code,
                selected.Name,
                Environment.NewLine);

            if (!Confirm(question, "Désactivation d'une fiche technique"))
            {
                return;
            }
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.SetRecipeActiveAsync(moduleContext.ApiBaseUrl, selected.Code, isActive);
            await LoadRecipesAsync();
            SetStatus(isActive ? "Fiche technique activée." : "Fiche technique désactivée.");
        });
    }

    private async void RefreshRecipeCostButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        if (RecipesDataGrid.SelectedItem is not RecipeResponse)
        {
            SetStatus("Sélectionnez une fiche enregistrée pour calculer son coût matière.", isError: true);
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await LoadSelectedRecipeCostAsync();
            SetStatus("Coût matière recalculé sur les PMP courants du stock.");
        });
    }

    /// <summary>
    /// Affiche les chiffres RENVOYES PAR LE SERVEUR : ni le total, ni le cout par
    /// portion ne sont recalcules ici. Quand des ingredients n'ont pas de cout
    /// connu, le bandeau d'avertissement du serveur est repris tel quel - le total
    /// affiche est alors un minorant, et l'ecran le dit.
    /// </summary>
    private void ApplyRecipeCost(RecipeCostResponse cost)
    {
        CostPerPortionTextBlock.Text = FormatAmount(cost.CostPerPortion);
        CostTotalTextBlock.Text = FormatAmount(cost.TotalCost);
        CostYieldPortionsTextBlock.Text = cost.YieldPortions.ToString("N0", CultureInfo.CurrentCulture);
        CostComputedAtTextBlock.Text = FormatMoment(cost.ComputedAt);
        CostBasisTextBlock.Text = cost.CostBasis;

        IngredientCostsDataGrid.ItemsSource = cost.Ingredients;

        if (cost.HasMissingCosts)
        {
            CostWarningTextBlock.Text = cost.Warning
                ?? "Coût partiel : au moins un ingrédient n'a pas de coût moyen connu et reste exclu du total.";
            CostWarningBorder.Visibility = Visibility.Visible;
        }
        else
        {
            CostWarningTextBlock.Text = string.Empty;
            CostWarningBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void ClearRecipeCost()
    {
        CostPerPortionTextBlock.Text = NoValuePlaceholder;
        CostTotalTextBlock.Text = NoValuePlaceholder;
        CostYieldPortionsTextBlock.Text = NoValuePlaceholder;
        CostComputedAtTextBlock.Text = NoValuePlaceholder;
        CostWarningTextBlock.Text = string.Empty;
        CostWarningBorder.Visibility = Visibility.Collapsed;
        IngredientCostsDataGrid.ItemsSource = null;
    }

    // =============================== Releves HACCP ===============================

    private void ReadingCheckpointComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateReadingVerdict();
    }

    private void ReadingValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateReadingVerdict();
    }

    private void ReadingCorrectiveActionTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateReadingActionState();
    }

    private void BackdateReadingCheckBox_Click(object sender, RoutedEventArgs e)
    {
        var backdated = BackdateReadingCheckBox.IsChecked == true;
        var visibility = backdated ? Visibility.Visible : Visibility.Collapsed;

        BackdateDatePanel.Visibility = visibility;
        BackdateTimePanel.Visibility = visibility;

        if (backdated && string.IsNullOrWhiteSpace(ReadingMeasuredAtTimeTextBox.Text))
        {
            ReadingMeasuredAtTimeTextBox.Text = DateTime.Now.ToString("HH:mm", CultureInfo.CurrentCulture);
        }
    }

    /// <summary>
    /// Verdict de conformite affiche IMMEDIATEMENT pendant la saisie, en appliquant
    /// la regle du domaine (TemperatureCheckpoint.IsWithinRange, bornes incluses)
    /// aux seuils du point de controle selectionne. C'est un miroir de la regle du
    /// serveur, jamais une seconde definition : le verdict qui fera foi est celui
    /// que le serveur fige sur le releve.
    /// </summary>
    private void UpdateReadingVerdict()
    {
        var checkpoint = ReadingCheckpointComboBox.SelectedItem as KitchenCheckpointOption;
        var minTemp = checkpoint?.MinTemp;
        var maxTemp = checkpoint?.MaxTemp;

        ReadingRangeTextBlock.Text = minTemp is not null && maxTemp is not null
            ? $"Plage de conformité : de {FormatTemperature(minTemp.Value)} °C à {FormatTemperature(maxTemp.Value)} °C, bornes incluses."
            : "Sélectionnez un point de contrôle pour connaître sa plage de conformité.";

        var hasValue = TryReadTemperature(ReadingValueTextBox.Text, out var value);

        if (minTemp is null || maxTemp is null || !hasValue)
        {
            ApplyReadingVerdictBadge(
                "En attente d'une valeur",
                "StatusDraftBackgroundBrush",
                "StatusDraftForegroundBrush");

            ReadingCorrectiveActionLabel.Text = "Action corrective";
            UpdateReadingActionState();
            return;
        }

        var compliant = TemperatureCheckpoint.IsWithinRange(value, minTemp.Value, maxTemp.Value);

        if (compliant)
        {
            ApplyReadingVerdictBadge(
                "Conforme",
                "StatusValidatedBackgroundBrush",
                "StatusValidatedForegroundBrush");

            ReadingCorrectiveActionLabel.Text = "Action corrective";
        }
        else
        {
            ApplyReadingVerdictBadge(
                "Non conforme — action corrective requise",
                "StatusRejectedBackgroundBrush",
                "StatusRejectedForegroundBrush");

            ReadingCorrectiveActionLabel.Text = "Action corrective *";
        }

        UpdateReadingActionState();
    }

    private void ApplyReadingVerdictBadge(string label, string backgroundKey, string foregroundKey)
    {
        ReadingVerdictTextBlock.Text = label;

        if (ThemeBrush(backgroundKey) is Brush background)
        {
            ReadingVerdictBorder.Background = background;
        }

        if (ThemeBrush(foregroundKey) is Brush foreground)
        {
            ReadingVerdictTextBlock.Foreground = foreground;
        }
    }

    /// <summary>
    /// Vrai quand la saisie courante est complete et acceptable : un point de
    /// controle, une valeur lisible, et - si la valeur sort de la plage - une action
    /// corrective. C'est la condition qui GRISE le bouton d'enregistrement tant
    /// qu'une non-conformite n'est pas motivee.
    /// </summary>
    private bool IsReadingReadyToSend()
    {
        var checkpoint = ReadingCheckpointComboBox.SelectedItem as KitchenCheckpointOption;
        var minTemp = checkpoint?.MinTemp;
        var maxTemp = checkpoint?.MaxTemp;

        if (minTemp is null || maxTemp is null)
        {
            return false;
        }

        if (!TryReadTemperature(ReadingValueTextBox.Text, out var value))
        {
            return false;
        }

        return TemperatureCheckpoint.IsWithinRange(value, minTemp.Value, maxTemp.Value)
            || !string.IsNullOrWhiteSpace(ReadingCorrectiveActionTextBox.Text);
    }

    private async void SaveReadingButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        var checkpoint = ReadingCheckpointComboBox.SelectedItem as KitchenCheckpointOption;
        var checkpointCode = checkpoint?.Code;
        var minTemp = checkpoint?.MinTemp;
        var maxTemp = checkpoint?.MaxTemp;

        if (checkpointCode is null || minTemp is null || maxTemp is null)
        {
            SetStatus("Sélectionnez le point de contrôle relevé.", isError: true);
            ReadingCheckpointComboBox.Focus();
            return;
        }

        if (!TryReadTemperature(ReadingValueTextBox.Text, out var value))
        {
            SetStatus("La température doit être une valeur en degrés Celsius, avec 1 décimale au maximum.", isError: true);
            ReadingValueTextBox.Focus();
            return;
        }

        // Bornes de bon sens du domaine (TemperatureCheckpoint.MinSupportedCelsius /
        // MaxSupportedCelsius), referencees et non recopiees.
        if (value < TemperatureCheckpoint.MinSupportedCelsius || value > TemperatureCheckpoint.MaxSupportedCelsius)
        {
            SetStatus(
                string.Format(
                    CultureInfo.CurrentCulture,
                    "La température doit être comprise entre {0} et {1} degrés Celsius.",
                    FormatTemperature(TemperatureCheckpoint.MinSupportedCelsius),
                    FormatTemperature(TemperatureCheckpoint.MaxSupportedCelsius)),
                isError: true);
            ReadingValueTextBox.Focus();
            return;
        }

        var compliant = TemperatureCheckpoint.IsWithinRange(value, minTemp.Value, maxTemp.Value);
        var correctiveAction = NullIfBlank(ReadingCorrectiveActionTextBox.Text);

        if (!compliant && correctiveAction is null)
        {
            SetStatus("Relevé non conforme : l'action corrective est obligatoire avant l'enregistrement.", isError: true);
            ReadingCorrectiveActionTextBox.Focus();
            return;
        }

        if (!TryBuildMeasuredAt(out var measuredAt))
        {
            return;
        }

        // Acte engageant : un releve est ajoute une fois pour toutes, il n'est
        // ensuite ni modifiable ni supprimable (une correction est un nouveau
        // releve). La confirmation nomme le point, la valeur et le verdict.
        var question = string.Format(
            CultureInfo.CurrentCulture,
            "Enregistrer un relevé {0} de {1} °C sur le point {2} ?{3}{3}Un relevé ne peut plus être modifié ni supprimé : une correction se fait par un nouveau relevé.",
            compliant ? "conforme" : "NON CONFORME",
            FormatTemperature(value),
            checkpointCode,
            Environment.NewLine);

        if (!Confirm(question, "Enregistrement d'un relevé HACCP"))
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.CreateTemperatureReadingAsync(
                moduleContext.ApiBaseUrl,
                new CreateTemperatureReadingRequest(checkpointCode, value, measuredAt, correctiveAction));

            ResetReadingForm();
            await LoadReadingsAsync();

            SetStatus(compliant
                ? "Relevé enregistré : conforme aux seuils du moment."
                : "Relevé enregistré : non conforme, action corrective tracée.");
        });
    }

    /// <summary>
    /// Instant de la mesure. Sans transcription, null est envoye et le serveur date
    /// le releve du moment de l'enregistrement. Avec transcription, la date et
    /// l'heure saisies sont lues en heure LOCALE du poste puis envoyees avec leur
    /// decalage : le serveur compare des instants.
    /// </summary>
    private bool TryBuildMeasuredAt(out DateTimeOffset? measuredAt)
    {
        measuredAt = null;

        if (BackdateReadingCheckBox.IsChecked != true)
        {
            return true;
        }

        if (ReadingMeasuredOnDatePicker.SelectedDate is not DateTime day)
        {
            SetStatus("Indiquez la date de la mesure transcrite.", isError: true);
            ReadingMeasuredOnDatePicker.Focus();
            return false;
        }

        var timeText = ReadingMeasuredAtTimeTextBox.Text.Trim();

        if (!TimeSpan.TryParseExact(timeText, @"hh\:mm", CultureInfo.InvariantCulture, out var time))
        {
            SetStatus("L'heure de la mesure doit être écrite au format hh:mm.", isError: true);
            ReadingMeasuredAtTimeTextBox.Focus();
            return false;
        }

        var local = day.Date.Add(time);
        var instant = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));

        // Miroir de la regle du serveur : une mesure ne peut pas etre datee dans le
        // futur (le serveur applique la meme borne, avec une tolerance d'horloge).
        if (instant > DateTimeOffset.Now)
        {
            SetStatus("Une mesure ne peut pas être datée dans le futur.", isError: true);
            ReadingMeasuredAtTimeTextBox.Focus();
            return false;
        }

        measuredAt = instant;
        return true;
    }

    private void ResetReadingFormButton_Click(object sender, RoutedEventArgs e)
    {
        ResetReadingForm();
        SetStatus("Formulaire de relevé vidé.");
    }

    private void ResetReadingForm()
    {
        ReadingCheckpointComboBox.SelectedItem = null;
        ReadingValueTextBox.Text = string.Empty;
        ReadingCorrectiveActionTextBox.Text = string.Empty;

        BackdateReadingCheckBox.IsChecked = false;
        BackdateDatePanel.Visibility = Visibility.Collapsed;
        BackdateTimePanel.Visibility = Visibility.Collapsed;
        ReadingMeasuredOnDatePicker.SelectedDate = DateTime.Today;
        ReadingMeasuredAtTimeTextBox.Text = string.Empty;

        UpdateReadingVerdict();
    }

    private async void RefreshReadingsButton_Click(object sender, RoutedEventArgs e)
    {
        await ReloadReadingsWithStatusAsync();
    }

    private async void NonCompliantOnlyCheckBox_Click(object sender, RoutedEventArgs e)
    {
        await ReloadReadingsWithStatusAsync();
    }

    private async Task ReloadReadingsWithStatusAsync()
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await LoadReadingsAsync();
            SetStatus("Relevés actualisés.");
        });
    }

    // =============================== Points de controle ===============================

    private void CheckpointsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateCheckpointActionState();
    }

    private void EditCheckpointButton_Click(object sender, RoutedEventArgs e)
    {
        if (CheckpointsDataGrid.SelectedItem is not TemperatureCheckpointResponse selected)
        {
            return;
        }

        editingCheckpointCode = selected.Code;

        CheckpointCodeTextBox.Text = selected.Code;
        CheckpointCodeTextBox.IsEnabled = false;
        CheckpointLabelTextBox.Text = selected.Label;
        CheckpointMinTempTextBox.Text = FormatTemperature(selected.MinTemp);
        CheckpointMaxTempTextBox.Text = FormatTemperature(selected.MaxTemp);

        CheckpointFormModeTextBlock.Text = $"Modification de {selected.Code}";
        SaveCheckpointButton.Content = "Enregistrer le point";

        UpdateCheckpointActionState();
    }

    private void ResetCheckpointFormButton_Click(object sender, RoutedEventArgs e)
    {
        ResetCheckpointForm();
        UpdateCheckpointActionState();
        SetStatus("Formulaire de point de contrôle vidé.");
    }

    private void ResetCheckpointForm()
    {
        editingCheckpointCode = null;

        CheckpointCodeTextBox.Text = string.Empty;
        CheckpointCodeTextBox.IsEnabled = true;
        CheckpointLabelTextBox.Text = string.Empty;
        CheckpointMinTempTextBox.Text = string.Empty;
        CheckpointMaxTempTextBox.Text = string.Empty;

        CheckpointFormModeTextBlock.Text = "Nouveau point de contrôle";
        SaveCheckpointButton.Content = "Créer le point";
    }

    private async void SaveCheckpointButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        var label = CheckpointLabelTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(label))
        {
            SetStatus("Le libellé du point de contrôle est obligatoire.", isError: true);
            CheckpointLabelTextBox.Focus();
            return;
        }

        if (!TryReadTemperature(CheckpointMinTempTextBox.Text, out var minTemp))
        {
            SetStatus("Le seuil minimum doit être une température valide, avec 1 décimale au maximum.", isError: true);
            CheckpointMinTempTextBox.Focus();
            return;
        }

        if (!TryReadTemperature(CheckpointMaxTempTextBox.Text, out var maxTemp))
        {
            SetStatus("Le seuil maximum doit être une température valide, avec 1 décimale au maximum.", isError: true);
            CheckpointMaxTempTextBox.Focus();
            return;
        }

        // Miroir de la regle du domaine (TemperatureCheckpoint : min strictement
        // inferieur a max), verifiee avant l'aller-retour.
        if (minTemp >= maxTemp)
        {
            SetStatus("Le seuil minimum doit être strictement inférieur au seuil maximum.", isError: true);
            CheckpointMinTempTextBox.Focus();
            return;
        }

        if (editingCheckpointCode is string existingCode)
        {
            // Acte engageant : la nouvelle plage s'appliquera a tous les relevés a
            // venir. La confirmation dit ce qui change et ce qui ne change pas.
            var question = string.Format(
                CultureInfo.CurrentCulture,
                "Modifier la plage du point {0} en {1} °C à {2} °C ?{3}{3}Les relevés déjà enregistrés conservent les seuils figés au moment de leur mesure : leur conformité n'est pas réécrite.",
                existingCode,
                FormatTemperature(minTemp),
                FormatTemperature(maxTemp),
                Environment.NewLine);

            if (!Confirm(question, "Modification d'un point de contrôle"))
            {
                return;
            }

            await moduleContext.RunAsync(async () =>
            {
                await moduleContext.ApiClient.UpdateTemperatureCheckpointAsync(
                    moduleContext.ApiBaseUrl,
                    existingCode,
                    new UpdateTemperatureCheckpointRequest(label, minTemp, maxTemp));

                ResetCheckpointForm();
                await LoadCheckpointsAsync();
                SetStatus($"Point de contrôle {existingCode} enregistré.");
            });

            return;
        }

        var code = CheckpointCodeTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            SetStatus("Le code du point de contrôle est obligatoire.", isError: true);
            CheckpointCodeTextBox.Focus();
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            var created = await moduleContext.ApiClient.CreateTemperatureCheckpointAsync(
                moduleContext.ApiBaseUrl,
                new CreateTemperatureCheckpointRequest(code, label, minTemp, maxTemp));

            ResetCheckpointForm();
            await LoadCheckpointsAsync();
            SetStatus($"Point de contrôle {created.Code} créé.");
        });
    }

    private async void ActivateCheckpointButton_Click(object sender, RoutedEventArgs e)
    {
        await SetCheckpointActiveAsync(isActive: true);
    }

    private async void DeactivateCheckpointButton_Click(object sender, RoutedEventArgs e)
    {
        await SetCheckpointActiveAsync(isActive: false);
    }

    private async Task SetCheckpointActiveAsync(bool isActive)
    {
        var moduleContext = context;

        if (moduleContext is null || CheckpointsDataGrid.SelectedItem is not TemperatureCheckpointResponse selected)
        {
            return;
        }

        if (!isActive)
        {
            var question = string.Format(
                CultureInfo.CurrentCulture,
                "Désactiver le point de contrôle {0} ({1}) ?{2}{2}Il ne sera plus proposé à la saisie des relevés. Les relevés déjà enregistrés restent intacts dans l'historique.",
                selected.Code,
                selected.Label,
                Environment.NewLine);

            if (!Confirm(question, "Désactivation d'un point de contrôle"))
            {
                return;
            }
        }

        await moduleContext.RunAsync(async () =>
        {
            await moduleContext.ApiClient.SetTemperatureCheckpointActiveAsync(
                moduleContext.ApiBaseUrl,
                selected.Code,
                isActive);

            await LoadCheckpointsAsync();
            SetStatus(isActive ? "Point de contrôle activé." : "Point de contrôle désactivé.");
        });
    }

    private async void IncludeInactiveCheckpointsCheckBox_Click(object sender, RoutedEventArgs e)
    {
        await ReloadCheckpointsWithStatusAsync();
    }

    private async void RefreshCheckpointsButton_Click(object sender, RoutedEventArgs e)
    {
        await ReloadCheckpointsWithStatusAsync();
    }

    private async Task ReloadCheckpointsWithStatusAsync()
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await LoadCheckpointsAsync();
            SetStatus("Points de contrôle actualisés.");
        });
    }

    // =============================== Actions transverses ===============================

    private async void RefreshAllButton_Click(object sender, RoutedEventArgs e)
    {
        var moduleContext = context;

        if (moduleContext is null || !moduleContext.ApiClient.IsAuthenticated)
        {
            return;
        }

        await moduleContext.RunAsync(async () =>
        {
            await LoadEverythingAsync();
            SetStatus("Module cuisine actualisé.");
        });
    }

    private void UpdateAllActionStates()
    {
        UpdateRecipeActionState();
        UpdateReadingActionState();
        UpdateCheckpointActionState();
    }

    // Etat metier (selection, statut de la ligne) croise avec le droit
    // kitchen.write, qui commande toutes les ecritures du module.
    private void UpdateRecipeActionState()
    {
        var selected = RecipesDataGrid.SelectedItem as RecipeResponse;

        SaveRecipeButton.IsEnabled = canWrite;
        AddIngredientButton.IsEnabled = canWrite;
        ActivateRecipeButton.IsEnabled = canWrite && selected is { IsActive: false };
        DeactivateRecipeButton.IsEnabled = canWrite && selected is { IsActive: true };

        ApplyPermissionHint(SaveRecipeButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(AddIngredientButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(ActivateRecipeButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(DeactivateRecipeButton, canWrite, WritePermissionHint);
    }

    private void UpdateReadingActionState()
    {
        var ready = IsReadingReadyToSend();

        SaveReadingButton.IsEnabled = canWrite && ready;

        // ApplyPermissionHint restaure d'abord l'info-bulle d'origine quand le droit est la
        // (la symetrie reste entiere) ; la raison metier ne la remplace que tant que le
        // formulaire n'est pas envoyable, et disparait des qu'il l'est.
        ApplyPermissionHint(SaveReadingButton, canWrite, WritePermissionHint);

        if (canWrite && !ready)
        {
            SaveReadingButton.ToolTip = ReadingIncompleteHint;
        }
    }

    private void UpdateCheckpointActionState()
    {
        var selected = CheckpointsDataGrid.SelectedItem as TemperatureCheckpointResponse;

        SaveCheckpointButton.IsEnabled = canWrite;
        EditCheckpointButton.IsEnabled = canWrite && selected is not null;
        ActivateCheckpointButton.IsEnabled = canWrite && selected is { IsActive: false };
        DeactivateCheckpointButton.IsEnabled = canWrite && selected is { IsActive: true };

        ApplyPermissionHint(SaveCheckpointButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(EditCheckpointButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(ActivateCheckpointButton, canWrite, WritePermissionHint);
        ApplyPermissionHint(DeactivateCheckpointButton, canWrite, WritePermissionHint);
    }

    /// <summary>
    /// Pose le message d'explication quand le droit manque, et RESTAURE l'info-bulle
    /// d'origine du bouton quand il est present : l'affectation doit etre symetrique,
    /// sinon un message pose pour un profil restreint survit a la reconnexion d'un
    /// profil qui, lui, a le droit (les vues survivent a la deconnexion).
    /// </summary>
    private void ApplyPermissionHint(Button button, bool allowed, string hint)
    {
        if (!originalToolTips.ContainsKey(button))
        {
            originalToolTips[button] = button.ToolTip;
        }

        button.ToolTip = allowed ? originalToolTips[button] : hint;
    }

    // =============================== Outils ===============================

    private void SetStatus(string message, bool isError = false)
    {
        context?.SetStatus(message, isError);
    }

    /// <summary>
    /// Gabarit de confirmation du depot : fenetre proprietaire, icone
    /// d'avertissement, et defaut sur Non.
    /// </summary>
    private bool Confirm(string question, string caption)
    {
        var owner = Window.GetWindow(this);

        var result = owner is null
            ? MessageBox.Show(question, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(owner, question, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    /// <summary>
    /// Aucune couleur en dur : la pastille de conformite reprend les brushes de
    /// statut du theme, comme les badges poses en XAML dans les grilles.
    /// TryFindResource (et non FindResource) pour que le concepteur XAML, qui rend
    /// la vue hors de l'application, ne leve pas sur une cle absente.
    /// </summary>
    private Brush? ThemeBrush(string resourceKey)
    {
        return TryFindResource(resourceKey) as Brush;
    }

    /// <summary>
    /// Lecture d'une quantite d'ingredient : la virgule (culture courante) comme le
    /// point (culture invariante) sont acceptes, et la capacite de la colonne
    /// numeric(18,3) est verifiee avant l'envoi.
    /// </summary>
    private static bool TryReadQuantity(string text, out decimal value)
    {
        value = 0m;

        var trimmed = text.Trim();

        if (!decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out value) &&
            !decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        return value > 0m && decimal.Round(value, 3) == value;
    }

    /// <summary>
    /// Lecture d'une temperature : meme tolerance virgule / point, et la precision
    /// de la colonne numeric(6,1) est verifiee avant l'envoi (le domaine refuse une
    /// valeur plus fine, qui serait silencieusement tronquee).
    /// </summary>
    private static bool TryReadTemperature(string text, out decimal value)
    {
        value = 0m;

        var trimmed = text.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        if (!decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out value) &&
            !decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        return decimal.Round(value, 1) == value;
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string FormatAmount(decimal value)
    {
        return value.ToString("N2", CultureInfo.CurrentCulture);
    }

    private static string FormatTemperature(decimal value)
    {
        return value.ToString("N1", CultureInfo.CurrentCulture);
    }

    private static string FormatMoment(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
    }

    /// <summary>Debut du jour local choisi, en instant (l'API compare des instants).</summary>
    private static DateTimeOffset? ToInstantStart(DateTime? day)
    {
        if (day is not DateTime value)
        {
            return null;
        }

        var local = value.Date;
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }

    /// <summary>Fin du jour local choisi, bornes incluses.</summary>
    private static DateTimeOffset? ToInstantEnd(DateTime? day)
    {
        if (day is not DateTime value)
        {
            return null;
        }

        var local = value.Date.AddDays(1).AddTicks(-1);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }
}

/// <summary>Entree de liste deroulante pour une categorie de fiche (valeur nulle = toutes).</summary>
public sealed class KitchenCategoryOption(RecipeCategory? value, string label)
{
    public RecipeCategory? Value { get; } = value;

    public string Label { get; } = label;
}

/// <summary>
/// Entree de liste deroulante pour un point de controle. Les seuils sont portes par
/// l'option pour que le verdict de conformite s'affiche des la frappe, sans nouvel
/// appel reseau. Code nul : entree "tous les points de contrôle" des filtres.
/// </summary>
public sealed class KitchenCheckpointOption(string? code, string label, decimal? minTemp, decimal? maxTemp)
{
    public string? Code { get; } = code;

    public string Label { get; } = label;

    public decimal? MinTemp { get; } = minTemp;

    public decimal? MaxTemp { get; } = maxTemp;
}

/// <summary>
/// Ligne d'ingredient en cours de saisie dans la grille editable. La quantite est
/// conservee sous forme de texte pour accepter la virgule comme le point pendant la
/// frappe ; la conversion et les controles de format ont lieu a l'enregistrement.
/// </summary>
public sealed class KitchenIngredientEditorRow : INotifyPropertyChanged
{
    private int lineNumber;
    private string itemCode = string.Empty;
    private string quantityText = "1";
    private string notes = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int LineNumber
    {
        get => lineNumber;
        set => SetField(ref lineNumber, value);
    }

    public string ItemCode
    {
        get => itemCode;
        set => SetField(ref itemCode, value ?? string.Empty);
    }

    public string QuantityText
    {
        get => quantityText;
        set => SetField(ref quantityText, value ?? string.Empty);
    }

    public string Notes
    {
        get => notes;
        set => SetField(ref notes, value ?? string.Empty);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>Libelles francais du module, source unique pour les listes et les grilles.</summary>
public static class KitchenLabels
{
    public static string Category(RecipeCategory category)
    {
        return category switch
        {
            RecipeCategory.Entree => "Entrée",
            RecipeCategory.Plat => "Plat",
            RecipeCategory.Dessert => "Dessert",
            RecipeCategory.Boisson => "Boisson",
            RecipeCategory.SousPreparation => "Sous-préparation",
            _ => category.ToString()
        };
    }
}

/// <summary>Affiche la categorie d'une fiche en francais dans les grilles.</summary>
public sealed class RecipeCategoryLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is RecipeCategory category ? KitchenLabels.Category(category) : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Convertit un horodatage UTC renvoye par l'API en heure du poste : aucun instant
/// brut ne doit apparaitre a l'ecran.
/// </summary>
public sealed class KitchenMomentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            DateTimeOffset moment => moment.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture),
            _ => null
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
