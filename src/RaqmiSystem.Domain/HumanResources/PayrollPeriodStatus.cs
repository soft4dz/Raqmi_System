namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// State of a monthly payroll period: Draft (open) then Validated (all payslips checked) then
/// Closed (locked for good). Closing is the compliance act of the module - see
/// <see cref="PayrollPeriod"/>.
/// </summary>
public enum PayrollPeriodStatus
{
    Draft = 0,
    Validated = 1,
    Closed = 2
}
