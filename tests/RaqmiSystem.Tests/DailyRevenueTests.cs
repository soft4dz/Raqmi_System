using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Tests;

public sealed class DailyRevenueTests
{
    [Fact]
    public void Constructor_calculates_total_and_normalizes_unit_code()
    {
        var revenue = new DailyRevenue(
            new DateOnly(2026, 1, 31),
            " el-manar ",
            1_200_000m,
            340_000m,
            110_000m,
            80_000m,
            " Journee normale ");

        Assert.Equal("EL-MANAR", revenue.HotelUnitCode);
        Assert.Equal(1_730_000m, revenue.Total);
        Assert.Equal("Journee normale", revenue.Notes);
        Assert.Equal(DailyRevenueStatus.Draft, revenue.Status);
        Assert.True(revenue.CanEdit);
    }

    [Fact]
    public void Workflow_submits_then_validates_entry()
    {
        var revenue = CreateDraft();

        revenue.Submit("controller", DateTimeOffset.UtcNow);
        revenue.Validate("director", DateTimeOffset.UtcNow);

        Assert.Equal(DailyRevenueStatus.Validated, revenue.Status);
        Assert.False(revenue.CanEdit);
        Assert.Equal("director", revenue.ValidatedBy);
    }

    [Fact]
    public void Submitted_entry_cannot_be_edited()
    {
        var revenue = CreateDraft();

        revenue.Submit("controller", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            revenue.UpdateAmounts(100m, 0m, 0m, 0m, null));
    }

    [Fact]
    public void Rejected_entry_can_be_corrected_and_returns_to_draft()
    {
        var revenue = CreateDraft();

        revenue.Submit("controller", DateTimeOffset.UtcNow);
        revenue.Reject("Missing control sheet.", "director", DateTimeOffset.UtcNow);
        revenue.UpdateAmounts(100m, 20m, 10m, 5m, "Corrected");

        Assert.Equal(DailyRevenueStatus.Draft, revenue.Status);
        Assert.Equal(135m, revenue.Total);
        Assert.Equal("Corrected", revenue.Notes);
        Assert.Null(revenue.RejectionReason);
        Assert.True(revenue.CanEdit);
    }

    [Fact]
    public void Constructor_rejects_negative_amounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DailyRevenue(new DateOnly(2026, 1, 31), "EL-MANAR", -1m, 0m, 0m, 0m));
    }

    private static DailyRevenue CreateDraft()
    {
        return new DailyRevenue(new DateOnly(2026, 1, 31), "EL-MANAR", 100m, 20m, 10m, 5m);
    }
}
