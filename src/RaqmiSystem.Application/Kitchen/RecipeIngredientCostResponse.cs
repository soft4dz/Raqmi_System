namespace RaqmiSystem.Application.Kitchen;

/// <summary>
/// One costed ingredient line. <see cref="HasCost"/> is false when the stock module knows no
/// weighted average cost for the item (typically an item that never entered stock): the line
/// is then EXCLUDED from the recipe total and flagged, rather than silently costed at zero -
/// a silently wrong cost is worse than an honest gap.
/// </summary>
public sealed record RecipeIngredientCostResponse(
    int LineNumber,
    string ItemCode,
    decimal Quantity,
    string? UnitOfMeasure,
    decimal? AverageUnitCost,
    decimal? LineCost,
    bool HasCost);
