using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// An employment contract. Its <see cref="GrossSalary"/> is the contractual base every payslip of
/// the module is computed from, which makes this entity the single monetary reference of the HR
/// side - the pre-payroll run never takes a salary from anywhere else.
///
/// Two invariants are enforced here rather than in a service, because a contract that violates
/// either one is not a bad request, it is a legally meaningless document:
/// <list type="bullet">
///   <item>a fixed-term contract (CDD, seasonal, internship) MUST carry an end date, and an
///   open-ended one (CDI) must not;</item>
///   <item>an employee has at most ONE active contract at a time - the database backs this with
///   the filtered unique index ux_hr_contracts_active_per_employee, so two concurrent creations
///   cannot both succeed and leave the pre-payroll run to pick a salary at random.</item>
/// </list>
/// </summary>
public sealed class EmploymentContract : AuditableEntity
{
    private EmploymentContract()
    {
    }

    public EmploymentContract(
        Guid employeeId,
        ContractType type,
        DateOnly startDate,
        DateOnly? endDate,
        decimal grossSalary,
        decimal weeklyHours)
    {
        if (employeeId == Guid.Empty)
        {
            throw new ArgumentException("An employee is required.", nameof(employeeId));
        }

        RequireTermConsistency(type, startDate, endDate);

        EmployeeId = employeeId;
        Type = type;
        StartDate = startDate;
        EndDate = endDate;
        GrossSalary = RequireSalary(grossSalary);
        WeeklyHours = RequireWeeklyHours(weeklyHours);
        Status = ContractStatus.Active;
    }

    public Guid EmployeeId { get; private set; }

    public ContractType Type { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    /// <summary>Contractual monthly gross salary, in DZD.</summary>
    public decimal GrossSalary { get; private set; }

    public decimal WeeklyHours { get; private set; }

    public ContractStatus Status { get; private set; } = ContractStatus.Active;

    public DateOnly? TerminatedOn { get; private set; }

    public string? TerminationReason { get; private set; }

    public bool IsActive => Status == ContractStatus.Active;

    /// <summary>
    /// True when the contract covers any day of the period - the test the pre-payroll run uses to
    /// decide whether this contract governs that month.
    ///
    /// The effective end is <see cref="TerminatedOn"/> when the contract was ended early, and
    /// <see cref="EndDate"/> otherwise. Reading only EndDate would make an open-ended contract
    /// ended last year look like it still governs today, and an employee gone months ago would
    /// keep receiving payslips.
    /// </summary>
    public bool CoversPeriod(PayrollMonth period)
    {
        if (StartDate > period.LastDay)
        {
            return false;
        }

        var effectiveEnd = TerminatedOn ?? EndDate;

        return effectiveEnd is null || effectiveEnd >= period.FirstDay;
    }

    public void UpdateTerms(decimal grossSalary, decimal weeklyHours, DateOnly? endDate)
    {
        EnsureNotEnded();
        RequireTermConsistency(Type, StartDate, endDate);

        GrossSalary = RequireSalary(grossSalary);
        WeeklyHours = RequireWeeklyHours(weeklyHours);
        EndDate = endDate;
    }

    public void Suspend()
    {
        EnsureNotEnded();
        Status = ContractStatus.Suspended;
    }

    public void Reactivate()
    {
        EnsureNotEnded();
        Status = ContractStatus.Active;
    }

    public void End(DateOnly terminatedOn, string reason)
    {
        EnsureNotEnded();

        if (terminatedOn < StartDate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(terminatedOn),
                "A contract cannot end before it starts.");
        }

        TerminatedOn = terminatedOn;
        TerminationReason = HumanResourcesText.Require(reason, nameof(reason), 400);
        Status = ContractStatus.Ended;
    }

    /// <summary>
    /// Checks the contract against the floor of its position. Called by the application layer,
    /// which is where the position is loaded; kept here so the rule reads next to the salary it
    /// constrains.
    /// </summary>
    public bool IsBelowPositionFloor(decimal positionMinimumGrossSalary)
    {
        return GrossSalary < positionMinimumGrossSalary;
    }

    private void EnsureNotEnded()
    {
        if (Status == ContractStatus.Ended)
        {
            throw new InvalidOperationException("An ended contract can no longer be modified.");
        }
    }

    private static void RequireTermConsistency(ContractType type, DateOnly startDate, DateOnly? endDate)
    {
        if (endDate is not null && endDate < startDate)
        {
            throw new ArgumentException("A contract cannot end before it starts.", nameof(endDate));
        }

        if (type == ContractType.Permanent)
        {
            if (endDate is not null)
            {
                throw new ArgumentException(
                    "An open-ended contract cannot carry an end date.",
                    nameof(endDate));
            }

            return;
        }

        if (endDate is null)
        {
            throw new ArgumentException(
                "A fixed-term, seasonal or internship contract requires an end date.",
                nameof(endDate));
        }
    }

    private static decimal RequireSalary(decimal value)
    {
        if (value <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "A contractual gross salary must be greater than zero.");
        }

        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal RequireWeeklyHours(decimal value)
    {
        // The ceiling is the statutory weekly maximum: a contract above it is a data-entry error,
        // and it would inflate every overtime computation derived from it.
        if (value is <= 0m or > 60m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Weekly hours must be greater than zero and at most 60.");
        }

        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
