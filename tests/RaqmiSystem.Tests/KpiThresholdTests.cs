using RaqmiSystem.Application.Kpi;
using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Tests;

/// <summary>
/// Les bornes de pilotage : leur coherence est verifiee par le DOMAINE, ce qui protege l'API et
/// le poste client de la meme facon.
/// </summary>
public sealed class KpiThresholdTests
{
    [Fact]
    public void A_higher_is_better_indicator_wants_its_favorable_bound_above_the_critical_one()
    {
        var threshold = new KpiThreshold(KpiCodes.OccupancyRate, null, 65m, 40m, 70m, "Direction");

        Assert.Equal(65m, threshold.FavorableThreshold);
        Assert.Equal(40m, threshold.CriticalThreshold);
        Assert.True(threshold.IsActive);
    }

    [Fact]
    public void Reversed_bounds_on_a_higher_is_better_indicator_are_refused()
    {
        Assert.Throws<ArgumentException>(() =>
            new KpiThreshold(KpiCodes.OccupancyRate, null, 40m, 65m, null, null));
    }

    [Fact]
    public void A_lower_is_better_indicator_wants_the_opposite_ordering()
    {
        var threshold = new KpiThreshold(KpiCodes.FoodCostRate, null, 30m, 35m, 28m, "F&B");

        Assert.Equal(30m, threshold.FavorableThreshold);

        Assert.Throws<ArgumentException>(() =>
            new KpiThreshold(KpiCodes.FoodCostRate, null, 35m, 30m, null, null));
    }

    [Fact]
    public void A_neutral_indicator_cannot_carry_bounds_at_all()
    {
        // Une capacite ne se juge pas : lui poser un seuil produirait un verdict qui ne veut
        // rien dire.
        Assert.Throws<ArgumentException>(() =>
            new KpiThreshold(KpiCodes.RoomsAvailable, null, 100m, 50m, null, null));
    }

    [Fact]
    public void A_neutral_indicator_may_still_carry_a_target()
    {
        var threshold = new KpiThreshold(KpiCodes.RoomsAvailable, null, null, null, 3_000m, null);

        Assert.Equal(3_000m, threshold.TargetValue);
        Assert.False(threshold.HasThresholdBounds());
    }

    [Fact]
    public void A_threshold_must_carry_at_least_one_bound_or_a_target()
    {
        Assert.Throws<ArgumentException>(() =>
            new KpiThreshold(KpiCodes.OccupancyRate, null, null, null, null, null));
    }

    [Fact]
    public void An_unknown_indicator_code_is_refused()
    {
        Assert.Throws<ArgumentException>(() =>
            new KpiThreshold("PAS_UN_INDICATEUR", null, 10m, 5m, null, null));
    }

    [Fact]
    public void A_bound_finer_than_four_decimals_is_refused_rather_than_silently_truncated()
    {
        Assert.Throws<ArgumentException>(() =>
            new KpiThreshold(KpiCodes.OccupancyRate, null, 65.123456m, 40m, null, null));
    }

    [Fact]
    public void The_unit_rule_wins_over_the_group_rule_as_a_whole()
    {
        var group = new KpiThreshold(KpiCodes.OccupancyRate, null, 65m, 40m, null, "Direction");
        var unit = new KpiThreshold(KpiCodes.OccupancyRate, "HOTEL-A", 55m, 30m, null, "Directeur A");

        var resolved = KpiThresholdSet.Resolve(KpiCodes.OccupancyRate, "HOTEL-A", [group, unit]);

        // Pas de melange : les deux bornes ET le responsable viennent de la regle de l'unite.
        Assert.Equal(55m, resolved.FavorableThreshold);
        Assert.Equal(30m, resolved.CriticalThreshold);
        Assert.Equal("Directeur A", resolved.OwnerRole);
    }

    [Fact]
    public void Another_unit_falls_back_on_the_group_rule()
    {
        var group = new KpiThreshold(KpiCodes.OccupancyRate, null, 65m, 40m, null, "Direction");
        var unit = new KpiThreshold(KpiCodes.OccupancyRate, "HOTEL-A", 55m, 30m, null, null);

        var resolved = KpiThresholdSet.Resolve(KpiCodes.OccupancyRate, "HOTEL-B", [group, unit]);

        Assert.Equal(65m, resolved.FavorableThreshold);
    }

    [Fact]
    public void A_deactivated_rule_stops_applying()
    {
        var group = new KpiThreshold(KpiCodes.OccupancyRate, null, 65m, 40m, null, null);
        group.Deactivate();

        var resolved = KpiThresholdSet.Resolve(KpiCodes.OccupancyRate, null, [group]);

        Assert.Same(KpiThresholdSet.None, resolved);
    }

    [Fact]
    public void Without_any_rule_no_verdict_is_rendered()
    {
        var resolved = KpiThresholdSet.Resolve(KpiCodes.OccupancyRate, "HOTEL-A", []);

        Assert.False(resolved.HasThreshold);
        Assert.Equal(
            KpiHealth.Unknown,
            KpiMath.Classify(10m, resolved.FavorableThreshold, resolved.CriticalThreshold, KpiPolarity.HigherIsBetter));
    }
}

internal static class KpiThresholdTestExtensions
{
    /// <summary>Lecture d'appoint pour les tests : la regle porte-t-elle des bornes ?</summary>
    public static bool HasThresholdBounds(this KpiThreshold threshold)
    {
        return threshold.FavorableThreshold is not null || threshold.CriticalThreshold is not null;
    }
}
