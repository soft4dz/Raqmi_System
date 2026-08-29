using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Closing;

/// <summary>
/// Official lock of one business day for one hotel unit (night audit closing).
/// A day is created directly in the <see cref="ClosingStatus.Closed"/> state; it can be
/// reopened (with a mandatory reason) by a control profile, and a reopened day can be
/// closed again through <see cref="CloseAgain"/>. When a day is re-closed, the reopening
/// trail (ReopenedAt/ReopenedBy/ReopenReason) is intentionally kept so the last reopening
/// cycle stays visible for audit purposes.
/// </summary>
public sealed class DailyClosing : AuditableEntity
{
    private DailyClosing()
    {
    }

    public DailyClosing(
        DateOnly businessDate,
        string hotelUnitCode,
        string closedBy,
        DateTimeOffset closedAtUtc,
        string? notes = null)
    {
        BusinessDate = businessDate;
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Status = ClosingStatus.Closed;
        ClosedAt = closedAtUtc;
        ClosedBy = RequireActor(closedBy);
        Notes = NormalizeOptional(notes, nameof(notes), 1000);
    }

    public DateOnly BusinessDate { get; private set; }

    public string HotelUnitCode { get; private set; } = string.Empty;

    public ClosingStatus Status { get; private set; } = ClosingStatus.Closed;

    public DateTimeOffset ClosedAt { get; private set; }

    public string ClosedBy { get; private set; } = string.Empty;

    public DateTimeOffset? ReopenedAt { get; private set; }

    public string? ReopenedBy { get; private set; }

    public string? ReopenReason { get; private set; }

    public string? Notes { get; private set; }

    public bool IsClosed => Status == ClosingStatus.Closed;

    public void Reopen(string reason, string userName, DateTimeOffset utcNow)
    {
        if (Status != ClosingStatus.Closed)
        {
            throw new InvalidOperationException("Only a closed business day can be reopened.");
        }

        ReopenReason = RequireValue(reason, nameof(reason), 500);
        Status = ClosingStatus.Reopened;
        ReopenedAt = utcNow;
        ReopenedBy = RequireActor(userName);
    }

    public void CloseAgain(string userName, DateTimeOffset utcNow)
    {
        if (Status != ClosingStatus.Reopened)
        {
            throw new InvalidOperationException("Only a reopened business day can be closed again.");
        }

        Status = ClosingStatus.Closed;
        ClosedAt = utcNow;
        ClosedBy = RequireActor(userName);

        // ReopenedAt / ReopenedBy / ReopenReason are intentionally preserved: they document
        // the last reopening cycle of this business day.
    }

    private static string RequireActor(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return "system";
        }

        return userName.Trim();
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
