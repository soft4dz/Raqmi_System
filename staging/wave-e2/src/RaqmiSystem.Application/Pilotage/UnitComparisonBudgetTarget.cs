using RaqmiSystem.Domain.Budgeting;

namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// One monthly target of a budget plan, for one unit, with the owning plan's status: the
/// calculator applies the budget module's rule itself, in pure code - only a FROZEN plan
/// (Approved, or Closed: a closed plan was approved and remains a frozen reference for its
/// exercise, the exact rule of GroupDashboardCalculator and BudgetService) is a reference
/// anyone committed to; a Draft plan participates in nothing. Several rows may exist for the
/// same (unit, year, month) - one per category line - and the calculator simply sums them.
/// </summary>
public sealed record UnitComparisonBudgetTarget(
    string HotelUnitCode,
    int Year,
    int Month,
    decimal AmountTarget,
    BudgetStatus PlanStatus);
