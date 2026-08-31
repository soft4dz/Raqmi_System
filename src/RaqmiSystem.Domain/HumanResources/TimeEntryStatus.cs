namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// Validation state of a daily time entry. Only <see cref="Validated"/> entries feed the
/// pre-payroll run: raw or unreviewed hours must never reach a payslip.
/// </summary>
public enum TimeEntryStatus
{
    Draft = 0,
    Validated = 1
}
