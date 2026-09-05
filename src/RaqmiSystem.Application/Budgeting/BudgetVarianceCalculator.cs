using RaqmiSystem.Domain.Budgeting;
using RaqmiSystem.Domain.Revenue;

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
/// SHAPE OF THE REPORT: every month of the requested period is emitted, and every category of
/// the report inside it, whether or not a target or an actual exists for that cell. A month
/// absent from the plan is a target of zero, not a missing row - the reader must be able to see
/// that nothing was budgeted for it. Les catégories du rapport sont celles que l'appelant fournit
/// (le paramétrage applicable à l'unité, dans son ordre), complétées par tout code rencontré dans
/// les objectifs ou le réalisé qui n'y figurerait pas : un montant enregistré n'est jamais
/// silencieusement écarté d'un rapport d'écarts.
/// </summary>
public sealed class BudgetVarianceCalculator
{
    public BudgetVarianceResponse Calculate(
        int year,
        string hotelUnitCode,
        int? month,
        Guid budgetPlanId,
        BudgetStatus planStatus,
        IReadOnlyList<BudgetVarianceCategory> categories,
        IEnumerable<BudgetTargetLine> targets,
        IEnumerable<BudgetActualRevenue> actuals)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(actuals);

        var targetsByCell = new Dictionary<(int Month, string Category), decimal>();

        foreach (var target in targets)
        {
            var cell = (target.Month, RevenueCategoryCodes.Normalize(target.Category, nameof(targets)));
            targetsByCell[cell] = targetsByCell.GetValueOrDefault(cell) + target.AmountTarget;
        }

        var actualsByCell = new Dictionary<(int Month, string Category), decimal>();

        foreach (var actual in actuals)
        {
            // The report is keyed by the BUSINESS date of the revenue, never by the date it was
            // captured or validated: a revenue belongs to the day it was produced, which is the
            // only reading under which the monthly targets mean anything.
            var cell = (actual.BusinessDate.Month, RevenueCategoryCodes.Normalize(actual.Category, nameof(actuals)));
            actualsByCell[cell] = actualsByCell.GetValueOrDefault(cell) + actual.Amount;
        }

        var reportCategories = ResolveCategories(categories, targetsByCell.Keys, actualsByCell.Keys);

        var monthsInScope = month.HasValue
            ? new[] { month.Value }
            : Enumerable.Range(1, 12).ToArray();

        var months = new List<BudgetVarianceMonth>(monthsInScope.Length);

        foreach (var currentMonth in monthsInScope)
        {
            var rows = reportCategories
                .Select(category =>
                {
                    var budgetAmount = Round(targetsByCell.GetValueOrDefault((currentMonth, category.Code)));
                    var actualAmount = Round(actualsByCell.GetValueOrDefault((currentMonth, category.Code)));

                    return new BudgetVarianceRow(
                        currentMonth,
                        category.Code,
                        category.Label,
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

    /// <summary>
    /// Les catégories fournies, dans leur ordre, puis tout code vu dans les objectifs ou le
    /// réalisé qui n'en fait pas partie (libellé par son code, par ordre alphabétique).
    /// </summary>
    private static IReadOnlyList<BudgetVarianceCategory> ResolveCategories(
        IReadOnlyList<BudgetVarianceCategory> categories,
        IEnumerable<(int Month, string Category)> targetCells,
        IEnumerable<(int Month, string Category)> actualCells)
    {
        var result = new List<BudgetVarianceCategory>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var category in categories)
        {
            var code = RevenueCategoryCodes.Normalize(category.Code, nameof(categories));

            if (seen.Add(code))
            {
                result.Add(new BudgetVarianceCategory(code, category.Label));
            }
        }

        var extras = targetCells
            .Concat(actualCells)
            .Select(cell => cell.Category)
            .Where(code => !seen.Contains(code))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal);

        foreach (var code in extras)
        {
            result.Add(new BudgetVarianceCategory(code, code));
        }

        return result;
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
