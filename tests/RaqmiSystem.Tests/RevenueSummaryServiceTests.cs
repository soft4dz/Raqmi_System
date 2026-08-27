using RaqmiSystem.Application.Revenue;

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
}
