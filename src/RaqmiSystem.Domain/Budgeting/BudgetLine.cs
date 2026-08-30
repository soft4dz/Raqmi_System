namespace RaqmiSystem.Domain.Budgeting;

/// <summary>
/// One monthly target of a <see cref="BudgetPlan"/>, for a single revenue category. Modelled as a
/// child entity with its own table and a required FK to the plan (rather than an EF owned
/// collection), exactly like <c>RaqmiSystem.Domain.Billing.InvoiceLine</c>: a dedicated entity
/// keeps the snake_case table configuration, the named unique index on
/// (budget_plan_id, month, category) and the check constraints explicit, and lets a line carry a
/// stable Id that API responses can reference.
///
/// A line is only ever created or modified through its owning plan, which is what enforces the
/// "an approved budget is frozen" invariant: there is no way to reach a line without going
/// through <see cref="BudgetPlan"/> first.
/// </summary>
public sealed class BudgetLine
{
    private BudgetLine()
    {
    }

    public BudgetLine(int month, BudgetCategory category, decimal amountTarget)
    {
        Month = RequireMonth(month, nameof(month));
        Category = RequireCategory(category, nameof(category));
        AmountTarget = RequireAmount(amountTarget, nameof(amountTarget));
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid BudgetPlanId { get; private set; }

    /// <summary>Calendar month of the plan's year, 1 (January) to 12 (December).</summary>
    public int Month { get; private set; }

    public BudgetCategory Category { get; private set; }

    public decimal AmountTarget { get; private set; }

    /// <summary>
    /// Internal so the amount can only be changed through <see cref="BudgetPlan"/>, which checks
    /// the plan is still editable before touching any line.
    /// </summary>
    internal void UpdateAmount(decimal amountTarget)
    {
        AmountTarget = RequireAmount(amountTarget, nameof(amountTarget));
    }

    internal static int RequireMonth(int month, string argumentName)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(argumentName, month, "Month must be between 1 and 12.");
        }

        return month;
    }

    internal static BudgetCategory RequireCategory(BudgetCategory category, string argumentName)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(argumentName, category, "Budget category is not supported.");
        }

        return category;
    }

    /// <summary>
    /// A budget target is an amount of money to be reached: it can be zero (a category the unit
    /// is not expected to produce anything on) but never negative. The scale is capped at the two
    /// decimals the column stores, so a value that would be silently truncated at persistence
    /// time - making the stored target differ from the one that was validated on screen - is
    /// refused upfront, following the same rule as InvoiceLine.
    /// </summary>
    private static decimal RequireAmount(decimal amountTarget, string argumentName)
    {
        if (amountTarget < 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, amountTarget, "Value cannot be negative.");
        }

        if (decimal.Round(amountTarget, 2) != amountTarget)
        {
            throw new ArgumentException("Value cannot have more than 2 decimal places.", argumentName);
        }

        return amountTarget;
    }
}
