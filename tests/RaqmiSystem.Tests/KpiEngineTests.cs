using RaqmiSystem.Application.Kpi;
using RaqmiSystem.Domain.Kpi;
using static RaqmiSystem.Tests.KpiTestData;

namespace RaqmiSystem.Tests;

/// <summary>
/// Les garanties d'architecture du moteur : aucun indicateur du catalogue sans reponse, aucune
/// mesure en double, et la regle de consolidation - un taux ne se moyenne jamais.
/// </summary>
public sealed class KpiEngineTests
{
    private readonly KpiEngine engine = new();

    private static readonly DateOnly Today = new(2026, 2, 1);

    /// <summary>
    /// Deux hotels de tailles tres differentes : c'est la seule configuration ou une mauvaise
    /// regle de consolidation se voit.
    /// </summary>
    private static KpiFactSet TwoUnits()
    {
        return Facts(
            units: [Unit(UnitA), Unit(UnitB)],
            rooms: [.. Rooms(2, UnitA), .. Rooms(20, UnitB)],
            stays:
            [
                // Hotel A : 2 chambres, tres rempli.
                Stay(0, Jan1, Jan1.AddDays(20), unit: UnitA),
                Stay(1, Jan1, Jan1.AddDays(20), unit: UnitA),

                // Hotel B : 20 chambres, tres vide.
                Stay(0, Jan1, Jan1.AddDays(2), unit: UnitB)
            ],
            revenues:
            [
                Revenue(Jan1, accommodation: 400_000m, unit: UnitA),
                Revenue(Jan1, accommodation: 100_000m, unit: UnitB)
            ]);
    }

    [Fact]
    public void Every_catalog_indicator_gets_an_answer_at_group_level()
    {
        var computation = engine.Compute(January, TwoUnits(), Today);

        Assert.All(KpiCatalog.All, definition =>
            Assert.NotNull(computation.Find(definition.Code, null)));
    }

    [Fact]
    public void Every_catalog_indicator_gets_an_answer_for_each_unit()
    {
        var computation = engine.Compute(January, TwoUnits(), Today);

        foreach (var unit in new[] { UnitA, UnitB })
        {
            Assert.All(KpiCatalog.All, definition =>
                Assert.NotNull(computation.Find(definition.Code, unit)));
        }
    }

