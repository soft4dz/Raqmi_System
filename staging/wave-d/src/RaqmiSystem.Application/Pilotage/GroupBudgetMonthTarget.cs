namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// One monthly target of an APPROVED (or closed - equally frozen) budget plan, for one unit.
/// The caller only passes targets from frozen plans: a draft budget is not a reference anyone
/// committed to. Several rows may exist for the same (unit, year, month) - one per category
/// line - and the calculator simply sums them.
/// </summary>
public sealed record GroupBudgetMonthTarget(
    string HotelUnitCode,
    int Year,
    int Month,
    decimal AmountTarget);
