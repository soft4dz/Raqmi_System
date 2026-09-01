using RaqmiSystem.Application.Kpi;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Kpi;
using static RaqmiSystem.Tests.KpiTestData;

namespace RaqmiSystem.Tests;

/// <summary>
/// L'assemblage des reponses : filtrage par permissions, comparaisons, alertes et classements.
/// Le filtre de permissions est verifie ICI parce qu'il est applique cote serveur, avant que la
/// moindre valeur ne parte - passer par l'API plutot que par l'ecran ne doit rien changer.
/// </summary>
public sealed class KpiDashboardBuilderTests
{
    private readonly KpiEngine engine = new();
    private readonly KpiDashboardBuilder builder = new();

    private static readonly DateOnly Today = new(2026, 2, 1);
    private static readonly DateTimeOffset Now = new(2026, 2, 1, 8, 0, 0, TimeSpan.Zero);

    private static KpiQuery Query(string? unitCode = null) => new(Jan1, Jan31, unitCode);

    private static KpiFactSet Sample()
    {
        return Facts(
            units: [Unit(UnitA, "Hotel El Manar"), Unit(UnitB, "Hotel Es Salam")],
            rooms: [.. Rooms(10, UnitA), .. Rooms(10, UnitB)],
            stays:
            [
                Stay(0, Jan1, Jan1.AddDays(20), unit: UnitA),
                Stay(1, Jan1, Jan1.AddDays(20), unit: UnitA),
                Stay(0, Jan1, Jan1.AddDays(4), unit: UnitB)
            ],
            revenues:
            [
                Revenue(Jan1, accommodation: 800_000m, food: 200_000m, unit: UnitA),
                Revenue(Jan1, accommodation: 200_000m, food: 50_000m, unit: UnitB)
            ],
            payslips: [Payslip(300_000m, unit: UnitA), Payslip(100_000m, unit: UnitB)],
            budgetTargets: [BudgetTarget(1_200_000m, unit: UnitA), BudgetTarget(200_000m, unit: UnitB)]);
    }

    private KpiDashboardResponse BuildDashboard(
        KpiAccessContext access,
        IReadOnlyCollection<KpiThreshold>? thresholds = null,
        IReadOnlyCollection<KpiSnapshot>? snapshots = null,
        KpiQuery? query = null)
    {
        var facts = Sample();
        var effectiveQuery = query ?? Query();
        var period = effectiveQuery.ToPeriod();

        return builder.BuildDashboard(
            effectiveQuery,
            engine.Compute(period, facts, Today),
            engine.Compute(period.PreviousYear(), KpiFactSet.Empty, Today),
            facts.Units,
            thresholds ?? [],
            snapshots ?? [],
            access,
            Now);
    }

    private static KpiAccessContext AccessWith(params string[] permissions) => new(permissions);

    [Fact]
    public void A_profile_without_hr_rights_never_sees_payroll_even_as_a_ratio()
    {
        // La masse salariale rapportee au chiffre d'affaires reste une donnee de paie.
        var dashboard = BuildDashboard(AccessWith(
            PermissionCatalog.RevenueRead,
            PermissionCatalog.LodgingRead));

        var codes = dashboard.Sections.SelectMany(section => section.Measures)
            .Select(measure => measure.Code)
            .ToHashSet();

        Assert.DoesNotContain(KpiCodes.PayrollToRevenueRate, codes);
        Assert.DoesNotContain(KpiCodes.PayrollCost, codes);
        Assert.True(dashboard.HiddenByPermission > 0);
    }

    [Fact]
    public void An_indicator_crossing_two_modules_needs_both_permissions()
    {
        // L'ADR croise recettes et hebergement : avoir l'un sans l'autre ne suffit pas.
        var withOnlyRevenue = BuildDashboard(AccessWith(PermissionCatalog.RevenueRead));

        Assert.DoesNotContain(
            KpiCodes.Adr,
            withOnlyRevenue.Sections.SelectMany(section => section.Measures).Select(measure => measure.Code));

        var withBoth = BuildDashboard(AccessWith(
            PermissionCatalog.RevenueRead,
            PermissionCatalog.LodgingRead));

        Assert.Contains(
            KpiCodes.Adr,
            withBoth.Sections.SelectMany(section => section.Measures).Select(measure => measure.Code));
    }

    [Fact]
    public void The_dashboard_says_how_many_indicators_it_is_not_showing()
    {
        var restricted = BuildDashboard(AccessWith(PermissionCatalog.RevenueRead));
        var full = BuildDashboard(KpiAccessContext.Unrestricted);

        Assert.Equal(0, full.HiddenByPermission);
        Assert.Equal(KpiCatalog.All.Count - restricted.Sections.Sum(s => s.Measures.Count),
            restricted.HiddenByPermission);
    }

