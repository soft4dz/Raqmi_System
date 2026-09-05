namespace RaqmiSystem.Application.Budgeting;

/// <summary>
/// Une catégorie à faire figurer dans le rapport d'écarts, avec son libellé d'affichage. La liste
/// est fournie par l'appelant (les catégories qui s'appliquent à l'unité, dans l'ordre du
/// paramétrage) : le calculateur n'a pas de liste figée.
/// </summary>
public sealed record BudgetVarianceCategory(string Code, string Label);
