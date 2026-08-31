using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Mice;

/// <summary>
/// An event occupying one function space over a time slot: seminar, wedding, gala dinner.
///
/// THE OCCUPIED WINDOW IS NOT THE EVENT. A room is unavailable from the moment the crew starts
/// setting it up until the moment it is cleared, which is why <see cref="SetupMinutes"/> and
/// <see cref="TeardownMinutes"/> extend the slot. Ignoring the turnaround is the classic banqueting
/// failure: two events booked back to back, and the second client walks into a room still being
/// stripped from the first. <see cref="OccupiedFrom"/> and <see cref="OccupiedTo"/> hold that real
/// window and are the ONLY thing the double-booking guard compares.
///
/// Those two fields are derived and recomputed on every change of the slot, through the single
/// ApplySlot path: they are stored rather than computed on read so the overlap test can run in the
/// database, which is where the guard needs it.
///
/// The times are the WALL CLOCK of the hotel, not UTC. An event runs inside the building; storing
/// it in UTC would gain nothing and would shift every running order at the first daylight-saving
/// change.
/// </summary>
public sealed class EventBooking : AuditableEntity
{
    public const int ReferenceMaxLength = 24;
    public const int TitleMaxLength = 160;
    public const int NotesMaxLength = 1000;
    public const int CancelReasonMaxLength = 300;
    public const int CustomerCodeMaxLength = 32;

    public const int MinDurationMinutes = 15;

    /// <summary>A single booking longer than a week is a data-entry error, not an event.</summary>
    public const int MaxDurationMinutes = 7 * 24 * 60;

    /// <summary>Setup or teardown longer than a day is likewise a typo.</summary>
    public const int MaxBufferMinutes = 24 * 60;

    private readonly List<EventBookingLine> lines = [];

    private readonly List<EventScheduleItem> schedule = [];

    private EventBooking()
    {
    }

