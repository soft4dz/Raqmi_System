using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Revenue;

/// <summary>
/// Une recette réduite à ce que la synthèse additionne : sa date, son unité et ses montants par
/// catégorie. Sans base de données ni graphe d'entités, pour que <see cref="RevenueSummaryService"/>
/// se teste seul.
/// </summary>
public sealed record DailyRevenueDraft(
    DateOnly BusinessDate,
    string HotelUnitCode,
    IReadOnlyCollection<DailyRevenueLineRequest> Lines)
{
    /// <summary>Forme historique à quatre montants, mappée sur les codes hôteliers.</summary>
    public DailyRevenueDraft(
        DateOnly businessDate,
        string hotelUnitCode,
        decimal accommodation,
        decimal food,
        decimal beverage,
        decimal other)
        : this(businessDate, hotelUnitCode, DailyRevenueRequestLines.Resolve(null, accommodation, food, beverage, other))
    {
    }

    public decimal Total => Lines.Sum(line => line.Amount);

    public decimal AmountOf(string categoryCode)
    {
        var normalized = RevenueCategoryCodes.Normalize(categoryCode, nameof(categoryCode));

        return Lines.Where(line => string.Equals(line.CategoryCode, normalized, StringComparison.OrdinalIgnoreCase))
            .Sum(line => line.Amount);
    }
}
