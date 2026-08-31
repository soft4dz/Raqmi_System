using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>
    /// Bloc de groupe sur lequel cette reservation a ete prise, quand elle vient d'un allotement.
    /// Null pour une reservation publique.
    ///
    /// Ce rattachement n'est pas decoratif : il dit si la nuitee CONSOMME le bloc ou si elle mange
    /// l'inventaire public. Sans lui, une chambre prise sur le bloc serait comptee deux fois -
    /// une fois comme tenue, une fois comme vendue - et l'hotel s'interdirait de vendre des
    /// chambres pourtant libres.
    /// </summary>
    public Guid? AllotmentId { get; private set; }

    /// <summary>
    /// Nom de l'occupant, tel qu'il figure sur la rooming list du groupe. Null tant que le groupe
    /// n'a pas transmis ses noms, ce qui est l'etat normal d'un bloc pose des mois a l'avance.
    /// </summary>
    public string? GuestName { get; private set; }

    /// <summary>Rattache la reservation a un bloc de groupe, a la creation.</summary>
    public void AttachToAllotment(Guid allotmentId)
    {
        if (allotmentId == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant de l'allotement est requis.", nameof(allotmentId));
        }

        AllotmentId = allotmentId;
    }

    /// <summary>Renseigne ou efface le nom de l'occupant (rooming list).</summary>
    public void SetGuestName(string? guestName)
    {
        if (string.IsNullOrWhiteSpace(guestName))
        {
            GuestName = null;
            return;
        }

        var trimmed = guestName.Trim();

        GuestName = trimmed.Length <= 160 ? trimmed : trimmed[..160];
    }

    public ReservationStatus Status { get; private set; } = ReservationStatus.Booked;

    /// <summary>
    /// Price of the ARRIVAL night, frozen at creation time. Never re-resolved afterwards. When
    /// the stay crosses rate periods, the authoritative per-night detail lives in
    /// <see cref="NightlyRatesSnapshotJson"/>; this flat figure remains the arrival-night rate
    /// for display and for legacy rows created before the per-night detail existed.
    /// </summary>
    public decimal NightlyRateSnapshot { get; private set; }

    /// <summary>Rate plan the frozen arrival-night price came from, for traceability.</summary>
    public string RatePlanCodeSnapshot { get; private set; } = string.Empty;

    /// <summary>
    /// JSON array of the per-night frozen rates ([{"night","amount","ratePlanCode"}], one entry
    /// per night, ordered by night), written once by <see cref="FreezeNightlyRates"/> at
    /// creation time. Null on rows created before this detail existed - those stays billed (and
    /// keep billing) <see cref="NightlyRateSnapshot"/> flat, which <see cref="GetNightlyRates"/>
    /// falls back to.
    /// </summary>
    public string? NightlyRatesSnapshotJson { get; private set; }

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
    /// Freezes the per-night rate detail at creation time (same snapshot discipline as
    /// <see cref="NightlyRateSnapshot"/>: later tariff changes never rewrite it). The detail
    /// must cover EXACTLY the nights of [ArrivalDate, DepartureDate) and its arrival-night
    /// amount and plan must equal the flat snapshot - the two representations may never
    /// diverge. Only a Booked reservation (i.e. one being created) accepts it.
    /// </summary>
    public void FreezeNightlyRates(IReadOnlyCollection<ReservationNightRate> nightlyRates)
    {
        ArgumentNullException.ThrowIfNull(nightlyRates);

        if (Status != ReservationStatus.Booked)
        {
            throw new InvalidOperationException(
                "Nightly rates can only be frozen on a booked reservation, at creation time.");
        }

        if (nightlyRates.Count != Nights)
        {
            throw new ArgumentException(
                $"The nightly rate detail must carry exactly one entry per night ({Nights}), got {nightlyRates.Count}.",
                nameof(nightlyRates));
        }

        var ordered = nightlyRates.OrderBy(rate => rate.Night).ToArray();
        var expectedNight = ArrivalDate;

        foreach (var rate in ordered)
        {
            if (rate.Night != expectedNight)
            {
                throw new ArgumentException(
                    $"The nightly rate detail must cover each night of the stay exactly once; " +
                    $"expected {expectedNight:yyyy-MM-dd}, got {rate.Night:yyyy-MM-dd}.",
                    nameof(nightlyRates));
            }

            RequireMoney(rate.Amount, nameof(nightlyRates));
            RequireValue(rate.RatePlanCode, nameof(nightlyRates), 60);
            expectedNight = expectedNight.AddDays(1);
        }

        if (ordered[0].Amount != NightlyRateSnapshot)
        {
            throw new ArgumentException(
                "The arrival-night amount of the detail must equal the flat nightly rate snapshot.",
                nameof(nightlyRates));
        }

        NightlyRatesSnapshotJson = JsonSerializer.Serialize(
            ordered.Select(rate => new NightRateDocument(rate.Night, rate.Amount, rate.RatePlanCode)).ToArray());
    }

    /// <summary>
    /// The per-night rates of the stay, ordered by night: the frozen detail when one was stored,
    /// otherwise the flat <see cref="NightlyRateSnapshot"/> applied to every night (legacy rows,
    /// or an unreadable detail - billing then matches exactly what those stays always billed).
    /// The folio generated at check-in charges these amounts, night by night.
    /// </summary>
    public IReadOnlyList<ReservationNightRate> GetNightlyRates()
    {
        if (!string.IsNullOrWhiteSpace(NightlyRatesSnapshotJson))
        {
            try
            {
                var documents = JsonSerializer.Deserialize<NightRateDocument[]>(NightlyRatesSnapshotJson);

                if (documents is not null && documents.Length == Nights)
                {
                    return documents
                        .Select(document => new ReservationNightRate(document.Night, document.Amount, document.RatePlanCode))
                        .OrderBy(rate => rate.Night)
                        .ToArray();
                }
            }
            catch (JsonException)
            {
                // Fall through to the flat snapshot below.
            }
        }

        var flatRates = new ReservationNightRate[Nights];

        for (var index = 0; index < flatRates.Length; index++)
        {
            flatRates[index] = new ReservationNightRate(
                ArrivalDate.AddDays(index),
                NightlyRateSnapshot,
                RatePlanCodeSnapshot);
        }

        return flatRates;
    }

    /// <summary>Total price of the stay: the sum of its per-night frozen rates.</summary>
    public decimal TotalStayAmount => GetNightlyRates().Sum(rate => rate.Amount);

    /// <summary>Storage shape of one entry of <see cref="NightlyRatesSnapshotJson"/>.</summary>
    private sealed record NightRateDocument(
        [property: JsonPropertyName("night")] DateOnly Night,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("ratePlanCode")] string RatePlanCode);

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
