using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Tests;

/// <summary>
/// Unit coverage of the Algerian payroll calculation. The engine is a pure function, so these
/// tests need no host and no database - which is the point: a payslip may be contested years
/// later and its arithmetic has to be verifiable on its own.
///
/// Two kinds of assertion are used deliberately:
/// <list type="bullet">
///   <item>exact figures computed by hand from the published rules (the IRG scale, a clean hourly
///   rate, a 30-day absence deduction), so a silent change of a rate or of the order of
///   operations fails here rather than on a real payslip;</item>
///   <item>the identities the printed document must satisfy, checked on every scenario.</item>
/// </list>
/// </summary>
public sealed class AlgerianPayrollEngineTests
{
    private static PayrollParameters Parameters =>
        PayrollParameterSet
            .CreateStatutoryDefault(PayrollMonth.Parse("2026-01"), "Test parameters")
            .ToParameters();

    [Fact]
    public void Nominal_month_applies_cnas_then_the_abated_progressive_scale()
    {
        // 60 000 gross, exactly the monthly reference hours, nothing else.
        // CNAS employee 9% = 5 400. IRG base = 60 000 - 5 400 - 40 000 = 14 600, entirely inside
        // the first bracket: 14 600 x 23% = 3 358. Net = 60 000 - 5 400 - 3 358 = 51 242.
        var result = AlgerianPayrollEngine.Compute(
            contractualGrossSalary: 60_000m,
            hoursWorked: 173.33m,
            unpaidAbsenceDays: 0m,
            bonusTotal: 0m,
            dependentChildren: 0,
            Parameters);

        Assert.Equal(60_000m, result.TaxableGross);
        Assert.Equal(0m, result.OvertimeHours);
        Assert.Equal(5_400m, result.EmployeeSocialContribution);
        Assert.Equal(14_600m, result.IncomeTaxBase);
        Assert.Equal(3_358m, result.IncomeTax);
        Assert.Equal(51_242m, result.NetPay);
        Assert.False(result.BelowMinimumWage);

        AssertPayslipIdentitiesHold(result);
    }

    [Fact]
    public void Overtime_is_only_the_hours_above_the_monthly_reference_and_is_paid_at_one_and_a_half()
    {
        // 173 330 gross over 173.33 reference hours is an hourly rate of exactly 1 000, so the
        // overtime figure can be checked by hand: 10 hours x 1 000 x 1.5 = 15 000.
        var result = AlgerianPayrollEngine.Compute(
            contractualGrossSalary: 173_330m,
            hoursWorked: 183.33m,
            unpaidAbsenceDays: 0m,
            bonusTotal: 0m,
            dependentChildren: 0,
            Parameters);

        Assert.Equal(10m, result.OvertimeHours);
        Assert.Equal(15_000m, result.OvertimeAmount);
        Assert.Equal(188_330m, result.TaxableGross);

        AssertPayslipIdentitiesHold(result);
    }

    [Fact]
    public void Working_less_than_the_reference_never_produces_negative_overtime()
    {
        var result = AlgerianPayrollEngine.Compute(
            contractualGrossSalary: 60_000m,
            hoursWorked: 100m,
            unpaidAbsenceDays: 0m,
            bonusTotal: 0m,
            dependentChildren: 0,
            Parameters);

        Assert.Equal(0m, result.OvertimeHours);
        Assert.Equal(0m, result.OvertimeAmount);

        // Short hours alone do not cut the salary - only an approved unpaid absence does. This is
        // the rule that keeps a missing time entry from silently reducing someone's pay.
        Assert.Equal(60_000m, result.TaxableGross);
    }

    [Fact]
    public void Unpaid_days_are_valued_on_the_thirty_day_reference_month()
    {
        // 30 000 over a 30-day reference month is 1 000 a day: 3 unpaid days deduct 3 000.
        var result = AlgerianPayrollEngine.Compute(
            contractualGrossSalary: 30_000m,
            hoursWorked: 150m,
            unpaidAbsenceDays: 3m,
            bonusTotal: 0m,
            dependentChildren: 0,
            Parameters);

        Assert.Equal(3_000m, result.AbsenceDeduction);
        Assert.Equal(27_000m, result.TaxableGross);

        AssertPayslipIdentitiesHold(result);
    }

    [Fact]
    public void Gross_never_goes_negative_when_deductions_exceed_the_base()
    {
        var result = AlgerianPayrollEngine.Compute(
            contractualGrossSalary: 30_000m,
            hoursWorked: 0m,
            unpaidAbsenceDays: 45m,
            bonusTotal: 0m,
            dependentChildren: 0,
            Parameters);

        Assert.Equal(0m, result.TaxableGross);
        Assert.Equal(0m, result.EmployeeSocialContribution);
        Assert.Equal(0m, result.IncomeTax);
        Assert.Equal(0m, result.NetPay);
    }

