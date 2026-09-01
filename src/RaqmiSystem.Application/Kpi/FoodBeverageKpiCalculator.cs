using RaqmiSystem.Domain.Inventory;
using RaqmiSystem.Domain.Kpi;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Les indicateurs de restauration et de stocks : cout matiere, ratios sur chiffre d'affaires,
/// rotation et ruptures.
///
/// LE COUT MATIERE EST UNE SORTIE DE STOCK, PAS UN ACHAT. C'est la distinction qui separe un
/// vrai food cost d'un ratio d'achats : ce qui est achete et pose sur une etagere n'a rien
/// coute au resultat du mois, seul ce qui en sort l'a fait. Le calculateur ne compte donc que
/// les mouvements de nature Consommation, valorises au cout unitaire porte par le mouvement.
///
/// UNE SORTIE NON VALORISEE N'EST PAS UNE SORTIE GRATUITE. Un mouvement sans cout unitaire est
/// compte comme donnee manquante et degrade la qualite de l'indicateur : un food cost qui
/// ignorerait silencieusement les sorties non valorisees serait faussement rassurant, ce qui est
/// exactement l'inverse du service rendu.
/// </summary>
public sealed class FoodBeverageKpiCalculator
{
    private const string NoFoodRevenue =
        "Aucun chiffre d'affaires restauration valide sur la periode : le ratio n'a pas d'objet.";

    private const string NoBeverageRevenue =
        "Aucun chiffre d'affaires boissons valide sur la periode : le ratio n'a pas d'objet.";

    private const string NoFoodBeverageRevenue =
        "Aucun chiffre d'affaires restauration ni boissons valide sur la periode.";

    private const string NoStock =
        "Le stock moyen valorise de la periode est nul : la rotation n'a pas d'objet.";

    private const string NoItem = "Aucun article actif dans le perimetre.";

    public IEnumerable<KpiMeasure> Compute(KpiPeriod period, string? unitCode, KpiFactSet facts)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(facts);

        var categories = facts.StockItems.ToDictionary(
            item => item.ItemCode,
            item => item.Category,
            StringComparer.OrdinalIgnoreCase);

        var consumptions = facts.StockMovements
            .Where(movement => movement.Kind == StockMovementKind.Consumption)
            .ToArray();

        var unvalued = consumptions.Count(movement => movement.UnitCost is null);

        var foodCost = SumConsumption(consumptions, categories, StockItemCategory.Alimentaire);
        var beverageCost = SumConsumption(consumptions, categories, StockItemCategory.Boisson);

        var validated = facts.Revenues
            .Where(revenue => revenue.Status == DailyRevenueStatus.Validated)
            .ToArray();

        var foodRevenue = validated.Sum(revenue => revenue.Food);
        var beverageRevenue = validated.Sum(revenue => revenue.Beverage);

        yield return Warn(KpiMeasure.Amount(KpiCodes.FoodCostAmount, unitCode, foodCost), unvalued);
        yield return Warn(KpiMeasure.Amount(KpiCodes.BeverageCostAmount, unitCode, beverageCost), unvalued);

        yield return Warn(
            KpiMeasure.Ratio(
                KpiCodes.FoodCostRate, unitCode, foodCost, foodRevenue, KpiMath.Percent, NoFoodRevenue),
            unvalued);

        yield return Warn(
            KpiMeasure.Ratio(
                KpiCodes.BeverageCostRate, unitCode, beverageCost, beverageRevenue, KpiMath.Percent,
                NoBeverageRevenue),
            unvalued);

        yield return Warn(
            KpiMeasure.Ratio(
                KpiCodes.TotalCostOfSalesRate,
                unitCode,
                foodCost + beverageCost,
                foodRevenue + beverageRevenue,
                KpiMath.Percent,
                NoFoodBeverageRevenue),
            unvalued);

