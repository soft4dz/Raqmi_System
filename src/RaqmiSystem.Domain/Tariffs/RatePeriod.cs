using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Tariffs;

/// <summary>
/// A dated nightly price inside a <see cref="RatePlan"/> for one room type.
///
/// <para>
/// <c>RoomTypeCode</c> is a normalized string, deliberately WITHOUT a foreign key: room types
/// live in the accommodation (PMS) module, which is built in parallel and owns its own tables.
/// The two modules meet on the code convention only (trimmed, upper-cased, max 40 chars - the
/// same normalization every code in this repository uses), so a period referencing a room type
/// that does not exist yet simply never matches a resolution query - it can never break a save.
/// </para>
///
/// <para>
/// CENTRAL INVARIANT (enforced by <c>TariffService</c>, not here, because it spans rows): two
/// periods of the SAME plan and the SAME room type must never overlap, and the bounds are
/// INCLUSIVE on both sides - a period ending on the 10th and a period starting on the 10th DO
/// overlap, because the night of the 10th would then carry two prices and a night has exactly
/// one price. <see cref="Overlaps"/> is the single in-memory statement of that rule.
/// </para>
/// </summary>
public sealed class RatePeriod : AuditableEntity
{
    private RatePeriod()
    {
    }

    public RatePeriod(Guid ratePlanId, string roomTypeCode, DateOnly fromDate, DateOnly toDate, decimal nightlyAmount)
    {
        if (ratePlanId == Guid.Empty)
        {
            throw new ArgumentException("Rate plan id is required.", nameof(ratePlanId));
        }

        RatePlanId = ratePlanId;
        RoomTypeCode = NormalizeRoomTypeCode(roomTypeCode);
        ApplySchedule(fromDate, toDate, nightlyAmount);
    }

    public Guid RatePlanId { get; private set; }

    public string RoomTypeCode { get; private set; } = string.Empty;

    public DateOnly FromDate { get; private set; }

    public DateOnly ToDate { get; private set; }

    public decimal NightlyAmount { get; private set; }

    public void Reschedule(DateOnly fromDate, DateOnly toDate, decimal nightlyAmount)
    {
        ApplySchedule(fromDate, toDate, nightlyAmount);
    }

    public bool Covers(DateOnly night)
    {
        return FromDate <= night && night <= ToDate;
    }

    /// <summary>
    /// True when both periods price the same room type on at least one common night. Bounds are
    /// inclusive: [1..10] and [10..20] overlap on the night of the 10th.
    /// </summary>
    public bool Overlaps(RatePeriod other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return RoomTypeCode == other.RoomTypeCode
            && FromDate <= other.ToDate
            && other.FromDate <= ToDate;
    }

    public static string NormalizeRoomTypeCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Room type code is required.", nameof(value));
        }

        var trimmed = value.Trim();

        if (trimmed.Length > 40)
        {
            throw new ArgumentException("Room type code cannot exceed 40 characters.", nameof(value));
        }

        return trimmed.ToUpperInvariant();
    }

    private void ApplySchedule(DateOnly fromDate, DateOnly toDate, decimal nightlyAmount)
    {
        if (fromDate > toDate)
        {
            throw new ArgumentException("The period's from date cannot be after its to date.", nameof(fromDate));
        }

        FromDate = fromDate;
        ToDate = toDate;
        NightlyAmount = RequireNightlyAmount(nightlyAmount);
    }

    private static decimal RequireNightlyAmount(decimal value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Nightly amount must be strictly positive.");
        }

        // Stored as numeric(18,2): more precision than the column holds would be silently
        // truncated by PostgreSQL, so it is refused at the door instead.
        if (decimal.Round(value, 2) != value)
        {
            throw new ArgumentException("Nightly amount cannot carry more than 2 decimal places.", nameof(value));
        }

        return value;
    }
}
