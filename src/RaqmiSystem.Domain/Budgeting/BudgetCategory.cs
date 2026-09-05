using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Domain.Budgeting;

/// <summary>
/// Alias de compatibilité : les quatre codes hôteliers historiques, sous le nom que les appelants
/// du module budget utilisaient quand la catégorie était une énumération figée. Une ligne de
/// budget référence désormais une <see cref="RevenueCategory"/> par son code — n'importe lequel
/// des codes paramétrés, pas seulement ces quatre. Préférer <see cref="RevenueCategoryCodes"/>
/// dans le code nouveau ; ceci n'existe que pour ne pas casser les appelants existants.
/// </summary>
public static class BudgetCategory
{
    public const string Accommodation = RevenueCategoryCodes.Accommodation;
    public const string Food = RevenueCategoryCodes.Food;
    public const string Beverage = RevenueCategoryCodes.Beverage;
    public const string Other = RevenueCategoryCodes.Other;
}
