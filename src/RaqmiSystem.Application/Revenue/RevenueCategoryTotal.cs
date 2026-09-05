namespace RaqmiSystem.Application.Revenue;

/// <summary>Total d'une catégorie de recettes sur un périmètre (synthèse d'une période, d'une unité...).</summary>
public sealed record RevenueCategoryTotal(string CategoryCode, string CategoryLabel, decimal Amount);
