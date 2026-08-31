using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Housekeeping;

/// <summary>
/// The housekeeping condition of ONE room, at most one row per room (guarded by a unique index
/// on room_id). The row is created lazily, the first time somebody declares something about the
/// room: a room that has never been serviced, refused or withdrawn has no row at all and is read
/// as <see cref="RoomConditionStatus.Clean"/> - a brand new room is sellable until proven
/// otherwise, and that presumption is stated in one place (the service's board projection)
/// rather than by seeding a row per room at setup time.
///
/// This entity carries the CLEANLINESS axis only. Whether the room is occupied tonight is a
/// question about reservations, answered by joining the lodging module at read time; storing it
/// here would create two truths about the same fact.
/// </summary>
public sealed class RoomCondition : AuditableEntity
{
    private RoomCondition()
    {
    }

    public RoomCondition(string hotelUnitCode, Guid roomId)
    {
        if (roomId == Guid.Empty)
        {
            throw new ArgumentException("Room id is required.", nameof(roomId));
        }

        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        RoomId = roomId;
        Status = RoomConditionStatus.Clean;
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    public Guid RoomId { get; private set; }

    public RoomConditionStatus Status { get; private set; }

    public DateTimeOffset? LastCleanedAt { get; private set; }

    public string? LastCleanedBy { get; private set; }

    public DateTimeOffset? LastInspectedAt { get; private set; }

    public string? LastInspectedBy { get; private set; }

    /// <summary>Why the room is out of order. Required by that status, cleared by every other one.</summary>
    public string? OutOfOrderReason { get; private set; }

    /// <summary>Date the room is expected back in service. Purely indicative: nothing expires on its own.</summary>
    public DateOnly? OutOfOrderUntil { get; private set; }

    /// <summary>
    /// Moves the room to <paramref name="status"/> and stamps the trail that goes with it:
    /// reaching Clean records who serviced it, reaching Inspected records who checked it, and
    /// OutOfOrder demands a reason. Any status other than OutOfOrder clears the withdrawal.
    /// </summary>
    public void Apply(
        RoomConditionStatus status,
        string actor,
        DateTimeOffset utcNow,
        string? outOfOrderReason = null,
        DateOnly? outOfOrderUntil = null)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown room condition status.");
        }

        if (status == RoomConditionStatus.OutOfOrder)
        {
            OutOfOrderReason = RequireValue(outOfOrderReason ?? string.Empty, nameof(outOfOrderReason), 300);
            OutOfOrderUntil = outOfOrderUntil;
        }
        else
        {
            OutOfOrderReason = null;
            OutOfOrderUntil = null;
        }

        // Inspected implies the room was cleaned first, so it refreshes the cleaning trail too:
        // a supervisor who inspects a room states that it is serviced, not only checked.
        if (status is RoomConditionStatus.Clean or RoomConditionStatus.Inspected)
        {
            LastCleanedAt = utcNow;
            LastCleanedBy = NormalizeActor(actor);
        }

        if (status == RoomConditionStatus.Inspected)
        {
            LastInspectedAt = utcNow;
            LastInspectedBy = NormalizeActor(actor);
        }

        Status = status;
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
}
