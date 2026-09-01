using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Tests;

/// <summary>
/// L'arithmetique commune a tous les indicateurs. Ces tests epinglent LA regle du moteur :
/// un rapport dont le denominateur est nul n'existe pas, il ne vaut pas zero.
/// </summary>
public sealed class KpiMathTests
{
    [Fact]
    public void Divide_by_zero_is_null_not_zero()
    {
        Assert.Null(KpiMath.Divide(1_000_000m, 0m));
    }

    [Fact]
    public void Percent_by_zero_is_null_not_zero()
    {
        Assert.Null(KpiMath.Percent(42m, 0m));
    }

    [Fact]
    public void Divide_rounds_to_two_decimals_away_from_zero()
    {
        Assert.Equal(33.33m, KpiMath.Divide(100m, 3m));
        Assert.Equal(0.13m, KpiMath.Divide(1.25m, 10m));
    }

    [Fact]
    public void Percent_expresses_a_share_over_one_hundred()
    {
        Assert.Equal(75m, KpiMath.Percent(30m, 40m));
    }

    [Fact]
    public void Variation_against_a_zero_reference_is_null()
    {
        Assert.Null(KpiMath.Variation(0m, 500m));
    }

    [Fact]
    public void Variation_against_a_negative_reference_uses_its_magnitude()
    {
        // Un resultat qui passe de -100 a -50 s'ameliore de 50 %, pas de -50 % : diviser par la
        // reference signee retournerait le signe de la progression.
        Assert.Equal(50m, KpiMath.Variation(-100m, -50m));
    }

    [Fact]
    public void Trend_is_flat_within_the_tolerance_band()
    {
        Assert.Equal(KpiTrend.Flat, KpiMath.Trend(1000m, 1002m));
        Assert.Equal(KpiTrend.Up, KpiMath.Trend(1000m, 1100m));
        Assert.Equal(KpiTrend.Down, KpiMath.Trend(1000m, 900m));
    }

    [Fact]
    public void Trend_from_zero_exists_even_though_the_variation_does_not()
    {
        // Passer de 0 a 10 est bien une hausse, meme si la variation en pourcentage n'existe pas.
        Assert.Null(KpiMath.Variation(0m, 10m));
        Assert.Equal(KpiTrend.Up, KpiMath.Trend(0m, 10m));
    }

    [Fact]
    public void Trend_is_unknown_without_a_reference()
    {
        Assert.Equal(KpiTrend.Unknown, KpiMath.Trend(null, 10m));
        Assert.Equal(KpiTrend.Unknown, KpiMath.Trend(10m, null));
    }

    [Theory]
    [InlineData(80, KpiHealth.Favorable)]
    [InlineData(65, KpiHealth.Favorable)]
    [InlineData(50, KpiHealth.Watch)]
    [InlineData(40, KpiHealth.Critical)]
    [InlineData(10, KpiHealth.Critical)]
    public void Classify_reads_a_higher_is_better_indicator_upwards(decimal value, KpiHealth expected)
    {
        Assert.Equal(
            expected,
            KpiMath.Classify(value, favorableThreshold: 65m, criticalThreshold: 40m, KpiPolarity.HigherIsBetter));
    }

    [Theory]
    [InlineData(25, KpiHealth.Favorable)]
    [InlineData(30, KpiHealth.Favorable)]
    [InlineData(33, KpiHealth.Watch)]
    [InlineData(35, KpiHealth.Critical)]
    [InlineData(60, KpiHealth.Critical)]
    public void Classify_reads_a_lower_is_better_indicator_downwards(decimal value, KpiHealth expected)
    {
        Assert.Equal(
            expected,
            KpiMath.Classify(value, favorableThreshold: 30m, criticalThreshold: 35m, KpiPolarity.LowerIsBetter));
    }

    [Fact]
    public void Classify_without_thresholds_is_unknown_never_favorable()
    {
        // L'absence de seuil n'est pas un satisfecit.
        Assert.Equal(
            KpiHealth.Unknown,
            KpiMath.Classify(42m, null, null, KpiPolarity.HigherIsBetter));
    }

    [Fact]
    public void Classify_without_a_value_is_unknown()
    {
        Assert.Equal(
            KpiHealth.Unknown,
            KpiMath.Classify(null, 65m, 40m, KpiPolarity.HigherIsBetter));
    }

    [Fact]
    public void Classify_of_a_neutral_indicator_is_always_unknown()
    {
        Assert.Equal(
            KpiHealth.Unknown,
            KpiMath.Classify(42m, 65m, 40m, KpiPolarity.Neutral));
    }

    [Fact]
    public void Previous_year_period_clamps_29_february_to_28()
    {
        var (from, to) = KpiMath.PreviousYearPeriod(new DateOnly(2024, 2, 29), new DateOnly(2024, 3, 31));

        Assert.Equal(new DateOnly(2023, 2, 28), from);
        Assert.Equal(new DateOnly(2023, 3, 31), to);
    }
}
