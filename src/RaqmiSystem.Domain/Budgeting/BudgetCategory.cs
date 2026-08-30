namespace RaqmiSystem.Domain.Budgeting;

/// <summary>
/// Revenue categories a budget can be broken down into. Deliberately the exact mirror of the
/// four amount columns carried by <c>RaqmiSystem.Domain.Revenue.DailyRevenue</c>
/// (Accommodation / Food / Beverage / Other): the whole point of the budgeting module is to put
/// a target and an actual side by side, and a category that has no counterpart in the recorded
/// daily revenue could never be confronted with anything. Adding a category here therefore only
/// makes sense together with a new amount column on DailyRevenue.
/// </summary>
public enum BudgetCategory
{
    Accommodation = 1,
    Food = 2,
    Beverage = 3,
    Other = 4
}
