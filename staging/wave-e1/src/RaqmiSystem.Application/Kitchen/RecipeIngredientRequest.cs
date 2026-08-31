namespace RaqmiSystem.Application.Kitchen;

public sealed record RecipeIngredientRequest(
    string ItemCode,
    decimal Quantity,
    string? Notes);
