namespace RaqmiSystem.Application.Kitchen;

/// <summary>
/// Material cost of a recipe sheet, computed on demand from the CURRENT weighted average
/// costs (PMP) served by the stock module - not from historical costs. <see cref="CostBasis"/>
/// carries that caveat verbatim so every consumer (screen, print, export) can display it.
///
/// When at least one ingredient has no known cost, <see cref="HasMissingCosts"/> is true,
/// <see cref="Warning"/> explains it, and <see cref="TotalCost"/> /
/// <see cref="CostPerPortion"/> only cover the costed ingredients - they are then a LOWER
/// BOUND of the real cost, never a fabricated exact figure.
/// </summary>
public sealed record RecipeCostResponse(
    string RecipeCode,
    string RecipeName,
    int YieldPortions,
    IReadOnlyCollection<RecipeIngredientCostResponse> Ingredients,
    decimal TotalCost,
    decimal CostPerPortion,
    bool HasMissingCosts,
    string? Warning,
    DateTimeOffset ComputedAt,
    string CostBasis);
