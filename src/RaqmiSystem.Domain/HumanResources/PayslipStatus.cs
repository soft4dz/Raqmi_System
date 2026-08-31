namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// State of a payslip. A <see cref="Draft"/> payslip is recomputed by every pre-payroll run;
/// a <see cref="Validated"/> one is frozen and skipped, so re-running the generation can never
/// silently rewrite a payslip someone has already checked and signed off.
/// </summary>
public enum PayslipStatus
{
    Draft = 0,
    Validated = 1
}
