namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// The complete result of one payslip calculation, line by line as it appears on the printed
/// document. Every monetary figure is already rounded to the centime, and the identities a
/// reader can check by hand hold exactly on these values:
/// <code>
/// TaxableGross = BaseGross + OvertimeAmount + BonusTotal - AbsenceDeduction
/// NetPay       = TaxableGross - EmployeeSocialContribution - IncomeTax
/// EmployerCost = TaxableGross + EmployerSocialContribution + EmployerPayrollTaxes
/// </code>
/// That is a deliberate property, not a side effect of rounding: a payslip whose printed lines
/// do not add up cannot be defended in front of an employee, an inspector or an auditor.
/// </summary>
public sealed record PayslipComputation
{
    public required decimal BaseGross { get; init; }

    public required decimal HoursWorked { get; init; }

    public required decimal OvertimeHours { get; init; }

    public required decimal OvertimeAmount { get; init; }

    public required decimal UnpaidAbsenceDays { get; init; }

    public required decimal AbsenceDeduction { get; init; }

    public required decimal BonusTotal { get; init; }

    /// <summary>Gross subject to contributions and tax - the base of everything below.</summary>
    public required decimal TaxableGross { get; init; }

    public required decimal EmployeeSocialContribution { get; init; }

    /// <summary>IRG base after the employee contribution and the abatement, floored at zero.</summary>
    public required decimal IncomeTaxBase { get; init; }

    public required decimal IncomeTax { get; init; }

    public required decimal NetPay { get; init; }

    /// <summary>Employer CNAS share. Shown on the payslip for information; never withheld.</summary>
    public required decimal EmployerSocialContribution { get; init; }

    public required decimal EmployerWorkAccident { get; init; }

    public required decimal EmployerUnemploymentInsurance { get; init; }

    public required decimal EmployerVocationalTraining { get; init; }

    /// <summary>Sum of the three employer payroll taxes above.</summary>
    public required decimal EmployerPayrollTaxes { get; init; }

    public required decimal EmployerCost { get; init; }

    /// <summary>
    /// True when the contractual base gross is below the statutory minimum wage. Purely a
    /// compliance flag: it never alters the calculation, it surfaces a contract that must be
    /// regularised.
    /// </summary>
    public required bool BelowMinimumWage { get; init; }
}
