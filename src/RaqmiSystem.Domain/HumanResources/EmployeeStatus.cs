namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// Lifecycle of an employee record. Only <see cref="Active"/> employees are picked up by the
/// pre-payroll run: a terminated employee keeps every past payslip but must never receive a new
/// one for a period after departure.
/// </summary>
public enum EmployeeStatus
{
    Active = 0,
    Suspended = 1,
    Terminated = 2
}
