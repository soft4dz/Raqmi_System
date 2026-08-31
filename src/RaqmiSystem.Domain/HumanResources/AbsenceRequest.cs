using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.HumanResources;

/// <summary>
/// An absence over a continuous date range: leave, sick leave, unpaid leave, maternity or an
/// exceptional authorisation.
///
/// Only an APPROVED absence of an unpaid type reduces a salary, and it does so through
/// <see cref="UnpaidDaysWithin"/>. Both halves of that sentence are enforced here: a request
/// still awaiting a decision must never deduct anything, and sick leave or maternity - which
/// CNAS compensates rather than the employer deducting - must never be counted as unpaid even
/// though the employee is away.
/// </summary>
public sealed class AbsenceRequest : AuditableEntity
{
    private AbsenceRequest()
    {
    }

    public AbsenceRequest(
        Guid employeeId,
        AbsenceType type,
        DateOnly startDate,
        DateOnly endDate,
        string? reason)
    {
        if (employeeId == Guid.Empty)
        {
            throw new ArgumentException("An employee is required.", nameof(employeeId));
        }

        if (endDate < startDate)
        {
            throw new ArgumentException("An absence cannot end before it starts.", nameof(endDate));
        }

        EmployeeId = employeeId;
        Type = type;
        StartDate = startDate;
        EndDate = endDate;
        Reason = HumanResourcesText.Optional(reason, nameof(reason), 400);
        Status = AbsenceStatus.Requested;
    }

    public Guid EmployeeId { get; private set; }

    public AbsenceType Type { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public string? Reason { get; private set; }

    public AbsenceStatus Status { get; private set; } = AbsenceStatus.Requested;

    public DateTimeOffset? DecidedAt { get; private set; }

    public string? DecidedBy { get; private set; }

    public string? DecisionNote { get; private set; }

    /// <summary>Total calendar days covered, both bounds included.</summary>
    public int TotalDays => EndDate.DayNumber - StartDate.DayNumber + 1;

    /// <summary>
    /// Days of this absence that fall inside the period AND actually reduce the salary: approved,
    /// and of an unpaid type. Everything else returns zero. An absence spanning two months is
    /// counted only for the days inside the requested period, so a run of March never deducts
    /// days that belong to April.
    /// </summary>
    public int UnpaidDaysWithin(PayrollMonth period)
    {
        if (Status != AbsenceStatus.Approved || !Type.IsUnpaid())
        {
            return 0;
        }

        var from = StartDate > period.FirstDay ? StartDate : period.FirstDay;
        var to = EndDate < period.LastDay ? EndDate : period.LastDay;

        return to < from ? 0 : to.DayNumber - from.DayNumber + 1;
    }

    public void UpdateDetails(AbsenceType type, DateOnly startDate, DateOnly endDate, string? reason)
    {
        EnsurePending();

        if (endDate < startDate)
        {
            throw new ArgumentException("An absence cannot end before it starts.", nameof(endDate));
        }

        Type = type;
        StartDate = startDate;
        EndDate = endDate;
        Reason = HumanResourcesText.Optional(reason, nameof(reason), 400);
    }

    public void Approve(string userName, DateTimeOffset utcNow, string? note)
    {
        EnsurePending();
        Status = AbsenceStatus.Approved;
        RecordDecision(userName, utcNow, note);
    }

    public void Reject(string userName, DateTimeOffset utcNow, string? note)
    {
        EnsurePending();
        Status = AbsenceStatus.Rejected;
        RecordDecision(userName, utcNow, note);
    }

    public void Cancel(string userName, DateTimeOffset utcNow, string? note)
    {
        if (Status is AbsenceStatus.Rejected or AbsenceStatus.Cancelled)
        {
            throw new InvalidOperationException("The absence is already closed.");
        }

        Status = AbsenceStatus.Cancelled;
        RecordDecision(userName, utcNow, note);
    }

    private void EnsurePending()
    {
        if (Status != AbsenceStatus.Requested)
        {
            throw new InvalidOperationException(
                "Only an absence still awaiting a decision can be modified or decided.");
        }
    }

    private void RecordDecision(string userName, DateTimeOffset utcNow, string? note)
    {
        DecidedAt = utcNow;
        DecidedBy = HumanResourcesText.Require(userName, nameof(userName), 160);
        DecisionNote = HumanResourcesText.Optional(note, nameof(note), 400);
    }
}
