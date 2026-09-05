namespace RaqmiSystem.Application.Budgeting;

/// <summary>
/// Un objectif mensuel tel que renvoyé au client. <see cref="CategoryLabel"/> accompagne le code
/// pour que l'écran affiche la catégorie sans table de correspondance locale.
/// </summary>
public sealed record BudgetLineResponse(
    Guid Id,
    int Month,
    string Category,
    decimal AmountTarget,
    string CategoryLabel);
