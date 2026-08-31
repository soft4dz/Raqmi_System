using RaqmiSystem.Domain.Kitchen;

namespace RaqmiSystem.Application.Kitchen;

public sealed record UpdateRecipeRequest(
    string Name,
    RecipeCategory Category,
    int YieldPortions,
    string? Allergens,
    string? Instructions,
    IReadOnlyCollection<RecipeIngredientRequest> Ingredients);
