using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Tests;

/// <summary>
/// L'historisation. La garantie centrale : un instantane cloture n'est JAMAIS reecrit par un
/// recalcul, et l'entite elle-meme le refuse - pas seulement le service - pour qu'aucun chemin
/// d'ecriture ne puisse la contourner.
/// </summary>
public sealed class KpiSnapshotTests
{
    private static readonly DateOnly From = new(2026, 1, 1);
    private static readonly DateOnly To = new(2026, 1, 31);
    private static readonly DateTimeOffset Now = new(2026, 2, 1, 8, 0, 0, TimeSpan.Zero);

    private static KpiSnapshot Snapshot(decimal? value = 62.5m)
    {
        return new KpiSnapshot(
            KpiCodes.OccupancyRate,
            "HOTEL-A",
            From,
            To,
            KpiPeriodGranularity.Month,
            value,
            numerator: 500m,
            denominator: 800m,
            KpiQuality.Valid,
            formulaVersion: 1,
            calculatedAt: Now);
    }

    [Fact]
    public void A_new_snapshot_starts_provisional()
    {
        var snapshot = Snapshot();

        Assert.Equal(KpiSnapshotStatus.Provisional, snapshot.Status);
        Assert.False(snapshot.IsClosed);
        Assert.Null(snapshot.ClosedAt);
    }

    [Fact]
    public void A_provisional_snapshot_is_refreshed_without_ceremony()
    {
        var snapshot = Snapshot();

        snapshot.Refresh(70m, 560m, 800m, KpiQuality.Valid, 1, Now.AddDays(1));

        Assert.Equal(70m, snapshot.Value);
        Assert.Equal(560m, snapshot.Numerator);
    }

    [Fact]
    public void A_closed_snapshot_refuses_to_be_recalculated()
    {
        var snapshot = Snapshot();
        snapshot.Close("controleur", Now);

        Assert.Throws<InvalidOperationException>(() =>
            snapshot.Refresh(70m, 560m, 800m, KpiQuality.Valid, 1, Now.AddDays(1)));

        Assert.Equal(62.5m, snapshot.Value);
    }

    [Fact]
    public void Closing_records_who_froze_the_figure_and_when()
    {
        var snapshot = Snapshot();

        snapshot.Close("controleur", Now);

        Assert.True(snapshot.IsClosed);
        Assert.Equal(Now, snapshot.ClosedAt);
        Assert.Equal("controleur", snapshot.ClosedBy);
    }

    [Fact]
    public void Closing_twice_is_an_error_not_a_silent_no_op()
    {
        // La seconde cloture ecraserait la trace de qui a fige le chiffre.
        var snapshot = Snapshot();
        snapshot.Close("controleur", Now);

        Assert.Throws<InvalidOperationException>(() => snapshot.Close("autre", Now.AddDays(1)));
    }

    [Fact]
    public void Divergence_is_detected_against_the_recomputed_value()
    {
        var snapshot = Snapshot();

        Assert.False(snapshot.DivergesFrom(62.5m));
        Assert.True(snapshot.DivergesFrom(63m));

        // Une valeur devenue indisponible diverge aussi d'une valeur figee.
        Assert.True(snapshot.DivergesFrom(null));
    }

    [Fact]
    public void A_snapshot_of_an_indicator_without_value_is_legitimate()
    {
        // Historiser "cet indicateur n'avait pas de valeur ce mois-la" est une information.
        var snapshot = new KpiSnapshot(
            KpiCodes.Adr,
            null,
            From,
            To,
            KpiPeriodGranularity.Month,
            value: null,
            numerator: 0m,
            denominator: 0m,
            KpiQuality.MissingData,
            formulaVersion: 1,
            calculatedAt: Now);

        Assert.Null(snapshot.Value);
        Assert.Equal(KpiQuality.MissingData, snapshot.Quality);
        Assert.False(snapshot.DivergesFrom(null));
    }

    [Fact]
    public void An_unknown_indicator_code_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new KpiSnapshot(
            "PAS_UN_INDICATEUR", null, From, To, KpiPeriodGranularity.Month,
            1m, 1m, 1m, KpiQuality.Valid, 1, Now));
    }

    [Fact]
    public void A_period_ending_before_it_starts_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new KpiSnapshot(
            KpiCodes.OccupancyRate, null, To, From, KpiPeriodGranularity.Month,
            1m, 1m, 1m, KpiQuality.Valid, 1, Now));
    }
}