    public EventBooking(
        string hotelUnitCode,
        string reference,
        string functionSpaceCode,
        string customerCode,
        string title,
        DateOnly eventDate,
        TimeOnly startTime,
        int durationMinutes,
        int setupMinutes,
        int teardownMinutes,
        EventSetupStyle setupStyle,
        int expectedAttendance,
        string? notes = null)
    {
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Reference = NormalizeReference(reference);
        FunctionSpaceCode = FunctionSpace.NormalizeCode(functionSpaceCode);
        CustomerCode = RequireText(customerCode, nameof(customerCode), CustomerCodeMaxLength).ToUpperInvariant();
        Title = RequireText(title, nameof(title), TitleMaxLength);
        SetupStyle = setupStyle;
        ExpectedAttendance = RequireAttendance(expectedAttendance);
        Notes = NormalizeOptional(notes, NotesMaxLength);
        Status = EventBookingStatus.Draft;

        ApplySlot(eventDate, startTime, durationMinutes, setupMinutes, teardownMinutes);
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    /// <summary>Human-readable reference, unique WITHIN the unit. Printed on the BEO.</summary>
    public string Reference { get; private set; } = string.Empty;

    public string FunctionSpaceCode { get; private set; } = string.Empty;

    public string CustomerCode { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public DateOnly EventDate { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public int DurationMinutes { get; private set; }

    /// <summary>Minutes needed BEFORE the guests arrive, to lay the room out.</summary>
    public int SetupMinutes { get; private set; }

    /// <summary>Minutes needed AFTER the guests leave, to clear the room.</summary>
    public int TeardownMinutes { get; private set; }

    /// <summary>Start of the real occupation, setup included. Derived; see the type summary.</summary>
    public DateTime OccupiedFrom { get; private set; }

    /// <summary>End of the real occupation, teardown included. Derived; see the type summary.</summary>
    public DateTime OccupiedTo { get; private set; }

    public EventSetupStyle SetupStyle { get; private set; }

    public int ExpectedAttendance { get; private set; }

    public EventBookingStatus Status { get; private set; } = EventBookingStatus.Draft;

    public string? Notes { get; private set; }

    public string? CancelReason { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancelledBy { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public string? ConfirmedBy { get; private set; }

    /// <summary>
    /// Invoice generated from this event, once billed. Its presence FREEZES the priced lines: the
    /// invoice is the legal document, and letting the quote drift away from it afterwards would
    /// leave two contradictory versions of what the client owes.
    /// </summary>
    public Guid? InvoiceId { get; private set; }

    public IReadOnlyCollection<EventBookingLine> Lines => lines.AsReadOnly();

    public IReadOnlyCollection<EventScheduleItem> Schedule => schedule.AsReadOnly();

    /// <summary>A cancelled event releases its space; anything else holds it. See EventBookingStatus.</summary>
    public bool IsBlocking => Status != EventBookingStatus.Cancelled;

    public bool IsInvoiced => InvoiceId is not null;

    public decimal TotalExclVat => RoundMoney(lines.Sum(line => line.LineTotalExclVat));

    public decimal TotalVat => RoundMoney(lines.Sum(line => line.VatAmount));

    public decimal TotalInclVat => RoundMoney(TotalExclVat + TotalVat);

    public void UpdateDetails(
        string title,
        EventSetupStyle setupStyle,
        int expectedAttendance,
        string? notes)
    {
        RequireOpen();

        Title = RequireText(title, nameof(title), TitleMaxLength);
        SetupStyle = setupStyle;
        ExpectedAttendance = RequireAttendance(expectedAttendance);
        Notes = NormalizeOptional(notes, NotesMaxLength);
    }

    /// <summary>
    /// Moves the event to another space. Separate from the slot change because the two can happen
    /// independently, and because the caller MUST re-run the double-booking guard against the
    /// TARGET space either way.
    /// </summary>
    public void MoveToSpace(string functionSpaceCode)
    {
        RequireOpen();
        FunctionSpaceCode = FunctionSpace.NormalizeCode(functionSpaceCode);
    }

    /// <summary>Moves the event in time. The caller MUST re-run the double-booking guard afterwards.</summary>
    public void Reschedule(
        DateOnly eventDate,
        TimeOnly startTime,
        int durationMinutes,
        int setupMinutes,
        int teardownMinutes)
    {
        RequireOpen();
        ApplySlot(eventDate, startTime, durationMinutes, setupMinutes, teardownMinutes);
    }

    public void ReplaceLines(IEnumerable<EventBookingLine> newLines)
    {
        RequireOpen();

        if (IsInvoiced)
        {
            throw new InvalidOperationException(
                "The priced lines can no longer be changed: this event has already been invoiced.");
        }

        lines.Clear();

        var number = 1;

        foreach (var line in newLines)
        {
            line.SetLineNumber(number++);
            lines.Add(line);
        }
    }

    /// <summary>
    /// The running order stays editable AFTER invoicing, on purpose: the commercial document is
    /// settled but the operation is not, and the kitchen may still need to move a coffee break on
    /// the morning of the event itself.
    /// </summary>
    public void ReplaceSchedule(IEnumerable<EventScheduleItem> newSchedule)
    {
        RequireOpen();

        schedule.Clear();
        schedule.AddRange(newSchedule.OrderBy(item => item.StartTime));
    }

    public void Confirm(string userName, DateTimeOffset utcNow)
    {
        if (Status == EventBookingStatus.Cancelled)
        {
            throw new InvalidOperationException("A cancelled event cannot be confirmed.");
        }

        if (Status == EventBookingStatus.Confirmed)
        {
            return;
        }

        Status = EventBookingStatus.Confirmed;
        ConfirmedAt = utcNow;
        ConfirmedBy = RequireText(userName, nameof(userName), 160);
    }

    public void Cancel(string reason, string userName, DateTimeOffset utcNow)
    {
        if (Status == EventBookingStatus.Cancelled)
        {
            throw new InvalidOperationException("The event is already cancelled.");
        }

        if (IsInvoiced)
        {
            throw new InvalidOperationException(
                "This event has been invoiced: cancel the invoice first, so that the accounts and "
                + "the event never contradict each other.");
        }

        Status = EventBookingStatus.Cancelled;
        CancelReason = RequireText(reason, nameof(reason), CancelReasonMaxLength);
        CancelledAt = utcNow;
        CancelledBy = RequireText(userName, nameof(userName), 160);
    }

    public void AttachInvoice(Guid invoiceId)
    {
        if (invoiceId == Guid.Empty)
        {
            throw new ArgumentException("The invoice identifier is required.", nameof(invoiceId));
        }

        if (IsInvoiced)
        {
            throw new InvalidOperationException("This event has already been invoiced.");
        }

        if (Status != EventBookingStatus.Confirmed)
        {
            throw new InvalidOperationException("Only a confirmed event can be invoiced.");
        }

        if (lines.Count == 0)
        {
            throw new InvalidOperationException("An event without a single priced line cannot be invoiced.");
        }

        InvoiceId = invoiceId;
    }

    /// <summary>Single path setting the slot, so the derived window can never drift away from it.</summary>
    private void ApplySlot(
        DateOnly eventDate,
        TimeOnly startTime,
        int durationMinutes,
        int setupMinutes,
        int teardownMinutes)
    {
        DurationMinutes = RequireRange(durationMinutes, MinDurationMinutes, MaxDurationMinutes, nameof(durationMinutes));
        SetupMinutes = RequireRange(setupMinutes, 0, MaxBufferMinutes, nameof(setupMinutes));
        TeardownMinutes = RequireRange(teardownMinutes, 0, MaxBufferMinutes, nameof(teardownMinutes));

        EventDate = eventDate;
        StartTime = startTime;

        var start = eventDate.ToDateTime(startTime, DateTimeKind.Unspecified);

        OccupiedFrom = start.AddMinutes(-SetupMinutes);
        OccupiedTo = start.AddMinutes(DurationMinutes + TeardownMinutes);
    }

    private void RequireOpen()
    {
        if (Status == EventBookingStatus.Cancelled)
        {
            throw new InvalidOperationException("A cancelled event can no longer be modified.");
        }
    }

    private static string NormalizeReference(string reference)
    {
        return RequireText(reference, nameof(reference), ReferenceMaxLength).ToUpperInvariant();
    }

    private static int RequireAttendance(int attendance)
    {
        if (attendance <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attendance),
                "The expected attendance must be strictly positive.");
        }

        if (attendance > FunctionSpace.MaxCapacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attendance),
                $"The expected attendance cannot exceed {FunctionSpace.MaxCapacity}.");
        }

        return attendance;
    }

    private static int RequireRange(int value, int min, int max, string parameterName)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"The value must be between {min} and {max} minutes.");
        }

        return value;
    }

    private static string RequireText(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The value is required.", parameterName);
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"The value cannot exceed {maxLength} characters.", parameterName);
        }

        return trimmed;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static decimal RoundMoney(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
