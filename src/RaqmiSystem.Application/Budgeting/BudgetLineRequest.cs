namespace RaqmiSystem.Application.Budgeting;

/// <summary>Un objectif mensuel : le mois, le code de la catégorie de recettes et le montant.</summary>
public sealed record BudgetLineRequest(
    int Month,
    string Category,
    decimal AmountTarget);
