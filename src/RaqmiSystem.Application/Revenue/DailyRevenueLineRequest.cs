namespace RaqmiSystem.Application.Revenue;

/// <summary>Un montant pour une catégorie de recettes, dans un corps de création ou de mise à jour.</summary>
public sealed record DailyRevenueLineRequest(string CategoryCode, decimal Amount);
