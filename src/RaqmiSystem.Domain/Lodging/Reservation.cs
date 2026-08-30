using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// A stay of one customer in one room over [ArrivalDate, DepartureDate). Dates follow the hotel
/// convention: the night of the departure day is NOT part of the stay, so a reservation leaving
/// on the 10th and another arriving on the 10th share the room without conflict.
///
/// The nightly rate is resolved through the tariff module AT CREATION TIME and frozen into
/// <see cref="NightlyRateSnapshot"/> / <see cref="RatePlanCodeSnapshot"/> - the same snapshot
/// discipline as the issuer identity frozen into issued invoices: later tariff changes must
/// never rewrite the price a booking was taken at.
///
/// CENTRAL INVARIANT (anti double-booking): two reservations of the same room whose status is
/// neither Cancelled nor NoShow may never overlap (overlap = Arrival &lt; other.Departure AND
/// Departure &gt; other.Arrival). The entity exposes the vocabulary
/// (<see cref="IsBlocking"/>, <see cref="PeriodsOverlap"/>); the guarantee itself is enforced by
/// the service inside a Serializable transaction, because no single-row invariant can see the
/// other reservations.
/// </summary>
public sealed class Reservation : AuditableEntity
{
    private Reservation()
    {
    }

    public Reservation(
        string hotelUnitCode,
        Guid roomId,
        string customerCode,
        DateOnly arrivalDate,
        DateOnly departureDate,
        int guestCount,
        decimal nightlyRateSnapshot,
        string ratePlanCodeSnapshot)
    {
        if (roomId == Guid.Empty)
        {
            throw new ArgumentException("Room id is required.", nameof(roomId));
        }

        if (departureDate <= arrivalDate)
        {
            throw new ArgumentException(
                "The departure date must be after the arrival date (a reservation covers at least one night).",
                nameof(departureDate));
        }

        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        RoomId = roomId;
        CustomerCode = Customer.NormalizeCode(customerCode);
        ArrivalDate = arrivalDate;
        DepartureDate = departureDate;
        GuestCount = RequireStrictlyPositive(guestCount, nameof(guestCount));
        NightlyRateSnapshot = RequireMoney(nightlyRateSnapshot, nameof(nightlyRateSnapshot));
        RatePlanCodeSnapshot = RequireValue(ratePlanCodeSnapshot, nameof(ratePlanCodeSnapshot), 60);
        Status = ReservationStatus.Booked;
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    public Guid RoomId { get; private set; }

    public string CustomerCode { get; private set; } = string.Empty;

    public DateOnly ArrivalDate { get; private set; }

    public DateOnly DepartureDate { get; private set; }

    public int GuestCount { get; private set; }

    public ReservationStatus Status { get; private set; } = ReservationStatus.Booked;

    /// <summary>Price of one night, frozen at creation time. Never re-resolved afterwards.</summary>
    public decimal NightlyRateSnapshot { get; private set; }

    /// <summary>Rate plan the frozen price came from, for traceability.</summary>
    public string RatePlanCodeSnapshot { get; private set; } = string.Empty;

    public string? CancelReason { get; private set; }

    public DateTimeOffset? CheckedInAt { get; private set; }

    public string? CheckedInBy { get; private set; }

    public DateTimeOffset? CheckedOutAt { get; private set; }

    public string? CheckedOutBy { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancelledBy { get; private set; }

    public DateTimeOffset? NoShowAt { get; private set; }

    public string? NoShowBy { get; private set; }

    /// <summary>Number of nights of the stay (the departure night is not one of them).</summary>
    public int Nights => DepartureDate.DayNumber - ArrivalDate.DayNumber;

    /// <summary>
    /// True when this reservation keeps its room busy over its period: everything but Cancelled
    /// and NoShow. A CheckedOut stay stays blocking - those nights were really consumed.
    /// </summary>
    public bool IsBlocking => Status is not (ReservationStatus.Cancelled or ReservationStatus.NoShow);

    /// <summary>
    /// The overlap rule of the central invariant, in one place: two [arrival, departure) periods
    /// overlap when each one starts before the other one ends. Half-open on the departure day,
    /// so a back-to-back departure/arrival on the same date is NOT an overlap.
    /// </summary>
    public static bool PeriodsOverlap(
        DateOnly firstArrival,
        DateOnly firstDeparture,
        DateOnly secondArrival,
        DateOnly secondDeparture)
    {
        return firstArrival < secondDeparture && firstDeparture > secondArrival;
    }

    /// <summary>True when the guest sleeps in the room on the given night.</summary>
    public bool CoversNight(DateOnly night)
    {
        return ArrivalDate <= night && night < DepartureDate;
    }

    /// <summary>
    /// Booked -> CheckedIn. Allowed from the UTC eve of the arrival date (see the lower-bound
    /// comment) up to the departure date included; refused once the departure date is past,
    /// because a stale Booked reservation checked in months later would open a folio billing
    /// every original night.
    /// </summary>
    public void CheckIn(DateOnly today, string userName, DateTimeOffset utcNow)
    {
        if (Status != ReservationStatus.Booked)
        {
            throw new InvalidOperationException("Only a booked reservation can be checked in.");
        }

        // "today" is the UTC business day (the codebase's convention for every UtcNow-based
        // decision), but the guest lives in local time the server does not know. In Algeria
        // (UTC+1) a guest arriving at 00:30 local on the arrival day is still on the PREVIOUS
        // UTC date, so a strict "today < ArrivalDate" bound would refuse a perfectly legitimate
        // arrival-night check-in. The safest rule without a client timezone is to relax the
        // bound by exactly ONE day: check-in is accepted from the UTC eve of the arrival date.
        if (today < ArrivalDate.AddDays(-1))
        {
            throw new InvalidOperationException("A reservation cannot be checked in before its arrival date.");
        }

        // Upper bound: once the departure date is past, the stay this booking described cannot
        // happen anymore - the reservation should be cancelled or written off as a no-show.
        if (today > DepartureDate)
        {
            throw new InvalidOperationException(
                "A reservation whose departure date has passed can no longer be checked in. " +
                "Cancel it or mark it as a no-show instead.");
        }

        Status = ReservationStatus.CheckedIn;
        CheckedInAt = utcNow;
        CheckedInBy = RequireActor(userName);
    }

    /// <summary>
    /// CheckedIn -> CheckedOut. The zero-balance rule lives in the service (it needs the folio);
    /// the entity only guards the transition itself.
    /// </summary>
    public void CheckOut(string userName, DateTimeOffset utcNow)
    {
        if (Status != ReservationStatus.CheckedIn)
        {
            throw new InvalidOperationException("Only a checked-in reservation can be checked out.");
        }

        Status = ReservationStatus.CheckedOut;
        CheckedOutAt = utcNow;
        CheckedOutBy = RequireActor(userName);
    }

    /// <summary>Booked -> Cancelled, with a mandatory reason. Frees the room immediately.</summary>
    public void Cancel(string reason, string userName, DateTimeOffset utcNow)
    {
        if (Status != ReservationStatus.Booked)
        {
            throw new InvalidOperationException("Only a booked reservation can be cancelled.");
        }

        CancelReason = RequireValue(reason, nameof(reason), 500);
        Status = ReservationStatus.Cancelled;
        CancelledAt = utcNow;
        CancelledBy = RequireActor(userName);
    }

    /// <summary>
    /// Booked -> NoShow, only once the arrival date is PAST (strictly after it): while the
    /// arrival day is still running the guest may yet arrive, and check-in must stay possible.
    /// </summary>
    public void MarkNoShow(DateOnly today, string userName, DateTimeOffset utcNow)
    {
        if (Status != ReservationStatus.Booked)
        {
            throw new InvalidOperationException("Only a booked reservation can be marked as a no-show.");
        }

        if (today <= ArrivalDate)
        {
            throw new InvalidOperationException(
                "A reservation can only be marked as a no-show after its arrival date has passed.");
        }

        Status = ReservationStatus.NoShow;
        NoShowAt = utcNow;
        NoShowBy = RequireActor(userName);
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

    private static int RequireStrictlyPositive(int value, string argumentName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "Value must be strictly positive.");
        }

        return value;
    }

    private static decimal RequireMoney(decimal value, string argumentName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "Value cannot be negative.");
        }

        if (decimal.Round(value, 2) != value)
        {
            throw new ArgumentException("Value cannot have more than 2 decimal places.", argumentName);
        }

        return value;
    }
}
