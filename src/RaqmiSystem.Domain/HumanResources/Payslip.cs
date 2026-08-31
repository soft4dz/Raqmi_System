using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// One payslip: the result of <see cref="AlgerianPayrollEngine"/> for one employee and one
/// period, stored line by line. There is at most one per (period, employee) -
/// ux_hr_payslips_period_employee.
///
/// A DRAFT payslip is fully recomputed by every pre-payroll run, so correcting a time entry, an
/// absence or a bonus and re-running is the normal way to fix a figure. A VALIDATED payslip is
/// frozen: the run skips it, and <see cref="Apply"/> refuses to touch it. That is what makes
/// "regenerate the month" a safe operation once part of the payroll has already been checked and
/// signed off.
///
/// Employee identity (name, NSS, NIN, RIB) is NOT copied here. It is read from
/// <see cref="Employee"/> when a payslip or a declaration is produced, so a corrected social
/// security number propagates to every document instead of leaving each payslip carrying its own
/// stale copy - and the correction itself stays visible in the audit trail.
/// </summary>
public sealed class Payslip : AuditableEntity
{
    private Payslip()
    {
    }

    public Payslip(PayrollMonth period, Guid employeeId, PayslipComputation computation)
    {
        if (employeeId == Guid.Empty)
        {
            throw new ArgumentException("An employee is required.", nameof(employeeId));
        }

        Period = period;
        EmployeeId = employeeId;
        Status = PayslipStatus.Draft;
        CopyFrom(computation);
    }

    public PayrollMonth Period { get; private set; }

    public Guid EmployeeId { get; private set; }

    public PayslipStatus Status { get; private set; } = PayslipStatus.Draft;

    public decimal BaseGross { get; private set; }

    public decimal HoursWorked { get; private set; }

    public decimal OvertimeHours { get; private set; }

    public decimal OvertimeAmount { get; private set; }

    public decimal UnpaidAbsenceDays { get; private set; }

    public decimal AbsenceDeduction { get; private set; }

    public decimal BonusTotal { get; private set; }

    public decimal TaxableGross { get; private set; }

    public decimal EmployeeSocialContribution { get; private set; }

    public decimal IncomeTaxBase { get; private set; }

    public decimal IncomeTax { get; private set; }

    public decimal NetPay { get; private set; }

    public decimal EmployerSocialContribution { get; private set; }

    public decimal EmployerWorkAccident { get; private set; }

    public decimal EmployerUnemploymentInsurance { get; private set; }

    public decimal EmployerVocationalTraining { get; private set; }

    public decimal EmployerPayrollTaxes { get; private set; }

    public decimal EmployerCost { get; private set; }

    public bool BelowMinimumWage { get; private set; }

    public DateTimeOffset? ValidatedAt { get; private set; }

    public string? ValidatedBy { get; private set; }

    public bool IsDraft => Status == PayslipStatus.Draft;

    /// <summary>
    /// Replaces every computed line with a fresh calculation. Refused on a validated payslip:
    /// re-running the generation must never rewrite a figure someone has already signed off.
    /// </summary>
    public void Apply(PayslipComputation computation)
    {
        if (Status != PayslipStatus.Draft)
        {
            throw new InvalidOperationException(
                "A validated payslip can no longer be recomputed.");
        }

        CopyFrom(computation);
    }

    public void Validate(string userName, DateTimeOffset utcNow)
    {
        if (Status == PayslipStatus.Validated)
        {
            throw new InvalidOperationException("The payslip is already validated.");
        }

        Status = PayslipStatus.Validated;
        ValidatedAt = utcNow;
        ValidatedBy = HumanResourcesText.Require(userName, nameof(userName), 160);
    }

    /// <summary>
    /// Sends a validated payslip back to draft so the period can be corrected. The caller is
    /// responsible for checking that the period is still open - only it knows that.
    /// </summary>
    public void Reopen()
    {
        Status = PayslipStatus.Draft;
        ValidatedAt = null;
        ValidatedBy = null;
    }

    private void CopyFrom(PayslipComputation computation)
    {
        ArgumentNullException.ThrowIfNull(computation);

        BaseGross = computation.BaseGross;
        HoursWorked = computation.HoursWorked;
        OvertimeHours = computation.OvertimeHours;
        OvertimeAmount = computation.OvertimeAmount;
        UnpaidAbsenceDays = computation.UnpaidAbsenceDays;
        AbsenceDeduction = computation.AbsenceDeduction;
        BonusTotal = computation.BonusTotal;
        TaxableGross = computation.TaxableGross;
        EmployeeSocialContribution = computation.EmployeeSocialContribution;
        IncomeTaxBase = computation.IncomeTaxBase;
        IncomeTax = computation.IncomeTax;
        NetPay = computation.NetPay;
        EmployerSocialContribution = computation.EmployerSocialContribution;
        EmployerWorkAccident = computation.EmployerWorkAccident;
        EmployerUnemploymentInsurance = computation.EmployerUnemploymentInsurance;
        EmployerVocationalTraining = computation.EmployerVocationalTraining;
        EmployerPayrollTaxes = computation.EmployerPayrollTaxes;
        EmployerCost = computation.EmployerCost;
        BelowMinimumWage = computation.BelowMinimumWage;
    }
}
