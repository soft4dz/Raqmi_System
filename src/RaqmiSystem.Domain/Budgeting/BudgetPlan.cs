using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Budgeting;

/// <summary>
/// The yearly budget of one hotel unit: a label, a status, and one target amount per month and
/// revenue category (see <see cref="BudgetLine"/>). There is at most one plan per
/// (Year, HotelUnitCode) - a unit cannot be steered against two competing budgets for the same
/// exercise - which the database enforces with the unique index ux_budget_plans_year_hotel_unit.
///
/// Lifecycle: Draft (freely editable) -> Approved (frozen) -> Closed (exercise over). Approval is
/// the engaging act: once approved, neither the plan nor any of its lines can be modified again,
/// because everything measured against the budget afterwards - variances, arbitrations, the
/// figures the direction commits to - would silently change meaning if the reference could still
/// be rewritten. That invariant lives here rather than in the service so no caller can bypass it.
/// </summary>
public sealed class BudgetPlan : AuditableEntity
{
    private readonly List<BudgetLine> _lines = new();

    private BudgetPlan()
    {
    }

    public BudgetPlan(int year, string hotelUnitCode, string label)
    {
        Year = RequireYear(year, nameof(year));
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Label = RequireValue(label, nameof(label), 160);
        Status = BudgetStatus.Draft;
    }

    public int Year { get; private set; }

    public string HotelUnitCode { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public BudgetStatus Status { get; private set; } = BudgetStatus.Draft;

    public DateTimeOffset? ApprovedAt { get; private set; }

    public string? ApprovedBy { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public string? ClosedBy { get; private set; }

    public IReadOnlyCollection<BudgetLine> Lines => _lines.AsReadOnly();

    public bool CanEdit => Status == BudgetStatus.Draft;

    public decimal TotalTarget => _lines.Sum(line => line.AmountTarget);

    public void Rename(string label)
    {
        EnsureEditable();
        Label = RequireValue(label, nameof(label), 160);
    }

    /// <summary>
    /// Creates or updates the single line of the plan for a given (month, category) pair. There
    /// can only ever be one - the pair is the line's business key, backed by the unique index
    /// ux_budget_lines_plan_month_category - so setting an existing pair adjusts its amount
    /// instead of adding a second, contradictory target.
    /// </summary>
    public BudgetLine SetLine(int month, BudgetCategory category, decimal amountTarget)
    {
        EnsureEditable();

        var normalizedMonth = BudgetLine.RequireMonth(month, nameof(month));
        var normalizedCategory = BudgetLine.RequireCategory(category, nameof(category));

        var existing = FindLine(normalizedMonth, normalizedCategory);

        if (existing is not null)
        {
            existing.UpdateAmount(amountTarget);
            return existing;
        }

        var line = new BudgetLine(normalizedMonth, normalizedCategory, amountTarget);
        _lines.Add(line);

        return line;
    }

    /// <summary>
    /// Replaces the whole grid of targets in one go. Duplicate (month, category) pairs are
    /// refused rather than last-one-wins: a payload carrying two different targets for the same
    /// cell is a caller mistake, and silently picking one of them would persist a budget nobody
    /// asked for.
    /// </summary>
    public void ReplaceLines(IReadOnlyCollection<BudgetLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        EnsureEditable();

        var incoming = new List<BudgetLine>();

        foreach (var line in lines)
        {
            ArgumentNullException.ThrowIfNull(line);

            if (incoming.Any(current => current.Month == line.Month && current.Category == line.Category))
            {
                throw new ArgumentException(
                    $"Duplicate budget line for month {line.Month} and category {line.Category}.",
                    nameof(lines));
            }

            incoming.Add(line);
        }

        // Reconciled in place rather than cleared-and-re-added: within a single SaveChanges, EF
        // gives no guarantee that the deletes of the replaced rows are emitted before the inserts
        // of their replacements, and the unique index on (budget_plan_id, month, category) would
        // then reject a replacement that only changes an amount. Updating the surviving rows also
        // keeps their Ids stable for callers holding on to them.
        _lines.RemoveAll(existing =>
            !incoming.Any(line => line.Month == existing.Month && line.Category == existing.Category));

        foreach (var line in incoming)
        {
            var existing = FindLine(line.Month, line.Category);

            if (existing is null)
            {
                _lines.Add(line);
            }
            else
            {
                existing.UpdateAmount(line.AmountTarget);
            }
        }
    }

    public bool RemoveLine(Guid lineId)
    {
        EnsureEditable();

        var line = _lines.SingleOrDefault(current => current.Id == lineId);

        if (line is null)
        {
            return false;
        }

        _lines.Remove(line);
        return true;
    }

    public void Approve(string userName, DateTimeOffset utcNow)
    {
        if (Status != BudgetStatus.Draft)
        {
            throw new InvalidOperationException("Only draft budget plans can be approved.");
        }

        // An empty budget commits the direction to nothing while looking like a decision; the
        // variance report it would feed is a column of zeros. Approving is refused until the plan
        // actually carries at least one target.
        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("A budget plan requires at least one line to be approved.");
        }

        Status = BudgetStatus.Approved;
        ApprovedAt = utcNow;
        ApprovedBy = RequireActor(userName);
    }

    public void Close(string userName, DateTimeOffset utcNow)
    {
        if (Status != BudgetStatus.Approved)
        {
            throw new InvalidOperationException("Only approved budget plans can be closed.");
        }

        Status = BudgetStatus.Closed;
        ClosedAt = utcNow;
        ClosedBy = RequireActor(userName);
    }

    private BudgetLine? FindLine(int month, BudgetCategory category)
    {
        return _lines.SingleOrDefault(line => line.Month == month && line.Category == category);
    }

    private void EnsureEditable()
    {
        if (Status != BudgetStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only draft budget plans can be modified. An approved budget is frozen.");
        }
    }

    private static int RequireYear(int year, string argumentName)
    {
        if (year is < 2000 or > 2999)
        {
            throw new ArgumentOutOfRangeException(argumentName, year, "Year is out of the supported range.");
        }

        return year;
    }

    private static string RequireActor(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return "system";
        }

        return userName.Trim();
    }

    private static string RequireValue(string value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }
}
