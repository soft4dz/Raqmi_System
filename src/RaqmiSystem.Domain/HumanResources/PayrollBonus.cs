using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// A variable pay element attached to one employee and one period (attendance bonus, night
/// premium, exceptional reward...). Bonuses are captured before the pre-payroll run and are
/// summed into the gross by <see cref="AlgerianPayrollEngine"/>.
///
/// Amounts are strictly positive. Negative "bonuses" would be an easy way to model an ad-hoc
/// deduction, and that is exactly why they are refused: a withheld amount has its own legal
/// justification and its own place on a payslip, and letting it in through the bonus door would
/// hide it from any control that reads the bonus list.
/// </summary>
public sealed class PayrollBonus : AuditableEntity
{
    private PayrollBonus()
    {
    }

    public PayrollBonus(PayrollMonth period, Guid employeeId, string code, string label, decimal amount)
    {
        if (employeeId == Guid.Empty)
        {
            throw new ArgumentException("An employee is required.", nameof(employeeId));
        }

        Period = period;
        EmployeeId = employeeId;
        Code = HumanResourcesText.Require(code, nameof(code), 40).ToUpperInvariant();
        Label = HumanResourcesText.Require(label, nameof(label), 160);
        Amount = RequireAmount(amount);
    }

    public PayrollMonth Period { get; private set; }

    public Guid EmployeeId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public void UpdateDetails(string label, decimal amount)
    {
        Label = HumanResourcesText.Require(label, nameof(label), 160);
        Amount = RequireAmount(amount);
    }

    private static decimal RequireAmount(decimal value)
    {
        if (value <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "A bonus amount must be greater than zero.");
        }

        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
