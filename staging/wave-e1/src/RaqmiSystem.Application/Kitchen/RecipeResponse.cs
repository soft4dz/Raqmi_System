using RaqmiSystem.Domain.Kitchen;

namespace RaqmiSystem.Application.Kitchen;

public sealed record RecipeResponse(
    Guid Id,
    string Code,
    string Name,
    RecipeCategory Category,
    int YieldPortions,
    string? Allergens,
    string? Instructions,
    bool IsActive,
    IReadOnlyCollection<RecipeIngredientResponse> Ingredients,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
