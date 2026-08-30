using RaqmiSystem.Application.Budgeting;
using RaqmiSystem.Domain.Budgeting;

namespace RaqmiSystem.Tests;

/// <summary>
/// Pure domain and arithmetic coverage for the budgeting module: the invariants carried by
/// <see cref="BudgetPlan"/> itself, and the budget-versus-actual computation performed by
/// <see cref="BudgetVarianceCalculator"/> (which needs no database, by design).
/// </summary>
public sealed class BudgetingTests
{
    [Fact]
    public void Budget_plan_starts_as_an_editable_draft_and_normalizes_its_unit_code()
    {
        var plan = new BudgetPlan(2026, " budhtl ", "  Budget 2026 - Hotel El Manar  ");

        Assert.Equal(2026, plan.Year);
        Assert.Equal("BUDHTL", plan.HotelUnitCode);
        Assert.Equal("Budget 2026 - Hotel El Manar", plan.Label);
        Assert.Equal(BudgetStatus.Draft, plan.Status);
        Assert.True(plan.CanEdit);
        Assert.Empty(plan.Lines);
        Assert.Equal(0m, plan.TotalTarget);
    }

    [Fact]
    public void Approved_budget_plan_can_no_longer_be_modified()
    {
        var plan = new BudgetPlan(2026, "BUDHTL", "Budget 2026");
        plan.SetLine(1, BudgetCategory.Accommodation, 100_000.00m);

        plan.Approve("direction.user", DateTimeOffset.UtcNow);

        Assert.Equal(BudgetStatus.Approved, plan.Status);
        Assert.False(plan.CanEdit);
        Assert.Equal("direction.user", plan.ApprovedBy);
        Assert.NotNull(plan.ApprovedAt);

        // Every mutation path is closed, not just the obvious one: an approved budget is the
        // reference every later variance is measured against.
        Assert.Throws<InvalidOperationException>(() => plan.Rename("Budget 2026 revise"));
        Assert.Throws<InvalidOperationException>(() => plan.SetLine(2, BudgetCategory.Food, 10_000.00m));
        Assert.Throws<InvalidOperationException>(() => plan.SetLine(1, BudgetCategory.Accommodation, 1m));
        Assert.Throws<InvalidOperationException>(() => plan.RemoveLine(plan.Lines.Single().Id));
        Assert.Throws<InvalidOperationException>(() => plan.ReplaceLines(new[]
        {
            new BudgetLine(1, BudgetCategory.Accommodation, 1m)
        }));

        // ... and the plan really is untouched.
        Assert.Equal(100_000.00m, plan.TotalTarget);
        Assert.Equal("Budget 2026", plan.Label);
    }

    [Fact]
    public void Approving_an_empty_budget_plan_is_refused()
    {
        var plan = new BudgetPlan(2026, "BUDHTL", "Budget 2026");

        Assert.Throws<InvalidOperationException>(() => plan.Approve("direction.user", DateTimeOffset.UtcNow));
        Assert.Equal(BudgetStatus.Draft, plan.Status);
    }

    [Fact]
    public void Budget_plan_is_only_closable_once_approved()
    {
        var plan = new BudgetPlan(2026, "BUDHTL", "Budget 2026");
        plan.SetLine(1, BudgetCategory.Accommodation, 100_000.00m);

        Assert.Throws<InvalidOperationException>(() => plan.Close("direction.user", DateTimeOffset.UtcNow));

        plan.Approve("direction.user", DateTimeOffset.UtcNow);
        plan.Close("direction.user", DateTimeOffset.UtcNow);

        Assert.Equal(BudgetStatus.Closed, plan.Status);
        Assert.NotNull(plan.ClosedAt);
        Assert.Throws<InvalidOperationException>(() => plan.SetLine(1, BudgetCategory.Food, 1m));
    }

    [Fact]
    public void Budget_line_month_is_bounded_to_the_twelve_calendar_months()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BudgetLine(0, BudgetCategory.Food, 1_000m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BudgetLine(13, BudgetCategory.Food, 1_000m));

        var plan = new BudgetPlan(2026, "BUDHTL", "Budget 2026");