    [Fact]
    public void The_headline_follows_the_catalog_order_of_the_direction()
    {
        var dashboard = BuildDashboard(KpiAccessContext.Unrestricted);

        Assert.Equal(
            KpiCatalog.DirectionHeadlineCodes,
            dashboard.Headline.Select(measure => measure.Code).ToArray());
    }

    [Fact]
    public void The_group_dashboard_drills_down_into_its_units()
    {
        var dashboard = BuildDashboard(KpiAccessContext.Unrestricted);

        Assert.Equal(2, dashboard.Units.Count);
        Assert.Equal("Hotel El Manar", dashboard.Units.First().HotelUnitName);
        Assert.NotEmpty(dashboard.Units.First().Headline);
    }

    [Fact]
    public void A_dashboard_already_scoped_to_one_unit_offers_no_further_drill_down()
    {
        var dashboard = BuildDashboard(KpiAccessContext.Unrestricted, query: Query(UnitA));

        Assert.Empty(dashboard.Units);
        Assert.Equal(UnitA, dashboard.HotelUnitCode);
    }

    [Fact]
    public void The_revenue_budget_column_is_derived_from_the_variance_already_computed()
    {
        // Une seule addition des lignes budgetaires dans tout le moteur : la colonne budget et
        // l'ecart budgetaire ne peuvent donc pas se contredire.
        var dashboard = BuildDashboard(KpiAccessContext.Unrestricted);

        var revenue = dashboard.Headline.Single(measure => measure.Code == KpiCodes.RevenueTotal);

        Assert.Equal(1_400_000m, revenue.BudgetValue);
        Assert.Equal(-150_000m, revenue.BudgetVarianceAmount);
    }

    [Fact]
    public void Indicators_the_budget_module_does_not_budget_have_an_empty_budget_column()
    {
        // Le module Budget budgete des recettes et rien d'autre : afficher zero ailleurs
        // laisserait croire a un objectif de zero.
        var dashboard = BuildDashboard(KpiAccessContext.Unrestricted);

        var occupancy = dashboard.Headline.Single(measure => measure.Code == KpiCodes.OccupancyRate);

        Assert.Null(occupancy.BudgetValue);
        Assert.Null(occupancy.BudgetVarianceAmount);
    }

