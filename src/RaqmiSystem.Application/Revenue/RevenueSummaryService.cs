using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Revenue;

/// <summary>
/// Additionne des recettes catégorie par catégorie. Calcul pur, sans base : le service EF lui
/// passe les lignes déjà chargées et, s'il les connaît, les catégories du paramétrage pour
/// libeller et ordonner les totaux ; un code absent du paramétrage est tout de même totalisé,
/// libellé par son code, à la fin - un montant enregistré ne disparaît jamais d'une synthèse.
/// </summary>
public sealed class RevenueSummaryService
{
    public RevenueSummary Calculate(
        IEnumerable<DailyRevenueDraft> revenueLines,
        IReadOnlyList<RevenueCategory>? categories = null)
    {
        ArgumentNullException.ThrowIfNull(revenueLines);

        var totalsByCode = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var draft in revenueLines)
        {
            foreach (var line in draft.Lines)
            {
                var code = RevenueCategoryCodes.Normalize(line.CategoryCode, nameof(revenueLines));
                totalsByCode[code] = totalsByCode.GetValueOrDefault(code) + line.Amount;
            }
        }

        var known = categories ?? [];

        var ordered = known
            .Select(category => new RevenueCategoryTotal(category.Code, category.Label, totalsByCode.GetValueOrDefault(category.Code)))
            .ToList();

        var knownCodes = known.Select(category => category.Code).ToHashSet(StringComparer.Ordinal);

        ordered.AddRange(totalsByCode
            .Where(pair => !knownCodes.Contains(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new RevenueCategoryTotal(pair.Key, pair.Key, pair.Value)));

        var accommodation = totalsByCode.GetValueOrDefault(RevenueCategoryCodes.Accommodation);
        var food = totalsByCode.GetValueOrDefault(RevenueCategoryCodes.Food);
        var beverage = totalsByCode.GetValueOrDefault(RevenueCategoryCodes.Beverage);
        var total = totalsByCode.Values.Sum();

        return new RevenueSummary(
            accommodation,
            food,
            beverage,
            total - accommodation - food - beverage,
            total,
            ordered);
    }
}