    [Fact]
    public void Bonuses_enter_the_gross_and_are_therefore_taxed_and_charged()
    {
        var withoutBonus = AlgerianPayrollEngine.Compute(60_000m, 173.33m, 0m, 0m, 0, Parameters);
        var withBonus = AlgerianPayrollEngine.Compute(60_000m, 173.33m, 0m, 10_000m, 0, Parameters);

        Assert.Equal(70_000m, withBonus.TaxableGross);
        Assert.Equal(6_300m, withBonus.EmployeeSocialContribution);
        Assert.True(withBonus.IncomeTax > withoutBonus.IncomeTax);

        AssertPayslipIdentitiesHold(withBonus);
    }

    [Fact]
    public void Each_dependent_child_raises_the_abatement_and_lowers_the_income_tax()
    {
        var childless = AlgerianPayrollEngine.Compute(60_000m, 173.33m, 0m, 0m, 0, Parameters);
        var withTwo = AlgerianPayrollEngine.Compute(60_000m, 173.33m, 0m, 0m, 2, Parameters);

        // Abatement goes from 40 000 to 42 000, so the base drops by 2 000 - taxed at 23% inside
        // the first bracket, that is 460 DZD less income tax and 460 more in the pocket.
        Assert.Equal(childless.IncomeTaxBase - 2_000m, withTwo.IncomeTaxBase);
        Assert.Equal(childless.IncomeTax - 460m, withTwo.IncomeTax);
        Assert.Equal(childless.NetPay + 460m, withTwo.NetPay);
    }

    [Fact]
    public void Employer_charges_are_computed_on_the_gross_and_never_touch_the_net()
    {
        var result = AlgerianPayrollEngine.Compute(100_000m, 173.33m, 0m, 0m, 0, Parameters);

        Assert.Equal(26_000m, result.EmployerSocialContribution);
        Assert.Equal(1_250m, result.EmployerWorkAccident);
        Assert.Equal(1_500m, result.EmployerUnemploymentInsurance);
        Assert.Equal(1_000m, result.EmployerVocationalTraining);
        Assert.Equal(3_750m, result.EmployerPayrollTaxes);
        Assert.Equal(129_750m, result.EmployerCost);

        // The employer share is information on the payslip, not a withholding.
        Assert.Equal(
            result.TaxableGross - result.EmployeeSocialContribution - result.IncomeTax,
            result.NetPay);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10_000, 2_300)]
    // The bracket boundaries, where the published cumulative amounts are stated: 23% of the first
    // 30 000 is 6 900, and 6 900 + 27% of the next 90 000 is 31 200.
    [InlineData(30_000, 6_900)]
    [InlineData(120_000, 31_200)]
    [InlineData(200_000, 57_600)]
    public void The_progressive_scale_reproduces_the_published_cumulative_amounts(
        decimal taxableBase,
        decimal expectedTax)
    {
        var tax = AlgerianPayrollEngine.ComputeProgressiveTax(taxableBase, Parameters.IncomeTaxBrackets);

        Assert.Equal(expectedTax, tax);
    }

    [Fact]
    public void A_base_below_the_minimum_wage_is_flagged_without_altering_the_calculation()
    {
        var result = AlgerianPayrollEngine.Compute(18_000m, 173.33m, 0m, 0m, 0, Parameters);

        Assert.True(result.BelowMinimumWage);

        // The flag is a compliance alert, not a correction: the payslip still pays what the
        // contract says, and the irregular contract is what has to be fixed.
        Assert.Equal(18_000m, result.TaxableGross);
        Assert.Equal(1_620m, result.EmployeeSocialContribution);

        // 18 000 - 1 620 = 16 380, well under the 40 000 abatement, so no income tax is due.
        Assert.Equal(0m, result.IncomeTaxBase);
        Assert.Equal(0m, result.IncomeTax);
        Assert.Equal(16_380m, result.NetPay);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 0, -1)]
    public void Negative_inputs_are_refused(
        decimal gross,
        decimal hours,
        decimal unpaidDays,
        decimal bonuses)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AlgerianPayrollEngine.Compute(gross, hours, unpaidDays, bonuses, 0, Parameters));
    }

    [Fact]
    public void A_negative_child_count_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AlgerianPayrollEngine.Compute(60_000m, 173.33m, 0m, 0m, -1, Parameters));
    }

    /// <summary>
    /// The three identities a reader of the payslip can check by hand. They are asserted on every
    /// scenario rather than once, because it is precisely rounding that breaks them.
    /// </summary>
    private static void AssertPayslipIdentitiesHold(PayslipComputation result)
    {
        Assert.Equal(
            result.BaseGross + result.OvertimeAmount + result.BonusTotal - result.AbsenceDeduction,
            result.TaxableGross);

        Assert.Equal(
            result.TaxableGross - result.EmployeeSocialContribution - result.IncomeTax,
            result.NetPay);

        Assert.Equal(
            result.TaxableGross + result.EmployerSocialContribution + result.EmployerPayrollTaxes,
            result.EmployerCost);

        Assert.Equal(
            result.EmployerWorkAccident
                + result.EmployerUnemploymentInsurance
                + result.EmployerVocationalTraining,
            result.EmployerPayrollTaxes);
    }
}
