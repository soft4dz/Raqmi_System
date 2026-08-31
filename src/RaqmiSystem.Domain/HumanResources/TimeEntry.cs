using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// The hours one employee worked on one day. There is at most one entry per (employee, day) -
/// backed by the unique index ux_hr_time_entries_employee_date - because two rows for the same
/// day would be summed by the pre-payroll run and silently double-count the overtime.
///
/// Only <see cref="TimeEntryStatus.Validated"/> entries feed payroll. An entry produced from a
/// time clock lands as <see cref="TimeEntryStatus.Draft"/> and must be reviewed: raw badge data
/// is evidence of presence, not an agreement on what should be paid.
/// </summary>
public sealed class TimeEntry : AuditableEntity
{
    private TimeEntry()
    {
    }

    public TimeEntry(Guid employeeId, DateOnly workDate, decimal hoursWorked, TimeEntrySource source)
    {
        if (employeeId == Guid.Empty)
        {
            throw new ArgumentException("An employee is required.", nameof(employeeId));
        }

        EmployeeId = employeeId;
        WorkDate = workDate;
        HoursWorked = RequireHours(hoursWorked);
        Source = source;
        Status = TimeEntryStatus.Draft;
    }

    public Guid EmployeeId { get; private set; }

    public DateOnly WorkDate { get; private set; }

    public decimal HoursWorked { get; private set; }

    public TimeEntrySource Source { get; private set; }

    public TimeEntryStatus Status { get; private set; } = TimeEntryStatus.Draft;

    public DateTimeOffset? ValidatedAt { get; private set; }

    public string? ValidatedBy { get; private set; }

    public void UpdateHours(decimal hoursWorked)
    {
        EnsureDraft();
        HoursWorked = RequireHours(hoursWorked);
    }

    public void Validate(string userName, DateTimeOffset utcNow)
    {
        if (Status == TimeEntryStatus.Validated)
        {
            throw new InvalidOperationException("The time entry is already validated.");
        }

        Status = TimeEntryStatus.Validated;
        ValidatedAt = utcNow;
        ValidatedBy = HumanResourcesText.Require(userName, nameof(userName), 160);
    }

    /// <summary>
    /// Sends a validated entry back to draft so it can be corrected. Reserved for periods that
    /// are still open - the payroll period lock is checked by the application layer, which is the
    /// only place that knows the period status.
    /// </summary>
    public void Reopen()
    {
        Status = TimeEntryStatus.Draft;
        ValidatedAt = null;
        ValidatedBy = null;
    }

    private void EnsureDraft()
    {
        if (Status != TimeEntryStatus.Draft)
        {
            throw new InvalidOperationException(
                "A validated time entry cannot be modified. Reopen it first.");
        }
    }

    private static decimal RequireHours(decimal value)
    {
        if (value is < 0m or > 24m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Hours worked in a day must be between 0 and 24.");
        }

        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
