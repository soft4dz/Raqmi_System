using RaqmiSystem.Domain.Kitchen;

namespace RaqmiSystem.Application.Kitchen;

public sealed record CreateRecipeRequest(
    string Code,
    string Name,
    RecipeCategory Category,
    int YieldPortions,
    string? Allergens,
    string? Instructions,
    IReadOnlyCollection<RecipeIngredientRequest> Ingredients);
