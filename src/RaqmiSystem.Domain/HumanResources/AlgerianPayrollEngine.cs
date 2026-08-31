namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// The Algerian payroll calculation, as a pure function of (facts, statutory parameters).
///
/// It is deliberately static and dependency-free: no database, no clock, no service. A payslip is
/// the one artefact of this ERP that an employee, a CNAS inspector or a labour inspector may
/// contest years later, so the calculation has to be reproducible from its inputs alone and
/// testable line by line without a host. Every rate, threshold and bracket arrives through
/// <see cref="PayrollParameters"/> - the engine encodes the SHAPE of Algerian payroll (gross,
/// then contributions, then progressive tax on the abated remainder), never the values of a
/// given year.
///
/// Order of operations, which is the part the law fixes:
/// <list type="number">
///   <item>gross = contractual base + overtime + bonuses - unpaid-absence deduction;</item>
///   <item>the employee social contribution is withheld on that gross;</item>
///   <item>the IRG base is the gross MINUS that contribution, minus the abatement;</item>
///   <item>the progressive scale applies to that base;</item>
///   <item>net = gross - contribution - IRG.</item>
/// </list>
/// Employer charges are computed on the same gross and never touch the net.
/// </summary>
public static class AlgerianPayrollEngine
{
    /// <summary>
    /// Money is rounded half-away-from-zero to the centime, the convention payroll documents use.
    /// Rounding happens on each published line and the totals are then derived FROM the rounded
    /// lines, so the payslip adds up exactly as printed (see <see cref="PayslipComputation"/>).
    /// </summary>
    private const int MoneyScale = 2;

    private const int HoursScale = 2;

    public static PayslipComputation Compute(
        decimal contractualGrossSalary,
        decimal hoursWorked,
        decimal unpaidAbsenceDays,
        decimal bonusTotal,
        int dependentChildren,
        PayrollParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        RequireNotNegative(contractualGrossSalary, nameof(contractualGrossSalary));
        RequireNotNegative(hoursWorked, nameof(hoursWorked));
        RequireNotNegative(unpaidAbsenceDays, nameof(unpaidAbsenceDays));
        RequireNotNegative(bonusTotal, nameof(bonusTotal));

        if (dependentChildren < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dependentChildren),
                "Dependent children cannot be negative.");
        }

        RequireParameters(parameters);

        var baseGross = RoundMoney(contractualGrossSalary);
        var hours = RoundHours(hoursWorked);
        var absenceDays = RoundHours(unpaidAbsenceDays);
        var bonuses = RoundMoney(bonusTotal);

        // Overtime is only what exceeds the monthly reference; the hourly rate is derived from the
        // contractual base, not from the gross, so a bonus never inflates the overtime rate.
        var hourlyRate = baseGross / parameters.MonthlyReferenceHours;
        var overtimeHours = RoundHours(Math.Max(0m, hours - parameters.MonthlyReferenceHours));
        var overtimeAmount = RoundMoney(overtimeHours * hourlyRate * parameters.OvertimeMultiplier);

        // Unpaid leave is deducted on the reference calendar month, not on the actual number of
        // days in the month: the daily value of a salary must not depend on whether the employee
        // was absent in February or in March.
        var dailyRate = baseGross / parameters.ReferenceDaysPerMonth;
        var absenceDeduction = RoundMoney(absenceDays * dailyRate);

        // Floored at zero: deductions can exceed the base on a month almost entirely unpaid, and a
        // negative gross would propagate a negative contribution and a negative net.
        var taxableGross = Math.Max(0m, baseGross + overtimeAmount + bonuses - absenceDeduction);

        var employeeSocial = RoundMoney(taxableGross * parameters.EmployeeSocialRate);
        var employerSocial = RoundMoney(taxableGross * parameters.EmployerSocialRate);
        var workAccident = RoundMoney(taxableGross * parameters.WorkAccidentRate);
        var unemployment = RoundMoney(taxableGross * parameters.UnemploymentInsuranceRate);
        var training = RoundMoney(taxableGross * parameters.VocationalTrainingRate);
        var employerPayrollTaxes = workAccident + unemployment + training;

        var abatement = parameters.IncomeTaxAbatement
            + (parameters.IncomeTaxAbatementPerChild * dependentChildren);

        // The IRG base is computed from the ALREADY ROUNDED contribution, so the figure can be
        // recomputed by hand from the printed lines of the payslip.
        var incomeTaxBase = Math.Max(0m, taxableGross - employeeSocial - abatement);
        var incomeTax = RoundMoney(ComputeProgressiveTax(incomeTaxBase, parameters.IncomeTaxBrackets));

        return new PayslipComputation
        {
            BaseGross = baseGross,
            HoursWorked = hours,
            OvertimeHours = overtimeHours,
            OvertimeAmount = overtimeAmount,
            UnpaidAbsenceDays = absenceDays,
            AbsenceDeduction = absenceDeduction,
            BonusTotal = bonuses,
            TaxableGross = taxableGross,
            EmployeeSocialContribution = employeeSocial,
            IncomeTaxBase = RoundMoney(incomeTaxBase),
            IncomeTax = incomeTax,
            NetPay = taxableGross - employeeSocial - incomeTax,
            EmployerSocialContribution = employerSocial,
            EmployerWorkAccident = workAccident,
            EmployerUnemploymentInsurance = unemployment,
            EmployerVocationalTraining = training,
            EmployerPayrollTaxes = employerPayrollTaxes,
            EmployerCost = taxableGross + employerSocial + employerPayrollTaxes,
            BelowMinimumWage = baseGross < parameters.MinimumWage
        };
    }

    /// <summary>
    /// Walks the scale from the bottom bracket up, taxing the fraction of the base that falls
    /// inside each bracket at the marginal rate of that bracket. The cumulative fixed amounts of
    /// the published scale ("6 900 + 27% above 30 000") come out of this walk instead of being
    /// stored alongside the rates, where they could contradict them.
    /// </summary>
    public static decimal ComputeProgressiveTax(
        decimal taxableBase,
        IReadOnlyList<IncomeTaxBracket> brackets)
    {
        ArgumentNullException.ThrowIfNull(brackets);

        if (taxableBase <= 0m)
        {
            return 0m;
        }

        var tax = 0m;
        var lowerBound = 0m;

        foreach (var bracket in brackets)
        {
            var upperBound = bracket.UpperBound ?? decimal.MaxValue;
            var fraction = Math.Min(taxableBase, upperBound) - lowerBound;

            if (fraction <= 0m)
            {
                break;
            }

            tax += fraction * bracket.Rate;
            lowerBound = upperBound;

            if (taxableBase <= upperBound)
            {
                break;
            }
        }

        return tax;
    }

    private static void RequireParameters(PayrollParameters parameters)
    {
        if (parameters.MonthlyReferenceHours <= 0m)
        {
            throw new ArgumentException(
                "Monthly reference hours must be greater than zero.",
                nameof(parameters));
        }

        if (parameters.ReferenceDaysPerMonth <= 0)
        {
            throw new ArgumentException(
                "Reference days per month must be greater than zero.",
                nameof(parameters));
        }

        if (parameters.IncomeTaxBrackets.Count == 0)
        {
            throw new ArgumentException(
                "At least one income tax bracket is required.",
                nameof(parameters));
        }
    }

    private static void RequireNotNegative(decimal value, string argumentName)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(argumentName, "Value cannot be negative.");
        }
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, MoneyScale, MidpointRounding.AwayFromZero);
    }

    private static decimal RoundHours(decimal value)
    {
        return Math.Round(value, HoursScale, MidpointRounding.AwayFromZero);
    }
}
