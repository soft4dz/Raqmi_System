using RaqmiSystem.Domain.Reporting;

namespace RaqmiSystem.Tests;

/// <summary>
/// Pure domain coverage of the reporting module: the code-defined catalog (its codes are wire
/// identifiers referenced by the desktop client and the execution journal, so they are pinned
/// here) and the invariants of the <see cref="ReportExecution"/> journal entity.
/// </summary>
public sealed class ReportingCatalogTests
{
    [Fact]
    public void Catalog_ships_exactly_the_five_delivered_reports()
    {
        var codes = ReportCatalog.All.Select(definition => definition.Code).ToArray();

        Assert.Equal(
            new[]
            {
                ReportCatalog.RevenueByUnit,
                ReportCatalog.ReceiptsByMethod,
                ReportCatalog.AgedBalance,
                ReportCatalog.InvoicedVat,
                ReportCatalog.OccupancyByUnit
            },
            codes);

        // The codes are wire identifiers: unique, and stable in lower case.
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(codes, code => Assert.Equal(code.ToLowerInvariant(), code));
    }

    [Fact]
    public void Every_report_has_a_title_a_description_and_uniquely_keyed_parameters()
    {
        Assert.All(ReportCatalog.All, definition =>
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Title));
            Assert.False(string.IsNullOrWhiteSpace(definition.Description));

            var keys = definition.Parameters.Select(parameter => parameter.Key).ToArray();
            Assert.Equal(keys.Length, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        });
    }

    [Fact]
    public void Occupancy_requires_its_unit_while_revenue_only_suggests_one()
    {
        var occupancy = ReportCatalog.Find(ReportCatalog.OccupancyByUnit);
        Assert.NotNull(occupancy);

        var occupancyUnit = occupancy!.Parameters.Single(parameter => parameter.Key == ReportCatalog.UnitCodeParameter);
        Assert.True(occupancyUnit.Required);
        Assert.Equal(ReportParameterType.HotelUnit, occupancyUnit.Type);

        var revenue = ReportCatalog.Find(ReportCatalog.RevenueByUnit);
        Assert.NotNull(revenue);

        var revenueUnit = revenue!.Parameters.Single(parameter => parameter.Key == ReportCatalog.UnitCodeParameter);
        Assert.False(revenueUnit.Required);
    }

    [Fact]
    public void Aged_balance_takes_a_single_as_of_date()
    {
        var agedBalance = ReportCatalog.Find(ReportCatalog.AgedBalance);

        Assert.NotNull(agedBalance);
        var parameter = Assert.Single(agedBalance!.Parameters);
        Assert.Equal(ReportCatalog.AsOfDateParameter, parameter.Key);
        Assert.Equal(ReportParameterType.Date, parameter.Type);
        Assert.True(parameter.Required);
    }

    [Fact]
    public void Find_matches_case_insensitively_and_ignores_surrounding_spaces()
    {
        Assert.NotNull(ReportCatalog.Find("  TVA-Facturee  "));
        Assert.Null(ReportCatalog.Find("rapport-fantome"));
        Assert.Null(ReportCatalog.Find(null));
        Assert.Null(ReportCatalog.Find("   "));
    }

    [Fact]
    public void Execution_normalizes_its_code_and_keeps_the_run_figures()
    {
        var execution = new ReportExecution("  TVA-Facturee ", "{\"from\":\"2026-07-01\"}", 12, 45);

        Assert.Equal("tva-facturee", execution.ReportCode);
        Assert.Equal("{\"from\":\"2026-07-01\"}", execution.ParametersJson);
        Assert.Equal(12, execution.RowCount);
        Assert.Equal(45, execution.DurationMilliseconds);
    }

    [Fact]
    public void Execution_refuses_a_missing_code_or_parameters_payload()
    {
        Assert.Throws<ArgumentException>(() => new ReportExecution("   ", "{}", 0, 0));
        Assert.Throws<ArgumentException>(() => new ReportExecution("tva-facturee", "  ", 0, 0));
    }

    [Fact]
    public void Execution_refuses_negative_row_counts_and_durations()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReportExecution("tva-facturee", "{}", -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReportExecution("tva-facturee", "{}", 0, -1));
    }
}
