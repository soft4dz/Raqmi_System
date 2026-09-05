using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Tests;

public sealed class RevenueSummaryServiceTests
{
    [Fact]
    public void Calculate_returns_total_by_revenue_category()
    {
        var service = new RevenueSummaryService();

        var result = service.Calculate([
            new DailyRevenueDraft(new DateOnly(2026, 8, 1), "EL-MANAR", 100m, 20m, 10m, 5m),
            new DailyRevenueDraft(new DateOnly(2026, 8, 1), "EL-MARSA", 200m, 40m, 15m, 10m)
        ]);

        Assert.Equal(300m, result.Accommodation);
        Assert.Equal(60m, result.Food);
        Assert.Equal(25m, result.Beverage);
        Assert.Equal(15m, result.Other);
        Assert.Equal(400m, result.Total);
    }

    /// <summary>
    /// Les totaux suivent les catégories du paramétrage, dans son ordre et avec ses libellés ; un
    /// code que le paramétrage ne connaît pas est tout de même totalisé, à la fin. Les quatre
    /// montants historiques sont dérivés des mêmes totaux, Other absorbant tout ce qui n'est pas
    /// hébergement, restauration ou bar.
    /// </summary>
    [Fact]
    public void Calculate_totals_each_configured_category_and_folds_the_rest_into_other()
    {
        var service = new RevenueSummaryService();

        var hotel = new DailyRevenueDraft(new DateOnly(2026, 8, 1), "EL-MANAR", 100m, 20m, 10m, 5m);

        var shop = new DailyRevenueDraft(
            new DateOnly(2026, 8, 1),
            "SHOP",
            [
                new DailyRevenueLineRequest(RevenueCategoryCodes.Merchandise, 200m),
                new DailyRevenueLineRequest("services", 40m),
                new DailyRevenueLineRequest("DELIVERY", 7m)
            ]);

        var result = service.Calculate([hotel, shop], RevenueCategoryCatalog.All);

        Assert.Equal(
            [
                (RevenueCategoryCodes.Accommodation, "Hébergement", 100m),
                (RevenueCategoryCodes.Food, "Restauration", 20m),
                (RevenueCategoryCodes.Beverage, "Bar", 10m),
                (RevenueCategoryCodes.Other, "Autres", 5m),
                (RevenueCategoryCodes.Merchandise, "Ventes de marchandises", 200m),
                (RevenueCategoryCodes.Services, "Prestations de services", 40m),
                (RevenueCategoryCodes.OtherIncome, "Autres produits", 0m),
                ("DELIVERY", "DELIVERY", 7m)
            ],
            result.Categories.Select(total => (total.CategoryCode, total.CategoryLabel, total.Amount)));

        Assert.Equal(100m, result.Accommodation);
        Assert.Equal(20m, result.Food);
        Assert.Equal(10m, result.Beverage);
        Assert.Equal(252m, result.Other);
        Assert.Equal(382m, result.Total);
        Assert.Equal(result.Total, result.Accommodation + result.Food + result.Beverage + result.Other);
    }

    [Fact]
    public void Calculate_without_configured_categories_orders_totals_by_code()
    {
        var result = new RevenueSummaryService().Calculate([
            new DailyRevenueDraft(new DateOnly(2026, 8, 1), "SHOP", [
                new DailyRevenueLineRequest(RevenueCategoryCodes.Services, 40m),
                new DailyRevenueLineRequest(RevenueCategoryCodes.Merchandise, 200m)
            ])
        ]);

        Assert.Equal(
            [RevenueCategoryCodes.Merchandise, RevenueCategoryCodes.Services],
            result.Categories.Select(total => total.CategoryCode));

        Assert.All(result.Categories, total => Assert.Equal(total.CategoryCode, total.CategoryLabel));
        Assert.Equal(240m, result.Other);
        Assert.Equal(240m, result.Total);
    }
}