    [Fact]
    public void A_breached_threshold_raises_an_alert_carrying_its_owner()
    {
        var thresholds = new[]
        {
            new KpiThreshold(KpiCodes.OccupancyRate, null, 65m, 40m, null, "Direction exploitation")
        };

        var dashboard = BuildDashboard(KpiAccessContext.Unrestricted, thresholds);

        var alert = dashboard.Alerts.First(candidate => candidate.KpiCode == KpiCodes.OccupancyRate);

        Assert.Equal(KpiAlertSeverity.Critical, alert.Severity);
        Assert.Equal("Direction exploitation", alert.OwnerRole);
        Assert.Equal(40m, alert.BreachedThreshold);
        Assert.Contains("critique", alert.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Un indicateur favorable ne declenche rien - et le detail par unite continue d'alerter
    /// separement. C'est exactement le service rendu par la descente : le groupe va bien
    /// (7,1 % d'occupation, au-dessus de la borne favorable posee ici) pendant qu'un
    /// etablissement decroche (1,3 %), et la direction voit les deux.
    /// </summary>
    [Fact]
    public void A_favorable_indicator_raises_no_alert_on_its_own_scope()
    {
        var thresholds = new[]
        {
            new KpiThreshold(KpiCodes.OccupancyRate, null, 5m, 1m, null, null)
        };

        var dashboard = BuildDashboard(KpiAccessContext.Unrestricted, thresholds);

        Assert.DoesNotContain(
            dashboard.Alerts,
            alert => alert.KpiCode == KpiCodes.OccupancyRate && alert.HotelUnitCode is null);

        Assert.Contains(
            dashboard.Alerts,
            alert => alert.KpiCode == KpiCodes.OccupancyRate && alert.HotelUnitCode == UnitB);
    }

    [Fact]
    public void An_indicator_without_a_threshold_raises_no_alert_but_is_not_declared_healthy()
    {
        var dashboard = BuildDashboard(KpiAccessContext.Unrestricted);

        Assert.Empty(dashboard.Alerts);

        var occupancy = dashboard.Headline.Single(measure => measure.Code == KpiCodes.OccupancyRate);

        Assert.Equal(KpiHealth.Unknown, occupancy.Health);
    }

    [Fact]
    public void Alerts_are_ordered_by_severity_then_stably()
    {
        var thresholds = new[]
        {
            new KpiThreshold(KpiCodes.OccupancyRate, null, 65m, 40m, null, null),
            new KpiThreshold(KpiCodes.FoodCostRate, null, 10m, 90m, null, null)
        };

        var dashboard = BuildDashboard(KpiAccessContext.Unrestricted, thresholds);

        Assert.NotEmpty(dashboard.Alerts);
        Assert.Equal(
            dashboard.Alerts.OrderByDescending(alert => alert.Severity)
                .ThenBy(alert => alert.HotelUnitCode ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(alert => alert.KpiCode, StringComparer.Ordinal)
                .ToArray(),
            dashboard.Alerts.ToArray());
    }

    [Fact]
    public void A_closed_snapshot_that_no_longer_matches_is_reported_never_corrected()
    {
        var snapshot = new KpiSnapshot(
            KpiCodes.RevenueTotal, null, Jan1, Jan31, KpiPeriodGranularity.Month,
            value: 999_999m, numerator: 999_999m, denominator: null,
            KpiQuality.Valid, formulaVersion: 1, calculatedAt: Now);

        snapshot.Close("controleur", Now);

        var dashboard = BuildDashboard(KpiAccessContext.Unrestricted, snapshots: [snapshot]);

        var revenue = dashboard.Headline.Single(measure => measure.Code == KpiCodes.RevenueTotal);

        Assert.Equal(1_250_000m, revenue.Value);
        Assert.Equal(999_999m, revenue.SnapshotValue);
        Assert.Equal(KpiSnapshotStatus.Closed, revenue.SnapshotStatus);
        Assert.Contains(revenue.MissingData, reason => reason.Contains("figee a la cloture", StringComparison.Ordinal));
    }

    [Fact]
    public void The_comparison_puts_the_group_first_then_every_unit()
    {
        var facts = Sample();
        var period = Query().ToPeriod();

        var comparison = builder.BuildComparison(
            Query(),
            engine.Compute(period, facts, Today),
            engine.Compute(period.PreviousYear(), KpiFactSet.Empty, Today),
            facts.Units,
            [],
            [],
            KpiAccessContext.Unrestricted,
            Now);

        Assert.Equal(3, comparison.Rows.Count);
        Assert.Null(comparison.Rows.First().HotelUnitCode);
        Assert.Equal("Groupe", comparison.Rows.First().HotelUnitName);
        Assert.Equal(KpiCatalog.BenchmarkCodes, comparison.Codes);
    }

    [Fact]
    public void The_comparison_ranks_indicator_by_indicator_and_never_produces_a_composite_score()
    {
        var facts = Sample();
        var period = Query().ToPeriod();

        var comparison = builder.BuildComparison(
            Query(),
            engine.Compute(period, facts, Today),
            engine.Compute(period.PreviousYear(), KpiFactSet.Empty, Today),
            facts.Units,
            [],
            [],
            KpiAccessContext.Unrestricted,
            Now);

        Assert.All(comparison.Rankings, ranking =>
            Assert.NotNull(KpiCatalog.Find(ranking.KpiCode)));

        var bestOccupancy = comparison.Rankings.Single(ranking =>
            ranking.Kind == KpiRankingKind.BestPerformance && ranking.KpiCode == KpiCodes.OccupancyRate);

        // Hotel A : 40 nuitees occupees sur 310 ; hotel B : 4 sur 310.
        Assert.Equal(UnitA, bestOccupancy.HotelUnitCode);

        var worstOccupancy = comparison.Rankings.Single(ranking =>
            ranking.Kind == KpiRankingKind.WeakestPerformance && ranking.KpiCode == KpiCodes.OccupancyRate);

        Assert.Equal(UnitB, worstOccupancy.HotelUnitCode);
    }

    [Fact]
    public void A_lower_is_better_indicator_is_ranked_downwards()
    {
        var facts = Sample();
        var period = Query().ToPeriod();

        var comparison = builder.BuildComparison(
            Query(),
            engine.Compute(period, facts, Today),
            engine.Compute(period.PreviousYear(), KpiFactSet.Empty, Today),
            facts.Units,
            [],
            [],
            KpiAccessContext.Unrestricted,
            Now);

        // Masse salariale sur CA : A = 300 000 / 1 000 000 = 30 %, B = 100 000 / 250 000 = 40 %.
        // Le meilleur est le plus BAS.
        var best = comparison.Rankings.Single(ranking =>
            ranking.Kind == KpiRankingKind.BestPerformance
            && ranking.KpiCode == KpiCodes.PayrollToRevenueRate);

        Assert.Equal(UnitA, best.HotelUnitCode);
    }

    [Fact]
    public void The_basis_states_what_every_figure_counts()
    {
        var dashboard = BuildDashboard(KpiAccessContext.Unrestricted);

        Assert.Contains("Validee", dashboard.Basis.Revenue, StringComparison.Ordinal);
        Assert.Contains("moyenne", dashboard.Basis.Consolidation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tiret", dashboard.Basis.DataQuality, StringComparison.OrdinalIgnoreCase);
    }
}
