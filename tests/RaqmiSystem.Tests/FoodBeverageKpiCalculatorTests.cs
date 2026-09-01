using RaqmiSystem.Application.Kpi;
using RaqmiSystem.Domain.Inventory;
using RaqmiSystem.Domain.Kpi;
using static RaqmiSystem.Tests.KpiTestData;

namespace RaqmiSystem.Tests;

/// <summary>
/// Le cout matiere et les indicateurs de stock. Le point cardinal verifie ici : le cout matiere
/// est une SORTIE de stock valorisee, pas un achat.
/// </summary>
public sealed class FoodBeverageKpiCalculatorTests
{
    private readonly FoodBeverageKpiCalculator calculator = new();

    private IReadOnlyDictionary<string, KpiMeasure> Compute(KpiFactSet facts)
    {
        return calculator.Compute(January, UnitA, facts).ToDictionary(measure => measure.Code);
    }

    [Fact]
    public void Food_cost_is_the_valued_consumption_over_food_revenue()
    {
        var facts = Facts(
            revenues: [Revenue(Jan1, food: 1_000m)],
            stockItems: [Item("VIANDE")],
            stockMovements: [Consumption("VIANDE", 10m, 30m)]);

        var measures = Compute(facts);

        Assert.Equal(300m, measures[KpiCodes.FoodCostAmount].Value);
        Assert.Equal(30m, measures[KpiCodes.FoodCostRate].Value);
    }

    [Fact]
    public void A_purchase_that_stays_on_the_shelf_costs_nothing_yet()
    {
        // La distinction qui separe un vrai food cost d'un ratio d'achats.
        var facts = Facts(
            revenues: [Revenue(Jan1, food: 1_000m)],
            stockItems: [Item("VIANDE")],
            stockMovements: [Entry("VIANDE", 100m, 30m)]);

        Assert.Equal(0m, Compute(facts)[KpiCodes.FoodCostAmount].Value);
    }

    [Fact]
    public void Beverage_cost_uses_its_own_family_and_its_own_revenue_column()
    {
        var facts = Facts(
            revenues: [Revenue(Jan1, food: 1_000m, beverage: 500m)],
            stockItems: [Item("VIANDE"), Item("VIN", StockItemCategory.Boisson)],
            stockMovements: [Consumption("VIANDE", 10m, 30m), Consumption("VIN", 5m, 20m)]);

        var measures = Compute(facts);

        Assert.Equal(100m, measures[KpiCodes.BeverageCostAmount].Value);
        Assert.Equal(20m, measures[KpiCodes.BeverageCostRate].Value);

        // Le ratio global reunit les deux couts et les deux colonnes de recettes.
        Assert.Equal(26.67m, measures[KpiCodes.TotalCostOfSalesRate].Value);
    }

    [Fact]
    public void An_unvalued_issue_degrades_the_quality_instead_of_counting_as_free()
    {
        // Un food cost qui ignorerait silencieusement les sorties non valorisees serait
        // faussement rassurant - exactement l'inverse du service rendu.
        var facts = Facts(
            revenues: [Revenue(Jan1, food: 1_000m)],
            stockItems: [Item("VIANDE"), Item("POISSON")],
            stockMovements: [Consumption("VIANDE", 10m, 30m), Consumption("POISSON", 5m, null)]);

        var rate = Compute(facts)[KpiCodes.FoodCostRate];

        Assert.Equal(30m, rate.Value);
        Assert.Equal(KpiQuality.Partial, rate.Quality);
        Assert.Contains(rate.MissingData, reason => reason.Contains("sans cout unitaire", StringComparison.Ordinal));
    }

    [Fact]
    public void Food_cost_without_food_revenue_has_no_object()
    {
        var facts = Facts(
            stockItems: [Item("VIANDE")],
            stockMovements: [Consumption("VIANDE", 10m, 30m)]);

        var rate = Compute(facts)[KpiCodes.FoodCostRate];

        Assert.Null(rate.Value);
        Assert.Equal(KpiQuality.MissingData, rate.Quality);
    }

    [Fact]
    public void Inventory_turnover_divides_consumption_by_the_average_valued_stock()
    {
        // Ouverture 1 000 (50 x 20), periode -600 (30 x 20) : cloture 400, moyenne 700.
        var facts = Facts(
            stockItems: [Item("VIANDE")],
            openingStockMovements: [Entry("VIANDE", 50m, 20m, date: new DateOnly(2025, 12, 1))],
            stockMovements: [Consumption("VIANDE", 30m, 20m)]);

        Assert.Equal(0.86m, Compute(facts)[KpiCodes.InventoryTurnover].Value);
    }

    [Fact]
    public void Stock_out_rate_reads_the_reconstructed_closing_quantity()
    {
        var facts = Facts(
            stockItems: [Item("VIANDE"), Item("POISSON"), Item("RETIRE", isActive: false)],
            openingStockMovements: [Entry("VIANDE", 50m, 20m), Entry("POISSON", 10m, 20m)],
            stockMovements: [Consumption("POISSON", 10m, 20m)]);

        // Deux articles actifs, un seul tombe a zero.
        Assert.Equal(50m, Compute(facts)[KpiCodes.StockOutRate].Value);
    }

    [Fact]
    public void An_active_item_that_was_never_supplied_counts_as_a_stock_out()
    {
        var facts = Facts(stockItems: [Item("JAMAIS-RECU")]);

        Assert.Equal(100m, Compute(facts)[KpiCodes.StockOutRate].Value);
    }

    [Fact]
    public void Without_any_item_the_stock_out_rate_has_no_object()
    {
        var measure = Compute(Facts())[KpiCodes.StockOutRate];

        Assert.Null(measure.Value);
        Assert.Equal(KpiQuality.MissingData, measure.Quality);
    }
}
