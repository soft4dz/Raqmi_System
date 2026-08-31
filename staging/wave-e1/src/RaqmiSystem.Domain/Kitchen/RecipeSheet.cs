using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Kitchen;

/// <summary>
/// Recipe sheet ("fiche technique"): the standardized description of a dish - its ingredient
/// list against the stock items, the number of portions the full recipe yields, allergens and
/// preparation instructions. The material cost of the sheet is NOT stored here: it is computed
/// on demand from the CURRENT weighted average costs of the stock module (see
/// IKitchenService.GetRecipeCostAsync), because a stored cost would silently go stale with
/// every stock receipt.
///
/// <c>Allergens</c> is a free-text field (max 300 characters), deliberately without any
/// regulatory nomenclature: this repository does not invent an official allergen list, it
/// lets the kitchen write the mentions it is responsible for.
/// </summary>
public sealed class RecipeSheet : AuditableEntity
{
    private readonly List<RecipeIngredient> _ingredients = new();

    private RecipeSheet()
    {
    }

    public RecipeSheet(
        string code,
        string name,
        RecipeCategory category,
        int yieldPortions,
        string? allergens = null,
        string? instructions = null)
    {
        Code = NormalizeCode(code);
        Name = RequireValue(name, nameof(name), 200);
        Category = RequireDefinedCategory(category);
        YieldPortions = RequireStrictlyPositivePortions(yieldPortions);
        Allergens = NormalizeOptional(allergens, nameof(allergens), 300);
        Instructions = NormalizeOptional(instructions, nameof(instructions), 4000);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public RecipeCategory Category { get; private set; } = RecipeCategory.Plat;

    /// <summary>Number of portions the full recipe yields. Strictly positive: the cost per portion divides by it.</summary>
    public int YieldPortions { get; private set; } = 1;

    /// <summary>Free-text allergen mentions (max 300). No regulatory nomenclature is enforced.</summary>
    public string? Allergens { get; private set; }

    public string? Instructions { get; private set; }

    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<RecipeIngredient> Ingredients => _ingredients.AsReadOnly();

    public void UpdateDetails(
        string name,
        RecipeCategory category,
        int yieldPortions,
        string? allergens,
        string? instructions)
    {
        Name = RequireValue(name, nameof(name), 200);
        Category = RequireDefinedCategory(category);
        YieldPortions = RequireStrictlyPositivePortions(yieldPortions);
        Allergens = NormalizeOptional(allergens, nameof(allergens), 300);
        Instructions = NormalizeOptional(instructions, nameof(instructions), 4000);
    }

    /// <summary>
    /// Replaces the whole ingredient list, renumbering the lines - the same replace-all
    /// semantics as Invoice.ReplaceLines. A recipe without any ingredient is meaningless
    /// (there would be nothing to cost), so at least one line is required.
    /// </summary>
    public void ReplaceIngredients(IReadOnlyCollection<RecipeIngredient> ingredients)
    {
        ArgumentNullException.ThrowIfNull(ingredients);

        if (ingredients.Count == 0)
        {
            throw new ArgumentException("A recipe requires at least one ingredient.", nameof(ingredients));
        }

        var duplicated = ingredients
            .GroupBy(ingredient => ingredient.ItemCode, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicated is not null)
        {
            throw new ArgumentException(
                $"Item {duplicated.Key} appears more than once in the ingredient list.",
                nameof(ingredients));
        }

        _ingredients.Clear();

        var lineNumber = 1;

        foreach (var ingredient in ingredients)
        {
            ArgumentNullException.ThrowIfNull(ingredient);
            ingredient.SetLineNumber(lineNumber++);
            _ingredients.Add(ingredient);
        }
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Recipe code is required.", nameof(code));
        }

        var normalized = code.Trim().ToUpperInvariant();

        if (normalized.Length > 40)
        {
            throw new ArgumentException("Recipe code cannot exceed 40 characters.", nameof(code));
        }

        return normalized;
    }

    private static int RequireStrictlyPositivePortions(int yieldPortions)
    {
        if (yieldPortions < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(yieldPortions),
                yieldPortions,
                "The recipe yield must be at least one portion.");
        }

        return yieldPortions;
    }

    private static RecipeCategory RequireDefinedCategory(RecipeCategory category)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentException("Recipe category is not valid.", nameof(category));
        }

        return category;
    }

    private static string RequireValue(string value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }

    private static string? NormalizeOptional(string? value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }
}
