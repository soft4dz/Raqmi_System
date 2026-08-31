using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// The monthly payroll period and its lock. There is exactly one row per period
/// (ux_hr_payroll_periods_period).
///
/// THIS IS THE COMPLIANCE MECHANISM OF THE MODULE. Lifecycle: Draft (payslips are generated and
/// corrected) then Validated (every payslip has been checked) then Closed (locked for good).
/// Once closed, nothing belonging to that period may be written again - no pre-payroll re-run,
/// no payslip edit, no bonus, no time-entry or absence change that would alter what was already
/// declared. Payroll that can be rewritten after the CNAS deposit and the bank transfers is
/// payroll no auditor can rely on, so the lock is a domain rule
/// (<see cref="EnsureOpen"/>) rather than a screen that hides a button.
///
/// Closing is deliberately one-way. Correcting a closed month is done by a regularisation on an
/// open period, which is exactly how the paper process works.
/// </summary>
public sealed class PayrollPeriod : AuditableEntity
{
    private PayrollPeriod()
    {
    }

    public PayrollPeriod(PayrollMonth period)
    {
        Period = period;
        Status = PayrollPeriodStatus.Draft;
    }

    public PayrollMonth Period { get; private set; }

    public PayrollPeriodStatus Status { get; private set; } = PayrollPeriodStatus.Draft;

    /// <summary>
    /// Number of payslips the period held when it was validated. Frozen on purpose: it is the
    /// figure the closing decision was taken on, and it stays readable in the history even if
    /// rows are later archived.
    /// </summary>
    public int PayslipCount { get; private set; }

    public DateTimeOffset? ValidatedAt { get; private set; }

    public string? ValidatedBy { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public string? ClosedBy { get; private set; }

    public bool IsClosed => Status == PayrollPeriodStatus.Closed;

    /// <summary>
    /// Guard every write touching this period must call. Throws when the period is closed.
    /// </summary>
    public void EnsureOpen()
    {
        if (Status == PayrollPeriodStatus.Closed)
        {
            throw new InvalidOperationException(
                $"Payroll period {Period} is closed - no further modification is allowed. "
                + "Correct it with a regularisation on an open period.");
        }
    }

    public void Validate(int payslipCount, string userName, DateTimeOffset utcNow)
    {
        if (Status != PayrollPeriodStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Payroll period {Period} cannot be validated because it is {Status}.");
        }

        if (payslipCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payslipCount), "Payslip count cannot be negative.");
        }

        Status = PayrollPeriodStatus.Validated;
        PayslipCount = payslipCount;
        ValidatedAt = utcNow;
        ValidatedBy = HumanResourcesText.Require(userName, nameof(userName), 160);
    }

    public void Close(string userName, DateTimeOffset utcNow)
    {
        // Validation first is not ceremony: it is the step that asserts every payslip of the month
        // was reviewed. Closing an unvalidated period would lock unreviewed figures for good.
        if (Status != PayrollPeriodStatus.Validated)
        {
            throw new InvalidOperationException(
                $"Payroll period {Period} must be validated before it can be closed.");
        }

        Status = PayrollPeriodStatus.Closed;
        ClosedAt = utcNow;
        ClosedBy = HumanResourcesText.Require(userName, nameof(userName), 160);
    }

    /// <summary>
    /// Sends a validated - never a closed - period back to draft, so a payslip found wrong during
    /// the final review can still be corrected before the point of no return.
    /// </summary>
    public void Reopen()
    {
        if (Status == PayrollPeriodStatus.Closed)
        {
            throw new InvalidOperationException(
                $"Payroll period {Period} is closed and can no longer be reopened.");
        }

        Status = PayrollPeriodStatus.Draft;
        PayslipCount = 0;
        ValidatedAt = null;
        ValidatedBy = null;
    }
}
