namespace RaqmiSystem.Application.Kitchen;

public sealed record RecipeIngredientResponse(
    Guid Id,
    int LineNumber,
    string ItemCode,
    decimal Quantity,
    string? Notes);