    [Fact]
    public void No_indicator_is_measured_twice_on_the_same_scope()
    {
        var computation = engine.Compute(January, TwoUnits(), Today);

        var duplicates = computation.Measures
            .GroupBy(measure => (measure.Code, measure.HotelUnitCode))
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key.Code} / {group.Key.HotelUnitCode ?? "GROUPE"}")
            .ToArray();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void An_indicator_awaiting_its_source_says_what_is_missing_instead_of_showing_zero()
    {
        var computation = engine.Compute(January, TwoUnits(), Today);

        var mttr = computation.Require(KpiCodes.Mttr, null);

        Assert.Null(mttr.Value);
        Assert.Equal(KpiQuality.NotApplicable, mttr.Quality);
        Assert.Contains(mttr.MissingData, reason => reason.Contains("GMAO", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_group_only_indicator_explains_itself_when_asked_per_unit()
    {
        var computation = engine.Compute(January, TwoUnits(), Today);

        var ebitda = computation.Require(KpiCodes.Ebitda, UnitA);

        Assert.Null(ebitda.Value);
        Assert.Equal(KpiQuality.NotApplicable, ebitda.Quality);
        Assert.Contains(
            ebitda.MissingData,
            reason => reason.Contains("groupe uniquement", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// LA regle de consolidation. Le taux d'occupation du groupe est la somme des nuitees
    /// occupees divisee par la somme des nuitees disponibles - jamais la moyenne des taux des
    /// unites, qui donnerait le meme poids a un hotel de 2 chambres et a un hotel de 20.
    /// </summary>
    [Fact]
    public void Group_ratio_equals_ratio_of_sums_and_differs_from_the_average_of_rates()
    {
        var computation = engine.Compute(January, TwoUnits(), Today);

        var group = computation.Require(KpiCodes.OccupancyRate, null);
        var unitA = computation.Require(KpiCodes.OccupancyRate, UnitA);
        var unitB = computation.Require(KpiCodes.OccupancyRate, UnitB);

        var ratioOfSums = KpiMath.Percent(
            unitA.Numerator!.Value + unitB.Numerator!.Value,
            unitA.Denominator!.Value + unitB.Denominator!.Value);

        Assert.Equal(ratioOfSums, group.Value);

        // Le piege evite : la moyenne simple des deux taux ne vaut PAS le taux du groupe.
        var averageOfRates = KpiMath.Round((unitA.Value!.Value + unitB.Value!.Value) / 2m);

        Assert.NotEqual(averageOfRates, group.Value);
    }

    [Fact]
    public void Group_adr_is_a_ratio_of_sums_too()
    {
        var computation = engine.Compute(January, TwoUnits(), Today);

        var group = computation.Require(KpiCodes.Adr, null);
        var unitA = computation.Require(KpiCodes.Adr, UnitA);
        var unitB = computation.Require(KpiCodes.Adr, UnitB);

        Assert.Equal(
            KpiMath.Divide(
                unitA.Numerator!.Value + unitB.Numerator!.Value,
                unitA.Denominator!.Value + unitB.Denominator!.Value),
            group.Value);
    }

    [Fact]
    public void An_additive_indicator_consolidates_by_summation()
    {
        var computation = engine.Compute(January, TwoUnits(), Today);

        var group = computation.Require(KpiCodes.RevenueTotal, null).Value;
        var unitA = computation.Require(KpiCodes.RevenueTotal, UnitA).Value;
        var unitB = computation.Require(KpiCodes.RevenueTotal, UnitB).Value;

        Assert.Equal(unitA + unitB, group);
        Assert.Equal(500_000m, group);
    }

    [Fact]
    public void Every_aggregation_rule_of_the_catalog_holds_on_real_data()
    {
        // Le catalogue ANNONCE une regle d'agregation par indicateur ; ce test verifie que le
        // calcul direct du groupe s'y conforme, indicateur par indicateur, plutot que de laisser
        // l'annonce sans controle.
        var computation = engine.Compute(January, TwoUnits(), Today);

        var checkable = KpiCatalog.All.Where(definition =>
            definition.Availability == KpiAvailability.Implemented
            && definition.ScopeLevel == KpiScopeLevel.UnitAndGroup);

        foreach (var definition in checkable)
        {
            var group = computation.Require(definition.Code, null);
            var unitA = computation.Require(definition.Code, UnitA);
            var unitB = computation.Require(definition.Code, UnitB);

            if (group.Value is null)
            {
                continue;
            }

            switch (definition.Aggregation)
            {
                case KpiAggregation.Sum when unitA.Value is not null && unitB.Value is not null:
                    Assert.Equal(
                        KpiMath.Round(unitA.Value.Value + unitB.Value.Value),
                        group.Value);
                    break;

                case KpiAggregation.RatioOfSums or KpiAggregation.Average
                    when unitA.Numerator is not null && unitB.Numerator is not null
                        && unitA.Denominator is not null && unitB.Denominator is not null:
                {
                    var numerator = unitA.Numerator.Value + unitB.Numerator.Value;
                    var denominator = unitA.Denominator.Value + unitB.Denominator.Value;

                    var expected = definition.Unit == KpiUnit.Percentage
                        ? KpiMath.Percent(numerator, denominator)
                        : KpiMath.Divide(numerator, denominator);

                    Assert.Equal(expected, group.Value);
                    break;
                }
            }
        }
    }

    [Fact]
    public void An_empty_group_still_answers_every_indicator()
    {
        var computation = engine.Compute(January, KpiFactSet.Empty, Today);

        Assert.All(KpiCatalog.All, definition =>
            Assert.NotNull(computation.Find(definition.Code, null)));

        // Aucune unite : pas de division par zero, pas de valeur inventee.
        Assert.Null(computation.Require(KpiCodes.OccupancyRate, null).Value);
        Assert.Equal(0m, computation.Require(KpiCodes.RevenueTotal, null).Value);
    }

    [Fact]
    public void Facts_of_one_unit_never_leak_into_another()
    {
        var computation = engine.Compute(January, TwoUnits(), Today);

        Assert.Equal(400_000m, computation.Require(KpiCodes.RevenueTotal, UnitA).Value);
        Assert.Equal(100_000m, computation.Require(KpiCodes.RevenueTotal, UnitB).Value);
    }

    [Fact]
    public void The_group_capacity_is_the_one_the_per_room_indicators_divide_by()
    {
        var computation = engine.Compute(January, TwoUnits(), Today);

        Assert.Equal(
            computation.Require(KpiCodes.RoomsAvailable, null).Value,
            computation.GroupCapacity.AvailableNights);

        Assert.Equal(
            computation.Require(KpiCodes.RoomsOccupied, null).Value,
            computation.GroupCapacity.OccupiedNights);
    }

    [Fact]
    public void Require_never_returns_null_even_for_a_scope_that_was_not_computed()
    {
        var computation = engine.Compute(January, TwoUnits(), Today);

        var measure = computation.Require(KpiCodes.RevenueTotal, "UNITE-INEXISTANTE");

        Assert.Equal(KpiQuality.NotApplicable, measure.Quality);
        Assert.NotEmpty(measure.MissingData);
    }
}
