namespace RaqmiSystem.Domain.Kitchen;

/// <summary>
/// Functional category of a recipe sheet. SousPreparation covers intermediate
/// preparations (stocks, sauces, doughs) that are themselves used by other recipes.
/// </summary>
public enum RecipeCategory
{
    Entree = 0,
    Plat = 1,
    Dessert = 2,
    Boisson = 3,
    SousPreparation = 4
}
