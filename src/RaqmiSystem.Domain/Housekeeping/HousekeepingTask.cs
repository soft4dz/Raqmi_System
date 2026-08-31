using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Housekeeping;

/// <summary>
/// One room to service on one day: the unit of work of the housekeeping sheet, and the unit of
/// planning of the teams. The whole lifecycle lives HERE rather than in the service, because
/// every transition is a rule about this row alone ("a task cannot be started before it is
/// assigned", "only a finished room can be inspected") - the kind of invariant an entity can
/// actually guarantee. The service adds what needs to see other rows: the day's generation, and
/// the room condition each transition drives.
///
/// <see cref="RoomNumber"/> is a SNAPSHOT of the room's number at planning time. A room
/// renumbered afterwards must not rewrite the sheets of past days: the history has to keep
/// reading the way the team lived it. <see cref="RoomId"/> stays the identity.
///
/// Assignment is a free-text attendant name, not a user account: housekeeping staff rarely hold
/// application credentials, and inventing accounts for them to satisfy a foreign key would be
/// worse than a name. It becomes a real link the day module 21 (RH) lands.
/// </summary>
public sealed class HousekeepingTask : AuditableEntity
{
    private HousekeepingTask()
    {
    }

    public HousekeepingTask(
        string hotelUnitCode,
        Guid roomId,
        string roomNumber,
        DateOnly serviceDate,
        HousekeepingTaskType taskType,
        string? notes = null)
    {
        if (roomId == Guid.Empty)
        {
            throw new ArgumentException("Room id is required.", nameof(roomId));
        }

        if (!Enum.IsDefined(taskType))
        {
            throw new ArgumentOutOfRangeException(nameof(taskType), taskType, "Unknown housekeeping task type.");
        }

        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        RoomId = roomId;
        RoomNumber = Room.NormalizeNumber(roomNumber);
        ServiceDate = serviceDate;
        TaskType = taskType;
        Notes = NormalizeOptional(notes, nameof(notes), 300);
        Status = HousekeepingTaskStatus.Pending;
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    public Guid RoomId { get; private set; }

    /// <summary>Room number as it read on the day the task was planned. Display and history only.</summary>
    public string RoomNumber { get; private set; } = string.Empty;

    public DateOnly ServiceDate { get; private set; }

    public HousekeepingTaskType TaskType { get; private set; }

    public HousekeepingTaskStatus Status { get; private set; }

    /// <summary>Name of the attendant the task is assigned to. Null while the task is unassigned.</summary>
    public string? AssignedTo { get; private set; }

    public DateTimeOffset? AssignedAt { get; private set; }

    public string? AssignedBy { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public string? StartedBy { get; private set; }

    public DateTimeOffset? CleanedAt { get; private set; }

    public string? CleanedBy { get; private set; }

    /// <summary>
    /// Minutes between the LAST start and the completion, rounded to the minute. Reset on each
    /// new pass, so a room refused and redone reports the pass the supervisor ends up accepting.
    /// </summary>
    public int? DurationMinutes { get; private set; }

    public DateTimeOffset? InspectedAt { get; private set; }

    public string? InspectedBy { get; private set; }

    /// <summary>Supervisor remark. Mandatory on a refusal, optional on an acceptance.</summary>
    public string? InspectionNotes { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancelledBy { get; private set; }

    public string? CancelReason { get; private set; }

    /// <summary>Free instruction left at planning time (VIP guest, baby cot, ...).</summary>
    public string? Notes { get; private set; }

    /// <summary>A task nothing can change any more: accepted by a supervisor, or withdrawn.</summary>
    public bool IsClosed => Status is HousekeepingTaskStatus.Inspected or HousekeepingTaskStatus.Cancelled;

    /// <summary>
    /// Assigns (or re-assigns) the task to an attendant. Allowed as long as the task is still
    /// live: a supervisor rebalancing the sheet at 11am must not be stopped because the room is
    /// already being done.
    /// </summary>
    public void AssignTo(string attendant, string actor, DateTimeOffset utcNow)
    {
        if (IsClosed)
        {
            throw new InvalidOperationException("A closed task cannot be assigned.");
        }

        AssignedTo = RequireValue(attendant, nameof(attendant), 160);
        AssignedAt = utcNow;
        AssignedBy = NormalizeActor(actor);
    }

    /// <summary>
    /// The attendant enters the room. Only a task that is planned - or that a supervisor sent
    /// back - can be started, and only once somebody is responsible for it.
    /// </summary>
    public void Start(string actor, DateTimeOffset utcNow)
    {
        if (Status is not (HousekeepingTaskStatus.Pending or HousekeepingTaskStatus.Rejected))
        {
            throw new InvalidOperationException("Only a pending or rejected task can be started.");
        }

        if (string.IsNullOrWhiteSpace(AssignedTo))
        {
            throw new InvalidOperationException(
                "The task must be assigned to an attendant before it can be started.");
        }

        Status = HousekeepingTaskStatus.InProgress;
        StartedAt = utcNow;
        StartedBy = NormalizeActor(actor);

        // A new pass invalidates the previous verdict AND the previous duration: what the sheet
        // must report is the work that ends up being accepted, not the attempt that was refused.
        InspectedAt = null;
        InspectedBy = null;
        DurationMinutes = null;
    }

    /// <summary>The attendant declares the room done. It now waits for inspection.</summary>
    public void MarkCleaned(string actor, DateTimeOffset utcNow, string? notes = null)
    {
        if (Status != HousekeepingTaskStatus.InProgress)
        {
            throw new InvalidOperationException("Only a task in progress can be marked as cleaned.");
        }

        Status = HousekeepingTaskStatus.Cleaned;
        CleanedAt = utcNow;
        CleanedBy = NormalizeActor(actor);

        if (notes is not null)
        {
            Notes = NormalizeOptional(notes, nameof(notes), 300);
        }

        // Defensive max: a clock skew between two application servers must never turn into a
        // negative duration on a sheet somebody reads to plan the next day.
        DurationMinutes = StartedAt is { } startedAt
            ? (int)Math.Max(0, Math.Round((utcNow - startedAt).TotalMinutes))
            : null;
    }

    /// <summary>
    /// The supervisor verdict on a finished room. Accepting closes the task; refusing demands a
    /// reason and sends the room back to work - a refusal nobody can explain teaches the
    /// attendant nothing.
    /// </summary>
    public void Inspect(bool accepted, string actor, DateTimeOffset utcNow, string? notes = null)
    {
        if (Status != HousekeepingTaskStatus.Cleaned)
        {
            throw new InvalidOperationException("Only a task declared cleaned can be inspected.");
        }

        InspectionNotes = accepted
            ? NormalizeOptional(notes, nameof(notes), 300)
            : RequireValue(notes ?? string.Empty, nameof(notes), 300);

        Status = accepted ? HousekeepingTaskStatus.Inspected : HousekeepingTaskStatus.Rejected;
        InspectedAt = utcNow;
        InspectedBy = NormalizeActor(actor);
    }

    /// <summary>Withdraws the task from the sheet. A closed task is already settled and stays so.</summary>
    public void Cancel(string reason, string actor, DateTimeOffset utcNow)
    {
        if (IsClosed)
        {
            throw new InvalidOperationException("A closed task cannot be cancelled.");
        }

        CancelReason = RequireValue(reason, nameof(reason), 300);
        Status = HousekeepingTaskStatus.Cancelled;
        CancelledAt = utcNow;
        CancelledBy = NormalizeActor(actor);
    }

    private static string NormalizeActor(string actor)
    {
        return string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim();
    }

    private static string RequireValue(string value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }

    private static string? NormalizeOptional(string? value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }
}
