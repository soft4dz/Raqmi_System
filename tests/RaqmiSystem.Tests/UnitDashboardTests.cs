using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Tests;

public sealed class UnitDashboardTests
{
    private static readonly DateOnly BusinessDate = new(2026, 8, 28);

    [Fact]
    public void Build_counts_missing_draft_submitted_and_validated_units()
    {
        var calculator = new UnitDashboardCalculator();

        var missingUnit = new HotelUnit("EL-MANAR", "Hotel El Manar", HotelUnitType.Hotel, 1);
        var draftUnit = new HotelUnit("EL-MARSA", "Hotel El Marsa", HotelUnitType.Hotel, 2);
        var submittedUnit = new HotelUnit("EL-RIADH", "Hotel El Riadh", HotelUnitType.Hotel, 3);
        var validatedUnit = new HotelUnit("EL-YASMINE", "Hotel El Yasmine", HotelUnitType.Hotel, 4);

        var draftRevenue = new DailyRevenue(BusinessDate, draftUnit.Code, 100m, 20m, 10m, 5m);

        var submittedRevenue = new DailyRevenue(BusinessDate, submittedUnit.Code, 200m, 40m, 15m, 10m);
        submittedRevenue.Submit("controller", DateTimeOffset.UtcNow);

        var validatedRevenue = new DailyRevenue(BusinessDate, validatedUnit.Code, 300m, 60m, 20m, 15m);
        validatedRevenue.Submit("controller", DateTimeOffset.UtcNow);
        validatedRevenue.Validate("director", DateTimeOffset.UtcNow);

        var result = calculator.Build(
            BusinessDate,
            [missingUnit, draftUnit, submittedUnit, validatedUnit],
            [draftRevenue, submittedRevenue, validatedRevenue]);

        Assert.Equal(4, result.TotalUnits);
        Assert.Equal(3, result.UnitsWithEntry);
        Assert.Equal(1, result.UnitsMissing);
        Assert.Equal(1, result.UnitsPendingValidation);
        Assert.Equal(draftRevenue.Total + submittedRevenue.Total + validatedRevenue.Total, result.GrandTotal);
        Assert.Equal(4, result.Units.Count);

        var missingRow = result.Units.Single(row => row.HotelUnitCode == missingUnit.Code);
        Assert.False(missingRow.HasEntry);
        Assert.Null(missingRow.Status);
        Assert.Null(missingRow.Total);
        Assert.Null(missingRow.SubmittedAt);
        Assert.Null(missingRow.ValidatedAt);

        var submittedRow = result.Units.Single(row => row.HotelUnitCode == submittedUnit.Code);
        Assert.True(submittedRow.HasEntry);
        Assert.Equal(DailyRevenueStatus.Submitted, submittedRow.Status);
        Assert.NotNull(submittedRow.SubmittedAt);
        Assert.Null(submittedRow.ValidatedAt);

        var validatedRow = result.Units.Single(row => row.HotelUnitCode == validatedUnit.Code);
        Assert.Equal(DailyRevenueStatus.Validated, validatedRow.Status);
        Assert.NotNull(validatedRow.ValidatedAt);
    }

    [Fact]
    public void Build_reports_all_units_missing_when_no_entries_exist()
    {
        var calculator = new UnitDashboardCalculator();

        var unitA = new HotelUnit("EL-MANAR", "Hotel El Manar", HotelUnitType.Hotel, 1);
        var unitB = new HotelUnit("EL-MARSA", "Hotel El Marsa", HotelUnitType.Hotel, 2);

        var result = calculator.Build(BusinessDate, [unitA, unitB], []);

        Assert.Equal(2, result.TotalUnits);
        Assert.Equal(0, result.UnitsWithEntry);
        Assert.Equal(2, result.UnitsMissing);
        Assert.Equal(0, result.UnitsPendingValidation);
        Assert.Equal(0m, result.GrandTotal);
        Assert.All(result.Units, row => Assert.False(row.HasEntry));
    }

    [Fact]
    public void Build_orders_rows_by_display_order_then_name()
    {
        var calculator = new UnitDashboardCalculator();

        var second = new HotelUnit("EL-MARSA", "Hotel El Marsa", HotelUnitType.Hotel, 2);
        var first = new HotelUnit("EL-MANAR", "Hotel El Manar", HotelUnitType.Hotel, 1);

        var result = calculator.Build(BusinessDate, [second, first], []);

        Assert.Equal(["EL-MANAR", "EL-MARSA"], result.Units.Select(row => row.HotelUnitCode));
    }
}
