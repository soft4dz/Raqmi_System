namespace RaqmiSystem.Application.Revenue;

/// <summary>
/// Totaux d'un ensemble de recettes : par catégorie (<see cref="Categories"/>), et sous la forme
/// historique des quatre montants hôteliers - <see cref="Other"/> additionnant tout ce qui n'est
/// ni hébergement, ni restauration, ni bar, si bien que la somme des quatre vaut toujours
/// <see cref="Total"/>.
/// </summary>
public sealed record RevenueSummary(
    decimal Accommodation,
    decimal Food,
    decimal Beverage,
    decimal Other,
    decimal Total,
    IReadOnlyCollection<RevenueCategoryTotal> Categories);
