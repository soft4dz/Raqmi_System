using RaqmiSystem.Domain.Tariffs;

namespace RaqmiSystem.Tests;

/// <summary>
/// Domain rules of the tariffs module: normalization, schedule validity, amount and discount
/// rules, and the inclusive-bounds overlap semantics that everything else builds on.
/// </summary>
public sealed class TariffsTests
{
    [Fact]
    public void Rate_plan_normalizes_codes_and_starts_active()
    {
        var plan = new RatePlan(" std-2026 ", " Tarif standard 2026 ", " htl-alger ", isDefault: true);

        Assert.Equal("STD-2026", plan.Code);
        Assert.Equal("Tarif standard 2026", plan.Label);
        Assert.Equal("HTL-ALGER", plan.HotelUnitCode);
        Assert.True(plan.IsDefault);
        Assert.True(plan.IsActive);
    }

    [Fact]
    public void Rate_plan_rejects_blank_code_label_or_unit()
    {
        Assert.Throws<ArgumentException>(() => new RatePlan(" ", "Label", "HTL"));
        Assert.Throws<ArgumentException>(() => new RatePlan("PLAN", " ", "HTL"));
        Assert.Throws<ArgumentException>(() => new RatePlan("PLAN", "Label", " "));
    }

    [Fact]
    public void Rate_period_normalizes_room_type_and_validates_schedule()
    {
        var planId = Guid.NewGuid();

        var period = new RatePeriod(planId, " dbl ", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), 12_500.50m);

        Assert.Equal("DBL", period.RoomTypeCode);
        Assert.Equal(12_500.50m, period.NightlyAmount);
        Assert.True(period.Covers(new DateOnly(2026, 6, 1)));
        Assert.True(period.Covers(new DateOnly(2026, 6, 30)));
        Assert.False(period.Covers(new DateOnly(2026, 7, 1)));

        // From after To.
        Assert.Throws<ArgumentException>(() =>
            new RatePeriod(planId, "DBL", new DateOnly(2026, 6, 2), new DateOnly(2026, 6, 1), 100m));

        // Non-positive and over-precise amounts.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RatePeriod(planId, "DBL", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RatePeriod(planId, "DBL", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), -10m));
        Assert.Throws<ArgumentException>(() =>
            new RatePeriod(planId, "DBL", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), 100.555m));

        // An empty plan id would detach the period from any plan.
        Assert.Throws<ArgumentException>(() =>
            new RatePeriod(Guid.Empty, "DBL", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), 100m));
    }

    [Fact]
    public void Rate_period_overlap_bounds_are_inclusive()
    {
        var planId = Guid.NewGuid();

        var june1To10 = new RatePeriod(planId, "DBL", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 10), 100m);

        // A period ending on the 10th and one starting on the 10th DO overlap: the night of the
        // 10th would carry two prices, and a night has exactly one price.
        var june10To20 = new RatePeriod(planId, "DBL", new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 20), 120m);
        Assert.True(june1To10.Overlaps(june10To20));
        Assert.True(june10To20.Overlaps(june1To10));

        // Starting the day after the other ends is the first non-overlapping layout.
        var june11To20 = new RatePeriod(planId, "DBL", new DateOnly(2026, 6, 11), new DateOnly(2026, 6, 20), 120m);
        Assert.False(june1To10.Overlaps(june11To20));
        Assert.False(june11To20.Overlaps(june1To10));

        // Same nights, different room type: no conflict.
        var suiteSameDates = new RatePeriod(planId, "SUITE", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 10), 300m);
        Assert.False(june1To10.Overlaps(suiteSameDates));

        // Full containment overlaps too.
        var june5To6 = new RatePeriod(planId, "DBL", new DateOnly(2026, 6, 5), new DateOnly(2026, 6, 6), 150m);
        Assert.True(june1To10.Overlaps(june5To6));
    }

    [Fact]
    public void Customer_convention_validates_discount_and_schedule()
    {
        var convention = new CustomerConvention(
            " sonatrach ",
            " conv-plan ",
            12.5m,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        Assert.Equal("SONATRACH", convention.CustomerCode);
        Assert.Equal("CONV-PLAN", convention.RatePlanCode);
        Assert.Equal(12.5m, convention.DiscountPercent);
        Assert.True(convention.IsActive);
        Assert.True(convention.Covers(new DateOnly(2026, 12, 31)));

        // The discount is optional, and 0 and 100 are both legal bounds.
        Assert.Null(new CustomerConvention("C", "P", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2)).DiscountPercent);
        Assert.Equal(0m, new CustomerConvention("C", "P", 0m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2)).DiscountPercent);
        Assert.Equal(100m, new CustomerConvention("C", "P", 100m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2)).DiscountPercent);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CustomerConvention("C", "P", -0.01m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CustomerConvention("C", "P", 100.01m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2)));
        Assert.Throws<ArgumentException>(() =>
            new CustomerConvention("C", "P", 10.555m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2)));
        Assert.Throws<ArgumentException>(() =>
            new CustomerConvention("C", "P", null, new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Customer_convention_validity_overlap_bounds_are_inclusive()
    {
        var firstHalf = new CustomerConvention("CLI", "P1", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

        // Starting the very day the other one ends overlaps: that day would have two conventions.
        var startsOnLastDay = new CustomerConvention("CLI", "P2", null, new DateOnly(2026, 6, 30), new DateOnly(2026, 12, 31));
        Assert.True(firstHalf.OverlapsValidity(startsOnLastDay));

        var secondHalf = new CustomerConvention("CLI", "P2", null, new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31));
        Assert.False(firstHalf.OverlapsValidity(secondHalf));

        // Another customer's convention never conflicts.
        var otherCustomer = new CustomerConvention("OTHER", "P1", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        Assert.False(firstHalf.OverlapsValidity(otherCustomer));
    }
}
