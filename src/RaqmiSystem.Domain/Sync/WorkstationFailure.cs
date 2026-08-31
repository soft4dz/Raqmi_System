using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Sync;

/// <summary>
/// One failure a workstation reported about itself: a call that did not go through, or that came
/// back with an error. It exists so that a manager can SEE that a workstation is having trouble,
/// after the fact.
///
/// This journal is structurally incomplete by design, and saying so plainly matters more than the
/// journal itself: a failure can only be reported once the link is back, so a workstation that
/// stays disconnected reports nothing, and one that is switched off mid-incident loses what it
/// had buffered. It answers "has this workstation been struggling?", never "is this the complete
/// list of what went wrong?".
///
/// Nothing from the business request is stored - no body, no payload, no field values. Only the
/// verb, the route, the status and a message ALREADY SANITISED by the client.
/// </summary>
public sealed class WorkstationFailure : AuditableEntity
{
    public const int MethodMaxLength = 8;
    public const int PathMaxLength = 256;
    public const int MessageMaxLength = 512;

    /// <summary>
    /// Drift is clamped to +/- 30 days. The bound is not cosmetic: a workstation whose clock is
    /// set to the year 2400 would otherwise overflow a 32-bit second count. A drift that hits the
    /// bound is meant to be read as "this clock is nonsense", not as a measurement.
    /// </summary>
    public const int MaxClockDriftSeconds = 30 * 24 * 3600;

    private WorkstationFailure()
    {
    }

    private WorkstationFailure(
        Guid eventId,
        Guid workstationId,
        string method,
        string path,
        int? statusCode,
        WorkstationFailureKind kind,
        string message,
        DateTime claimedAtUtc,
        DateTime serverNowUtc)
    {
        Id = eventId;
        WorkstationId = workstationId;
        Method = Truncate(method, MethodMaxLength);
        Path = Truncate(path, PathMaxLength);
        StatusCode = statusCode;
        Kind = kind;
        Message = Truncate(message, MessageMaxLength);
        ClaimedAtUtc = claimedAtUtc;
        RecordedAtUtc = serverNowUtc;
        ClockDriftSeconds = ComputeDrift(claimedAtUtc, serverNowUtc);
    }

    /// <summary>HTTP verb of the call that failed.</summary>
    public string Method { get; private set; } = string.Empty;

    /// <summary>Route of the call that failed, without query string (see the client sanitiser).</summary>
    public string Path { get; private set; } = string.Empty;

    /// <summary>Status code when the server did answer; null when the call never arrived.</summary>
    public int? StatusCode { get; private set; }

    public WorkstationFailureKind Kind { get; private set; }

    /// <summary>Diagnostic message, sanitised and truncated by the client before it is sent.</summary>
    public string Message { get; private set; } = string.Empty;

    public Guid WorkstationId { get; private set; }

    /// <summary>The instant the workstation CLAIMS the failure happened. Not trusted.</summary>
    public DateTime ClaimedAtUtc { get; private set; }

    /// <summary>The instant the server received the report. This one is trustworthy.</summary>
    public DateTime RecordedAtUtc { get; private set; }

    /// <summary>
    /// Server time minus claimed time, in seconds, clamped. It is kept because a large drift is
    /// itself an operational finding: a workstation with a wrong clock produces wrong business
    /// dates everywhere else in the product, and this journal is where it becomes visible.
    /// </summary>
    public int ClockDriftSeconds { get; private set; }

    public static WorkstationFailure Record(
        Guid eventId,
        Guid workstationId,
        string method,
        string path,
        int? statusCode,
        WorkstationFailureKind kind,
        string message,
        DateTime claimedAtUtc,
        DateTime serverNowUtc)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("The event identifier is required.", nameof(eventId));
        }

        if (workstationId == Guid.Empty)
        {
            throw new ArgumentException("The workstation identifier is required.", nameof(workstationId));
        }

        return new WorkstationFailure(
            eventId,
            workstationId,
            method,
            path,
            statusCode,
            kind,
            message,
            claimedAtUtc,
            serverNowUtc);
    }

    private static int ComputeDrift(DateTime claimedAtUtc, DateTime serverNowUtc)
    {
        var seconds = (serverNowUtc - claimedAtUtc).TotalSeconds;

        if (seconds > MaxClockDriftSeconds)
        {
            return MaxClockDriftSeconds;
        }

        if (seconds < -MaxClockDriftSeconds)
        {
            return -MaxClockDriftSeconds;
        }

        return (int)seconds;
    }

    // Never refuse a diagnostic report for being too long: an over-long message is trimmed. The
    // alternative - rejecting the batch - would blind the journal exactly when something unusual
    // is happening, which is when it is needed.
    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
