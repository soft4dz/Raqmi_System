namespace RaqmiSystem.Application.Revenue;

/// <summary>
/// Corps de création d'une recette. Deux formes sont acceptées, jamais les deux à la fois :
/// <see cref="Lines"/> (un montant par code de catégorie, la forme générale), ou les quatre
/// montants hôteliers historiques, mappés sur les codes ACCOMMODATION / FOOD / BEVERAGE / OTHER
/// (voir <see cref="DailyRevenueRequestLines"/>).
/// </summary>
public sealed record CreateDailyRevenueRequest(
    DateOnly BusinessDate,
    string HotelUnitCode,
    decimal Accommodation = 0m,
    decimal Food = 0m,
    decimal Beverage = 0m,
    decimal Other = 0m,
    string? Notes = null,
    IReadOnlyCollection<DailyRevenueLineRequest>? Lines = null);
