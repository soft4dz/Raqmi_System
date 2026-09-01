using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Tests;

/// <summary>
/// La coherence du catalogue. Il est la source unique de verite de toute la bibliotheque : une
/// fiche incomplete ou incoherente se propagerait a l'API, aux ecrans et a l'historique, aussi
/// est-elle verifiee ici plutot que decouverte en production.
/// </summary>
public sealed class KpiCatalogTests
{
    [Fact]
    public void Codes_are_unique()
    {
        var codes = KpiCatalog.All.Select(definition => definition.Code).ToArray();

        Assert.Equal(codes.Length, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Codes_follow_the_upper_snake_case_convention()
    {
        Assert.All(KpiCatalog.All, definition =>
        {
            Assert.Equal(definition.Code, definition.Code.ToUpperInvariant());
            Assert.DoesNotContain(" ", definition.Code, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Every_definition_carries_the_fields_a_reader_needs()
    {
        Assert.All(KpiCatalog.All, definition =>
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Name), definition.Code);
            Assert.False(string.IsNullOrWhiteSpace(definition.ShortName), definition.Code);
            Assert.False(string.IsNullOrWhiteSpace(definition.Description), definition.Code);
            Assert.False(string.IsNullOrWhiteSpace(definition.Formula), definition.Code);
            Assert.False(string.IsNullOrWhiteSpace(definition.SourceDetail), definition.Code);
            Assert.True(definition.FormulaVersion >= 1, definition.Code);
        });
    }

    [Fact]
    public void Every_definition_requires_at_least_one_known_permission()
    {
        var known = PermissionCatalog.All.Select(permission => permission.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.All(KpiCatalog.All, definition =>
        {
            Assert.NotEmpty(definition.RequiredPermissions);
            Assert.All(definition.RequiredPermissions, permission =>
                Assert.True(known.Contains(permission), $"{definition.Code} exige la cle inconnue {permission}."));
        });
    }

    [Fact]
    public void An_indicator_awaiting_its_source_names_what_is_missing()
    {
        // C'est tout l'interet de les declarer : dire ce qu'il faudrait construire, precisement.
        Assert.All(
            KpiCatalog.All.Where(definition => definition.Availability == KpiAvailability.AwaitingSource),
            definition =>
            {
                Assert.False(string.IsNullOrWhiteSpace(definition.MissingSource), definition.Code);
                Assert.Equal(KpiSourceModule.None, definition.SourceModule);
            });
    }

    [Fact]
    public void An_implemented_indicator_names_the_module_that_owns_its_data()
    {
        Assert.All(
            KpiCatalog.All.Where(definition => definition.Availability == KpiAvailability.Implemented),
            definition =>
            {
                Assert.NotEqual(KpiSourceModule.None, definition.SourceModule);
                Assert.Null(definition.MissingSource);
            });
    }

    [Fact]
    public void A_neutral_indicator_never_declares_a_polarity_dependent_reading()
    {
        // Une capacite ou un effectif ne se juge pas : le moteur ne rend donc jamais de verdict
        // dessus, et c'est ce que la polarite neutre signifie.
        Assert.All(
            KpiCatalog.All.Where(definition => definition.Polarity == KpiPolarity.Neutral),
            definition => Assert.Equal(
                KpiHealth.Unknown,
                KpiMath.Classify(1m, 10m, 0m, definition.Polarity)));
    }

    [Fact]
    public void Headline_and_benchmark_codes_all_exist_in_the_catalog()
    {
        Assert.All(KpiCatalog.DirectionHeadlineCodes, code => Assert.NotNull(KpiCatalog.Find(code)));
        Assert.All(KpiCatalog.BenchmarkCodes, code => Assert.NotNull(KpiCatalog.Find(code)));
    }

    /// <summary>
    /// Les colonnes du comparatif sont soit des TAUX ET RATIOS, comparables entre hotels de
    /// tailles differentes, soit deux grandeurs de CONTEXTE explicitement admises - le chiffre
    /// d'affaires et l'excedent brut d'exploitation, que la direction veut voir en regard des
    /// taux. Une troisieme grandeur additive glissee dans ce tableau reviendrait a classer les
    /// hotels par nombre de chambres, ce que personne n'a besoin d'un tableau de bord pour
    /// savoir : le test la refuse.
    /// </summary>
    [Fact]
    public void Benchmark_columns_are_ratios_or_one_of_the_two_admitted_context_volumes()
    {
        string[] contextVolumes = [KpiCodes.RevenueTotal, KpiCodes.Ebitda];

        Assert.All(
            KpiCatalog.BenchmarkCodes.Where(code => !contextVolumes.Contains(code)),
            code =>
            {
                var definition = KpiCatalog.Require(code);

                Assert.Equal(KpiAggregation.RatioOfSums, definition.Aggregation);
                Assert.True(
                    definition.Unit is KpiUnit.Percentage or KpiUnit.Currency or KpiUnit.Ratio,
                    $"{code} n'est pas une colonne de comparatif exploitable.");
            });
    }

    /// <summary>
    /// Consequence a connaitre du comparatif : la colonne EBE reste VIDE unite par unite, et
    /// n'est renseignee que sur la ligne du groupe. La comptabilite de Raqmi System n'est pas
    /// analytique - une ecriture ne porte pas d'unite hoteliere - et repartir le resultat au
    /// prorata d'une cle quelconque fabriquerait un chiffre convaincant et sans fondement. Le
    /// jour ou une comptabilite analytique existe, c'est cette declaration qui change.
    /// </summary>
    [Fact]
    public void The_operating_result_column_of_the_benchmark_is_group_only()
    {
        Assert.Equal(KpiScopeLevel.GroupOnly, KpiCatalog.Require(KpiCodes.Ebitda).ScopeLevel);
        Assert.Equal(KpiScopeLevel.UnitAndGroup, KpiCatalog.Require(KpiCodes.RevenueTotal).ScopeLevel);
    }

    [Fact]
    public void A_group_only_indicator_reads_data_that_carries_no_hotel_unit()
    {
        // Les seuls modules concernes sont la comptabilite (pas d'analytique) et les ordres de
        // paiement (pas d'unite). Ajouter un autre module ici serait un choix, pas un constat.
        Assert.All(
            KpiCatalog.All.Where(definition => definition.ScopeLevel == KpiScopeLevel.GroupOnly),
            definition => Assert.True(
                definition.SourceModule is KpiSourceModule.Accounting or KpiSourceModule.Treasury
                    or KpiSourceModule.None,
                $"{definition.Code} est declare groupe seulement sans que sa source le justifie."));
    }

    [Fact]
    public void Require_rejects_an_unknown_code()
    {
        Assert.Throws<ArgumentException>(() => KpiCatalog.Require("PAS_UN_INDICATEUR"));
        Assert.Null(KpiCatalog.Find("PAS_UN_INDICATEUR"));
        Assert.Null(KpiCatalog.Find(null));
    }

    [Fact]
    public void The_library_covers_every_requested_family()
    {
        Assert.All(
            Enum.GetValues<KpiCategory>(),
            category => Assert.NotEmpty(KpiCatalog.InCategory(category)));
    }
}
