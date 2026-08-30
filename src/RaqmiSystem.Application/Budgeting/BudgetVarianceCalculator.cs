using RaqmiSystem.Domain.Budgeting;

namespace RaqmiSystem.Application.Budgeting;

/// <summary>
/// Confronts a budget with what was actually produced. Pure in-memory combination (no database
/// access) so the arithmetic can be unit tested independently of the EF-backed service, following
/// the same shape as <c>RevenueSummaryService</c> and <c>UnitDashboardCalculator</c>: the caller
/// fetches the targets and the actual revenue rows, this class only computes.
///
/// WHAT COUNTS AS ACTUAL - the rule the whole module hangs on: only daily revenue at the
/// <c>Validated</c> status is actual. A Draft entry is a keystroke that has not been controlled
/// yet, a Submitted one is awaiting that control, and a Rejected one has been refused; none of
/// the three is money the establishment can claim to have made, and letting any of them into the
/// comparison would let an un-reviewed - or an explicitly refused - figure close a budget gap on
/// paper. This mirrors the treasury summary, which totals only Confirmed receipts for exactly the
/// same reason. The filtering itself happens in the query that builds
/// <see cref="BudgetActualRevenue"/>; this class documents and depends on it.
///
/// SHAPE OF THE REPORT: every month of the requested period is emitted, and every one of the four
/// revenue categories inside it, whether or not a target or an actual exists for that cell. A
/// month absent from the plan is a target of zero, not a missing row - the reader must be able to
/// see that nothing was budgeted for it.
/// </summary>
public sealed class BudgetVarianceCalculator
{
    private static readonly BudgetCategory[] Categories =
    {
        BudgetCategory.Accommodation,
        BudgetCategory.Food,
        BudgetCategory.Beverage,
        BudgetCategory.Other
    };

    public BudgetVarianceResponse Calculate(
        int year,
        string hotelUnitCode,
        int? month,
        Guid budgetPlanId,
        BudgetStatus planStatus,
        IEnumerable<BudgetTargetLine> targets,
        IEnumerable<BudgetActualRevenue> actuals)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(actuals);

        var targetsByCell = new Dictionary<(int Month, BudgetCategory Category), decimal>();

        foreach (var target in targets)
        {
            var cell = (target.Month, target.Category);
            targetsByCell[cell] = targetsByCell.GetValueOrDefault(cell) + target.AmountTarget;
        }

        var actualsByCell = new Dictionary<(int Month, BudgetCategory Category), decimal>();

        foreach (var actual in actuals)
        {
            // The report is keyed by the BUSINESS date of the revenue, never by the date it was
            // captured or validated: a revenue belongs to the day it was produced, which is the
            // only reading under which the monthly targets mean anything.
            var actualMonth = actual.BusinessDate.Month;

            Accumulate(actualsByCell, actualMonth, BudgetCategory.Accommodation, actual.Accommodation);
            Accumulate(actualsByCell, actualMonth, BudgetCategory.Food, actual.Food);
            Accumulate(actualsByCell, actualMonth, BudgetCategory.Beverage, actual.Beverage);
            Accumulate(actualsByCell, actualMonth, BudgetCategory.Other, actual.Other);
        }

        var monthsInScope = month.HasValue
            ? new[] { month.Value }
            : Enumerable.Range(1, 12).ToArray();

        var months = new List<BudgetVarianceMonth>(monthsInScope.Length);

        foreach (var currentMonth in monthsInScope)
        {
            var rows = Categories
                .Select(category =>
                {
                    var budgetAmount = Round(targetsByCell.GetValueOrDefault((currentMonth, category)));
                    var actualAmount = Round(actualsByCell.GetValueOrDefault((currentMonth, category)));

                    return new BudgetVarianceRow(
                        currentMonth,
                        category,
                        budgetAmount,
                        actualAmount,
                        actualAmount - budgetAmount,
                        Percentage(budgetAmount, actualAmount - budgetAmount));
                })
                .ToArray();

            var monthBudget = rows.Sum(row => row.BudgetAmount);
            var monthActual = rows.Sum(row => row.ActualAmount);

            months.Add(new BudgetVarianceMonth(
                currentMonth,
                rows,
                monthBudget,
                monthActual,
                monthActual - monthBudget,
                Percentage(monthBudget, monthActual - monthBudget)));
        }

        var totalBudget = months.Sum(current => current.BudgetAmount);
        var totalActual = months.Sum(current => current.ActualAmount);

        return new BudgetVarianceResponse(
            year,
            hotelUnitCode,
            month,
            budgetPlanId,
            planStatus,
            months,
            totalBudget,
            totalActual,
            totalActual - totalBudget,
            Percentage(totalBudget, totalActual - totalBudget));
    }

    private static void Accumulate(
        Dictionary<(int Month, BudgetCategory Category), decimal> accumulator,
        int month,
        BudgetCategory category,
        decimal amount)
    {
        var cell = (month, category);
        accumulator[cell] = accumulator.GetValueOrDefault(cell) + amount;
    }

    /// <summary>
    /// The relative gap, in percent, rounded to two decimals.
    ///
    /// DIVISION BY ZERO - the behaviour is null, deliberately, and it is not an error state. When
    /// nothing was budgeted for a cell there is no reference to be relative to: an actual of
    /// 50 000 DZD against a target of 0 is not "infinitely above target", it is a figure whose
    /// percentage simply does not exist. Returning 0 would read as "on target" and returning some
    /// large number would invent a denominator, so the percentage is left undefined and the
    /// consumer displays a dash. The variance IN VALUE is always populated, including in that
    /// case, so nothing is lost: it is the percentage alone that is meaningless, not the gap.
    /// The same holds when target and actual are both zero.
    /// </summary>
    private static decimal? Percentage(decimal budgetAmount, decimal varianceAmount)
    {
        if (budgetAmount == 0m)
        {
            return null;
        }

        return Math.Round(varianceAmount / budgetAmount * 100m, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal Round(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
