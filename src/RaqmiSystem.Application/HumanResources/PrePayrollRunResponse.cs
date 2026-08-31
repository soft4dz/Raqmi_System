namespace RaqmiSystem.Application.HumanResources;

/// <summary>
/// Outcome of a pre-payroll run. <see cref="SkippedValidated"/> is the number of payslips left
/// untouched because they were already validated - reported rather than silent, so the operator
/// can see that re-running did NOT rewrite what had been signed off.
///
/// <see cref="Warnings"/> carries the compliance findings of the run (a contract below the
/// minimum wage, an employee with no active contract). They never block the run: the payroll of
/// the other employees must not be held hostage by one irregular file, but the finding has to
/// reach the operator.
/// </summary>
public sealed record PrePayrollRunResponse(
    string Period,
    int Generated,
    int Updated,
    int SkippedValidated,
    int EmployeesWithoutContract,
    decimal TotalTaxableGross,
    decimal TotalNetPay,
    decimal TotalEmployerCost,
    IReadOnlyCollection<string> Warnings);
