namespace RaqmiSystem.Application.Revenue;

/// <summary>
/// Corps de mise à jour d'une recette : mêmes deux formes que <see cref="CreateDailyRevenueRequest"/>.
/// Le jeu de lignes envoyé remplace le jeu existant.
/// </summary>
public sealed record UpdateDailyRevenueRequest(
    decimal Accommodation = 0m,
    decimal Food = 0m,
    decimal Beverage = 0m,
    decimal Other = 0m,
    string? Notes = null,
    IReadOnlyCollection<DailyRevenueLineRequest>? Lines = null);