        Assert.Throws<ArgumentOutOfRangeException>(() => plan.SetLine(-1, BudgetCategory.Food, 1_000m));
        Assert.Throws<ArgumentOutOfRangeException>(() => plan.SetLine(12, BudgetCategory.Food, -1m));

        Assert.Equal(1, plan.SetLine(1, BudgetCategory.Food, 1_000m).Month);
        Assert.Equal(12, plan.SetLine(12, BudgetCategory.Food, 1_000m).Month);
    }

    [Fact]
    public void A_month_and_category_pair_carries_exactly_one_target()
    {
        var plan = new BudgetPlan(2026, "BUDHTL", "Budget 2026");

        var first = plan.SetLine(3, BudgetCategory.Beverage, 10_000.00m);
        var second = plan.SetLine(3, BudgetCategory.Beverage, 12_500.00m);

        // Re-setting the same cell adjusts the existing target rather than adding a second,
        // contradictory one - and keeps the line's identity stable.
        Assert.Same(first, second);
        Assert.Single(plan.Lines);
        Assert.Equal(12_500.00m, plan.Lines.Single().AmountTarget);
        Assert.Equal(first.Id, plan.Lines.Single().Id);

        // The same cell in another month, and another category in the same month, are distinct.
        plan.SetLine(4, BudgetCategory.Beverage, 1m);
        plan.SetLine(3, BudgetCategory.Food, 1m);
        Assert.Equal(3, plan.Lines.Count);
    }

    [Fact]
    public void Replacing_lines_refuses_a_payload_carrying_the_same_cell_twice()
    {
        var plan = new BudgetPlan(2026, "BUDHTL", "Budget 2026");

        Assert.Throws<ArgumentException>(() => plan.ReplaceLines(new[]
        {
            new BudgetLine(5, BudgetCategory.Accommodation, 100_000.00m),
            new BudgetLine(5, BudgetCategory.Accommodation, 250_000.00m)
        }));

        Assert.Empty(plan.Lines);
    }

    [Fact]
    public void Replacing_lines_keeps_surviving_cells_and_drops_the_others()
    {
        var plan = new BudgetPlan(2026, "BUDHTL", "Budget 2026");
        var kept = plan.SetLine(1, BudgetCategory.Accommodation, 100_000.00m);
        plan.SetLine(1, BudgetCategory.Food, 50_000.00m);

        plan.ReplaceLines(new[]
        {
            new BudgetLine(1, BudgetCategory.Accommodation, 120_000.00m),
            new BudgetLine(2, BudgetCategory.Accommodation, 200_000.00m)
        });

        Assert.Equal(2, plan.Lines.Count);
        Assert.DoesNotContain(plan.Lines, line => line.Category == BudgetCategory.Food);

        // The surviving cell is updated in place, Id included: clearing and re-inserting would
        // have EF delete and re-insert rows guarded by ux_budget_lines_plan_month_category.
        Assert.Equal(kept.Id, plan.Lines.Single(line => line.Month == 1).Id);
        Assert.Equal(120_000.00m, plan.Lines.Single(line => line.Month == 1).AmountTarget);
        Assert.Equal(320_000.00m, plan.TotalTarget);
    }

    [Fact]
    public void Variance_is_exact_month_by_month_and_category_by_category()
    {
        var report = new BudgetVarianceCalculator().Calculate(
            2026,
            "BUDHTL",
            month: null,
            budgetPlanId: Guid.NewGuid(),
            planStatus: BudgetStatus.Approved,
            targets: new[]
            {
                new BudgetTargetLine(1, BudgetCategory.Accommodation, 100_000.00m),
                new BudgetTargetLine(1, BudgetCategory.Food, 50_000.00m),
                new BudgetTargetLine(2, BudgetCategory.Accommodation, 200_000.00m)
            },
            actuals: new[]
            {
                new BudgetActualRevenue(new DateOnly(2026, 1, 5), 60_000.00m, 55_000.00m, 0m, 0m),
                new BudgetActualRevenue(new DateOnly(2026, 1, 20), 30_000.00m, 0m, 1_000.00m, 0m)
            });

        // A full-year report always carries its twelve months and each month its four categories,
        // whether or not anything was budgeted or produced.
        Assert.Equal(12, report.Months.Count);
        Assert.All(report.Months, month => Assert.Equal(4, month.Categories.Count));

        var january = report.Months.Single(month => month.Month == 1);

        var accommodation = january.Categories.Single(row => row.Category == BudgetCategory.Accommodation);
        Assert.Equal(100_000.00m, accommodation.BudgetAmount);
        Assert.Equal(90_000.00m, accommodation.ActualAmount);
        Assert.Equal(-10_000.00m, accommodation.VarianceAmount);
        Assert.Equal(-10.00m, accommodation.VariancePercentage);

        var food = january.Categories.Single(row => row.Category == BudgetCategory.Food);
        Assert.Equal(50_000.00m, food.BudgetAmount);
        Assert.Equal(55_000.00m, food.ActualAmount);
        Assert.Equal(5_000.00m, food.VarianceAmount);
        Assert.Equal(10.00m, food.VariancePercentage);

        Assert.Equal(150_000.00m, january.BudgetAmount);
        Assert.Equal(146_000.00m, january.ActualAmount);
        Assert.Equal(-4_000.00m, january.VarianceAmount);
        Assert.Equal(-2.67m, january.VariancePercentage);

        var february = report.Months.Single(month => month.Month == 2);
        Assert.Equal(200_000.00m, february.BudgetAmount);
        Assert.Equal(0m, february.ActualAmount);
        Assert.Equal(-200_000.00m, february.VarianceAmount);
        Assert.Equal(-100.00m, february.VariancePercentage);

        Assert.Equal(350_000.00m, report.BudgetAmount);
        Assert.Equal(146_000.00m, report.ActualAmount);
        Assert.Equal(-204_000.00m, report.VarianceAmount);
        Assert.Equal(-58.29m, report.VariancePercentage);
    }

    [Fact]
    public void Variance_percentage_is_undefined_rather_than_zero_when_nothing_was_budgeted()
    {
        var report = new BudgetVarianceCalculator().Calculate(
            2026,
            "BUDHTL",
            month: 1,
            budgetPlanId: Guid.NewGuid(),
            planStatus: BudgetStatus.Draft,
            targets: Array.Empty<BudgetTargetLine>(),
            actuals: new[]
            {
                new BudgetActualRevenue(new DateOnly(2026, 1, 9), 0m, 0m, 50_000.00m, 0m)
            });

        var january = Assert.Single(report.Months);
        var beverage = january.Categories.Single(row => row.Category == BudgetCategory.Beverage);

        // The gap in VALUE is always there; only the relative reading is missing, because there is
        // no denominator to be relative to.
        Assert.Equal(0m, beverage.BudgetAmount);
        Assert.Equal(50_000.00m, beverage.ActualAmount);
        Assert.Equal(50_000.00m, beverage.VarianceAmount);
        Assert.Null(beverage.VariancePercentage);

        // Zero against zero is undefined too, not "on target".
        var other = january.Categories.Single(row => row.Category == BudgetCategory.Other);
        Assert.Equal(0m, other.VarianceAmount);
        Assert.Null(other.VariancePercentage);

        Assert.Null(january.VariancePercentage);
        Assert.Null(report.VariancePercentage);
    }

    [Fact]
    public void Variance_narrowed_to_one_month_reports_that_month_alone()
    {
        var report = new BudgetVarianceCalculator().Calculate(
            2026,
            "BUDHTL",
            month: 2,
            budgetPlanId: Guid.NewGuid(),
            planStatus: BudgetStatus.Approved,
            targets: new[]
            {
                new BudgetTargetLine(1, BudgetCategory.Accommodation, 100_000.00m),
                new BudgetTargetLine(2, BudgetCategory.Accommodation, 200_000.00m)
            },
            actuals: new[]
            {
                new BudgetActualRevenue(new DateOnly(2026, 2, 14), 180_000.00m, 0m, 0m, 0m)
            });

        var february = Assert.Single(report.Months);
        Assert.Equal(2, february.Month);
        Assert.NotNull(report.Month);
        Assert.Equal(2, report.Month!.Value);

        // January's target is not folded into the totals of a February-only report.
        Assert.Equal(200_000.00m, report.BudgetAmount);
        Assert.Equal(180_000.00m, report.ActualAmount);
        Assert.Equal(-20_000.00m, report.VarianceAmount);
        Assert.Equal(-10.00m, report.VariancePercentage);
    }
}
