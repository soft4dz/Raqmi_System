using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Tests;

/// <summary>
/// Unit coverage of the HR invariants that live in the domain: the payroll period value object,
/// the contract term rules, the unpaid-day computation, the payslip freeze and the closing lock.
/// These are the rules no caller may bypass, so they are tested without any host.
/// </summary>
public sealed class HumanResourcesDomainTests
{
    [Theory]
    [InlineData("2026-01", 2026, 1)]
    [InlineData("2026-12", 2026, 12)]
    public void A_payroll_period_round_trips_through_its_text_form(string text, int year, int month)
    {
        var period = PayrollMonth.Parse(text);

        Assert.Equal(year, period.Year);
        Assert.Equal(month, period.Month);
        Assert.Equal(text, period.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026")]
    [InlineData("2026-13")]
    [InlineData("2026-00")]
    [InlineData("1999-05")]
    [InlineData("26-05")]
    [InlineData("2026/05")]
    public void An_unparsable_payroll_period_is_refused(string? text)
    {
        Assert.False(PayrollMonth.TryParse(text, out _));
    }

    [Fact]
    public void Payroll_periods_order_chronologically()
    {
        var january = PayrollMonth.Parse("2026-01");
        var december = PayrollMonth.Parse("2025-12");

        Assert.True(december < january);
        Assert.Equal(january, december.AddMonths(1));
        Assert.Equal(new DateOnly(2026, 1, 31), january.LastDay);

        // Month lengths come from the calendar, leap years included: 2026 is not one, 2028 is.
        Assert.Equal(new DateOnly(2026, 2, 28), PayrollMonth.Parse("2026-02").LastDay);
        Assert.Equal(new DateOnly(2028, 2, 29), PayrollMonth.Parse("2028-02").LastDay);
    }

    [Fact]
    public void An_open_ended_contract_cannot_carry_an_end_date()
    {
        Assert.Throws<ArgumentException>(() => new EmploymentContract(
            Guid.NewGuid(),
            ContractType.Permanent,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            60_000m,
            40m));
    }

    [Fact]
    public void A_fixed_term_contract_requires_an_end_date()
    {
        Assert.Throws<ArgumentException>(() => new EmploymentContract(
            Guid.NewGuid(),
            ContractType.FixedTerm,
            new DateOnly(2026, 1, 1),
            null,
            60_000m,
            40m));
    }

    [Fact]
    public void A_contract_covers_only_the_periods_between_its_bounds()
    {
        var contract = new EmploymentContract(
            Guid.NewGuid(),
            ContractType.FixedTerm,
            new DateOnly(2026, 3, 15),
            new DateOnly(2026, 5, 15),
            60_000m,
            40m);

        Assert.False(contract.CoversPeriod(PayrollMonth.Parse("2026-02")));
        Assert.True(contract.CoversPeriod(PayrollMonth.Parse("2026-03")));
        Assert.True(contract.CoversPeriod(PayrollMonth.Parse("2026-04")));
        Assert.True(contract.CoversPeriod(PayrollMonth.Parse("2026-05")));
        Assert.False(contract.CoversPeriod(PayrollMonth.Parse("2026-06")));
    }

    [Fact]
    public void An_ended_contract_can_no_longer_be_modified()
    {
        var contract = NewPermanentContract();
        contract.End(new DateOnly(2026, 6, 30), "End of trial period");

        Assert.Throws<InvalidOperationException>(() => contract.UpdateTerms(70_000m, 40m, null));
        Assert.Throws<InvalidOperationException>(() => contract.End(new DateOnly(2026, 7, 31), "Again"));
    }

    [Fact]
    public void Only_an_approved_unpaid_absence_deducts_days()
    {
        var employeeId = Guid.NewGuid();
        var period = PayrollMonth.Parse("2026-04");

        var pending = new AbsenceRequest(
            employeeId,
            AbsenceType.UnpaidLeave,
            new DateOnly(2026, 4, 10),
            new DateOnly(2026, 4, 12),
            null);

        // Still awaiting a decision: it must not reduce anyone's salary.
        Assert.Equal(0, pending.UnpaidDaysWithin(period));

        pending.Approve("rh.manager", DateTimeOffset.UtcNow, null);
        Assert.Equal(3, pending.UnpaidDaysWithin(period));

        var sickLeave = new AbsenceRequest(
            employeeId,
            AbsenceType.SickLeave,
            new DateOnly(2026, 4, 10),
            new DateOnly(2026, 4, 12),
            null);

        sickLeave.Approve("rh.manager", DateTimeOffset.UtcNow, null);

        // Compensated by CNAS rather than deducted by the employer.
        Assert.Equal(0, sickLeave.UnpaidDaysWithin(period));
        Assert.Equal(3, sickLeave.TotalDays);
    }

    [Fact]
    public void An_absence_spanning_two_months_counts_only_its_days_inside_the_period()
    {
        var absence = new AbsenceRequest(
            Guid.NewGuid(),
            AbsenceType.UnpaidLeave,
            new DateOnly(2026, 3, 30),
            new DateOnly(2026, 4, 2),
            null);

        absence.Approve("rh.manager", DateTimeOffset.UtcNow, null);

        Assert.Equal(4, absence.TotalDays);
        Assert.Equal(2, absence.UnpaidDaysWithin(PayrollMonth.Parse("2026-03")));
        Assert.Equal(2, absence.UnpaidDaysWithin(PayrollMonth.Parse("2026-04")));
        Assert.Equal(0, absence.UnpaidDaysWithin(PayrollMonth.Parse("2026-05")));
    }

    [Fact]
    public void An_absence_already_decided_cannot_be_decided_again()
    {
        var absence = new AbsenceRequest(
            Guid.NewGuid(),
            AbsenceType.AnnualLeave,
            new DateOnly(2026, 4, 10),
            new DateOnly(2026, 4, 12),
            null);

        absence.Approve("rh.manager", DateTimeOffset.UtcNow, null);

        Assert.Throws<InvalidOperationException>(() =>
            absence.Reject("rh.manager", DateTimeOffset.UtcNow, null));
    }

    [Fact]
    public void A_validated_payslip_can_no_longer_be_recomputed()
    {
        var payslip = new Payslip(PayrollMonth.Parse("2026-04"), Guid.NewGuid(), Computation(60_000m));

        payslip.Apply(Computation(65_000m));
        Assert.Equal(65_000m, payslip.BaseGross);

        payslip.Validate("rh.manager", DateTimeOffset.UtcNow);

        // The whole point of validating: a later run must not rewrite what was signed off.
        Assert.Throws<InvalidOperationException>(() => payslip.Apply(Computation(80_000m)));
        Assert.Equal(65_000m, payslip.BaseGross);
    }

    [Fact]
    public void A_period_must_be_validated_before_it_can_be_closed()
    {
        var period = new PayrollPeriod(PayrollMonth.Parse("2026-04"));

        Assert.Throws<InvalidOperationException>(() => period.Close("rh.manager", DateTimeOffset.UtcNow));

        period.Validate(3, "rh.manager", DateTimeOffset.UtcNow);
        period.Close("rh.manager", DateTimeOffset.UtcNow);

        Assert.Equal(PayrollPeriodStatus.Closed, period.Status);
        Assert.Equal(3, period.PayslipCount);
    }

    [Fact]
    public void A_closed_period_refuses_every_further_write_and_cannot_be_reopened()
    {
        var period = new PayrollPeriod(PayrollMonth.Parse("2026-04"));
        period.Validate(1, "rh.manager", DateTimeOffset.UtcNow);

        // Still open at this point: a payslip found wrong during the final review can be corrected.
        period.EnsureOpen();
        period.Reopen();
        Assert.Equal(PayrollPeriodStatus.Draft, period.Status);

        period.Validate(1, "rh.manager", DateTimeOffset.UtcNow);
        period.Close("rh.manager", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => period.EnsureOpen());
        Assert.Throws<InvalidOperationException>(() => period.Reopen());
        Assert.Throws<InvalidOperationException>(() => period.Validate(1, "rh.manager", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void An_employee_is_payable_only_for_the_periods_of_the_employment()
    {
        var employee = new Employee(
            "EMP-001",
            "Amina",
            "Belkacem",
            "UNIT1",
            "RECEP",
            new DateOnly(2026, 3, 10));

        Assert.False(employee.IsPayableFor(PayrollMonth.Parse("2026-02")));
        Assert.True(employee.IsPayableFor(PayrollMonth.Parse("2026-03")));

        employee.Terminate(new DateOnly(2026, 5, 20));

        // Terminated mid-May: still payable for May, never for June.
        Assert.True(employee.IsPayableFor(PayrollMonth.Parse("2026-05")));
        Assert.False(employee.IsPayableFor(PayrollMonth.Parse("2026-06")));
    }

    [Fact]
    public void A_terminated_employee_cannot_be_reactivated()
    {
        var employee = new Employee("EMP-002", "Omar", "Haddad", "UNIT1", "RECEP", new DateOnly(2026, 1, 5));
        employee.Terminate(new DateOnly(2026, 4, 30));

        Assert.Throws<InvalidOperationException>(() => employee.Reactivate());
        Assert.Throws<InvalidOperationException>(() => employee.Terminate(new DateOnly(2026, 5, 31)));
    }

    [Fact]
    public void A_tax_scale_must_be_ordered_and_closed_by_a_single_open_ended_bracket()
    {
        var set = PayrollParameterSet.CreateStatutoryDefault(PayrollMonth.Parse("2026-01"), "Base");

        // Bounds going backwards.
        Assert.Throws<ArgumentException>(() => set.ReplaceBrackets(new[]
        {
            new IncomeTaxBracket(120_000m, 0.23m),
            new IncomeTaxBracket(30_000m, 0.27m),
            new IncomeTaxBracket(null, 0.33m)
        }));

        // An open-ended bracket that is not the last one.
        Assert.Throws<ArgumentException>(() => set.ReplaceBrackets(new[]
        {
            new IncomeTaxBracket(null, 0.23m),
            new IncomeTaxBracket(120_000m, 0.27m)
        }));

        // A rate outside 0..1.
        Assert.Throws<ArgumentException>(() => set.ReplaceBrackets(new[]
        {
            new IncomeTaxBracket(null, 1.5m)
        }));

        // The refused scales left the valid one untouched.
        Assert.Equal(3, set.Brackets.Count);
    }

    [Fact]
    public void The_shipped_parameter_set_carries_the_ported_statutory_values()
    {
        var parameters = PayrollParameterSet
            .CreateStatutoryDefault(PayrollMonth.Parse("2026-01"), "Base")
            .ToParameters();

        Assert.Equal(173.33m, parameters.MonthlyReferenceHours);
        Assert.Equal(1.5m, parameters.OvertimeMultiplier);
        Assert.Equal(30, parameters.ReferenceDaysPerMonth);
        Assert.Equal(0.09m, parameters.EmployeeSocialRate);
        Assert.Equal(0.26m, parameters.EmployerSocialRate);
        Assert.Equal(40_000m, parameters.IncomeTaxAbatement);
        Assert.Equal(1_000m, parameters.IncomeTaxAbatementPerChild);
        Assert.Equal(20_000m, parameters.MinimumWage);
        Assert.Equal(3, parameters.IncomeTaxBrackets.Count);
        Assert.Null(parameters.IncomeTaxBrackets[^1].UpperBound);
    }

    private static EmploymentContract NewPermanentContract()
    {
        return new EmploymentContract(
            Guid.NewGuid(),
            ContractType.Permanent,
            new DateOnly(2026, 1, 1),
            null,
            60_000m,
            40m);
    }

    private static PayslipComputation Computation(decimal baseGross)
    {
        return AlgerianPayrollEngine.Compute(
            baseGross,
            173.33m,
            0m,
            0m,
            0,
            PayrollParameterSet
                .CreateStatutoryDefault(PayrollMonth.Parse("2026-01"), "Base")
                .ToParameters());
    }
}
