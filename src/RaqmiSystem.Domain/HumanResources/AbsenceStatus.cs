namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// Approval state of an absence request. Only <see cref="Approved"/> absences are deducted by
/// the pre-payroll run - a pending request must never reduce a salary.
/// </summary>
public enum AbsenceStatus
{
    Requested = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3
}
