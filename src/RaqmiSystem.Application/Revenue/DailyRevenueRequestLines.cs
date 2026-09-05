using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Revenue;

/// <summary>
/// Une recette s'envoie sous deux formes : des lignes par catégorie (la forme générale) ou les
/// quatre montants hôteliers historiques (l'ancien corps, toujours accepté pour que les clients
/// existants continuent de fonctionner sans changement). Cette classe choisit la forme et ramène
/// l'ancienne à la nouvelle ; le contrôle d'existence des catégories relève du service.
/// </summary>
public static class DailyRevenueRequestLines
{
    public static IReadOnlyList<DailyRevenueLineRequest> Resolve(
        IReadOnlyCollection<DailyRevenueLineRequest>? lines,
        decimal accommodation,
        decimal food,
        decimal beverage,
        decimal other)
    {
        var hasLines = lines is { Count: > 0 };
        var hasLegacyAmounts = accommodation != 0m || food != 0m || beverage != 0m || other != 0m;

        // Les deux formes dans le même corps n'ont pas de sens univoque : additionner, écraser ou
        // ignorer l'une des deux persisterait une recette que l'appelant n'a pas voulue.
        if (hasLines && hasLegacyAmounts)
        {
            throw new ArgumentException(
                "Send either category lines or the four legacy amounts (accommodation, food, beverage, other), not both.",
                nameof(lines));
        }

        if (hasLines)
        {
            return lines!.ToArray();
        }

        return
        [
            new DailyRevenueLineRequest(RevenueCategoryCodes.Accommodation, accommodation),
            new DailyRevenueLineRequest(RevenueCategoryCodes.Food, food),
            new DailyRevenueLineRequest(RevenueCategoryCodes.Beverage, beverage),
            new DailyRevenueLineRequest(RevenueCategoryCodes.Other, other)
        ];
    }
}
