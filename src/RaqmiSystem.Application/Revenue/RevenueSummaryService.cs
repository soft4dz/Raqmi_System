namespace RaqmiSystem.Application.Revenue;

public sealed class RevenueSummaryService
{
    public RevenueSummary Calculate(IEnumerable<DailyRevenueDraft> revenueLines)
    {
        var lines = revenueLines.ToArray();

        var accommodation = lines.Sum(line => line.Accommodation);
        var food = lines.Sum(line => line.Food);
        var beverage = lines.Sum(line => line.Beverage);
        var other = lines.Sum(line => line.Other);

        return new RevenueSummary(
            accommodation,
            food,
            beverage,
            other,
            accommodation + food + beverage + other);
    }
}