        foreach (var measure in ComputeStockMeasures(unitCode, facts, consumptions, categories))
        {
            yield return measure;
        }
    }

    private static decimal SumConsumption(
        IReadOnlyCollection<KpiStockMovementFact> consumptions,
        IReadOnlyDictionary<string, StockItemCategory> categories,
        StockItemCategory category)
    {
        return consumptions
            .Where(movement => movement.UnitCost is not null
                && categories.TryGetValue(movement.ItemCode, out var itemCategory)
                && itemCategory == category)
            .Sum(movement => movement.Quantity * movement.UnitCost!.Value);
    }

    /// <summary>
    /// Rotation et ruptures. Le stock est RECONSTITUE par cumul des mouvements signes : Raqmi
    /// System ne stocke pas de quantite courante par article, il tient un registre de mouvements
    /// - ce qui est le bon choix comptable, et impose simplement de sommer pour connaitre un
    /// etat.
    ///
    /// La valorisation du stock utilise le cout unitaire porte par chaque mouvement, faute d'un
    /// cout moyen pondere stocke par article. C'est une approximation, et elle est dite : sur un
    /// article dont le prix a fortement varie, le stock d'ouverture est valorise aux couts
    /// historiques de ses entrees successives et non au dernier cours.
    /// </summary>
    private static IEnumerable<KpiMeasure> ComputeStockMeasures(
        string? unitCode,
        KpiFactSet facts,
        IReadOnlyCollection<KpiStockMovementFact> consumptions,
        IReadOnlyDictionary<string, StockItemCategory> categories)
    {
        var openingValue = SumSignedValue(facts.OpeningStockMovements);
        var periodValue = SumSignedValue(facts.StockMovements);
        var closingValue = openingValue + periodValue;
        var averageStock = (openingValue + closingValue) / 2m;

        var consumedValue = consumptions
            .Where(movement => movement.UnitCost is not null)
            .Sum(movement => movement.Quantity * movement.UnitCost!.Value);

        yield return KpiMeasure.Ratio(
            KpiCodes.InventoryTurnover, unitCode, consumedValue, averageStock, KpiMath.Divide, NoStock)
            .WithWarning(
                "Le stock est valorise au cout unitaire de chaque mouvement, faute d'un cout moyen "
                + "pondere conserve par article.");

        var activeItems = facts.StockItems.Where(item => item.IsActive).ToArray();

        if (activeItems.Length == 0)
        {
            yield return KpiMeasure.Missing(KpiCodes.StockOutRate, unitCode, NoItem);
            yield break;
        }

        var quantities = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var movement in facts.OpeningStockMovements.Concat(facts.StockMovements))
        {
            quantities[movement.ItemCode] =
                quantities.GetValueOrDefault(movement.ItemCode) + movement.SignedQuantity;
        }

        // Un article actif dont aucun mouvement n'a jamais ete enregistre est en rupture au sens
        // du registre : son stock reconstitue vaut zero. C'est la lecture honnete d'un article
        // cree mais jamais approvisionne.
        var stockOuts = activeItems.Count(item => quantities.GetValueOrDefault(item.ItemCode) <= 0m);

        yield return KpiMeasure.Ratio(
            KpiCodes.StockOutRate, unitCode, stockOuts, activeItems.Length, KpiMath.Percent, NoItem);

        _ = categories;
    }

    private static decimal SumSignedValue(IReadOnlyCollection<KpiStockMovementFact> movements)
    {
        return movements
            .Where(movement => movement.UnitCost is not null)
            .Sum(movement => movement.SignedQuantity * movement.UnitCost!.Value);
    }

    private static KpiMeasure Warn(KpiMeasure measure, int unvaluedMovementCount)
    {
        return unvaluedMovementCount == 0
            ? measure
            : measure.WithWarning(
                $"{unvaluedMovementCount} sortie(s) de stock sans cout unitaire ne sont pas "
                + "valorisees : le cout matiere reel est superieur au montant affiche.");
    }
}
