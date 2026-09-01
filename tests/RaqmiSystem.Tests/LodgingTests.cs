using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Tests;

/// <summary>
/// Pure domain coverage of the lodging invariants: normalization, the reservation state
/// machine and its date guards, the overlap rule's boundaries, and the folio sign rules.
/// These hold for every caller, not only the ones that go through the API.
/// </summary>
public sealed class LodgingTests
{
    private static readonly DateOnly Arrival = new(2026, 9, 10);

    private static readonly DateOnly Departure = new(2026, 9, 13);

    private static Reservation CreateBookedReservation(
        DateOnly? arrival = null,
        DateOnly? departure = null,
        decimal rate = 12_000.00m)
    {
        return TestReservations.Create(
            " htl-alger ",
            Guid.NewGuid(),
            " cli-42 ",
            arrival ?? Arrival,
            departure ?? Departure,
            guests: 2,
            nightlyRate: rate,
            ratePlanCode: "STD");
    }

    [Fact]
    public void Room_type_normalizes_codes_and_requires_a_positive_capacity()
    {
        var roomType = new RoomType(" htl-alger ", " dbl ", " Chambre double ", 2);

        Assert.Equal("HTL-ALGER", roomType.HotelUnitCode);
        Assert.Equal("DBL", roomType.Code);
        Assert.Equal("Chambre double", roomType.Label);
        Assert.True(roomType.IsActive);

        Assert.Throws<ArgumentOutOfRangeException>(() => new RoomType("HTL", "DBL", "Double", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => roomType.UpdateDetails("Double", -1));
    }

    [Fact]
    public void Room_normalizes_its_number_and_type_code()
    {
        var room = new Room(" htl-alger ", " 101a ", " dbl ");

        Assert.Equal("HTL-ALGER", room.HotelUnitCode);
        Assert.Equal("101A", room.Number);
        Assert.Equal("DBL", room.RoomTypeCode);
        Assert.True(room.IsActive);
    }

    [Fact]
    public void Reservation_requires_at_least_one_night()
    {
        // Same-day in/out is zero nights; inverted dates are worse. Both must be refused.
        Assert.Throws<ArgumentException>(() => CreateBookedReservation(Arrival, Arrival));
        Assert.Throws<ArgumentException>(() => CreateBookedReservation(Departure, Arrival));

        var reservation = CreateBookedReservation();
        Assert.Equal(3, reservation.Nights);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
    }

    [Fact]
    public void Reservation_refuses_a_rate_with_more_than_two_decimals_or_negative()
    {
        Assert.Throws<ArgumentException>(() => CreateBookedReservation(rate: 100.005m));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateBookedReservation(rate: -1m));
    }

    [Fact]
    public void Overlap_rule_frees_the_departure_night()
    {
        // Same period overlaps itself; a stay ending the day another starts does not.
        Assert.True(Reservation.PeriodsOverlap(Arrival, Departure, Arrival, Departure));
        Assert.True(Reservation.PeriodsOverlap(Arrival, Departure, Arrival.AddDays(1), Departure.AddDays(5)));
        Assert.False(Reservation.PeriodsOverlap(Arrival, Departure, Departure, Departure.AddDays(2)));
        Assert.False(Reservation.PeriodsOverlap(Departure, Departure.AddDays(2), Arrival, Departure));
    }

    [Fact]
    public void Check_in_is_refused_before_the_utc_eve_of_the_arrival_date_and_allowed_from_it()
    {
        var reservation = CreateBookedReservation();

        // Two days before arrival is really too early, timezone tolerance included.
        var exception = Assert.Throws<InvalidOperationException>(
            () => reservation.CheckIn(Arrival.AddDays(-2), "receptionist", DateTimeOffset.UtcNow));

        Assert.Contains("avant sa date d'arrivee", exception.Message);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);

        reservation.CheckIn(Arrival, "receptionist", DateTimeOffset.UtcNow);

        Assert.Equal(ReservationStatus.CheckedIn, reservation.Status);
        Assert.Equal("receptionist", reservation.CheckedInBy);

        // The UTC eve of the arrival date is accepted: a guest east of UTC arriving just after
        // local midnight on the arrival day is still on the previous UTC date.
        var arrivingAfterLocalMidnight = CreateBookedReservation();
        arrivingAfterLocalMidnight.CheckIn(Arrival.AddDays(-1), "receptionist", DateTimeOffset.UtcNow);

        Assert.Equal(ReservationStatus.CheckedIn, arrivingAfterLocalMidnight.Status);
    }

    [Fact]
    public void Check_in_is_refused_once_the_departure_date_has_passed()
    {
        // The departure day itself is still acceptable (a very late arrival on the last
        // morning of the stay), the day after is not: the stay cannot happen anymore.
        var lastMorning = CreateBookedReservation();
        lastMorning.CheckIn(Departure, "receptionist", DateTimeOffset.UtcNow);
        Assert.Equal(ReservationStatus.CheckedIn, lastMorning.Status);

        var stale = CreateBookedReservation();

        var exception = Assert.Throws<InvalidOperationException>(
            () => stale.CheckIn(Departure.AddDays(1), "receptionist", DateTimeOffset.UtcNow));

        Assert.Contains("date de depart est passee", exception.Message);
        Assert.Equal(ReservationStatus.Confirmed, stale.Status);
    }

    [Fact]
    public void Check_out_requires_a_checked_in_reservation()
    {
        var reservation = CreateBookedReservation();

        Assert.Throws<InvalidOperationException>(
            () => reservation.CheckOut("receptionist", DateTimeOffset.UtcNow));

        reservation.CheckIn(Arrival, "receptionist", DateTimeOffset.UtcNow);
        reservation.CheckOut("receptionist", DateTimeOffset.UtcNow);

        Assert.Equal(ReservationStatus.CheckedOut, reservation.Status);

        // Terminal: nothing else may happen to a checked-out stay.
        Assert.Throws<InvalidOperationException>(
            () => reservation.Cancel("Too late.", "receptionist", DateTimeOffset.UtcNow));
        Assert.Throws<InvalidOperationException>(
            () => reservation.CheckIn(Arrival, "receptionist", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Cancellation_requires_a_reason_and_only_applies_to_booked_reservations()
    {
        var reservation = CreateBookedReservation();

        Assert.Throws<ArgumentException>(
            () => reservation.Cancel("   ", "receptionist", DateTimeOffset.UtcNow));

        reservation.Cancel("Guest cancelled by phone.", "receptionist", DateTimeOffset.UtcNow);

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal("Guest cancelled by phone.", reservation.CancelReason);
        Assert.False(reservation.IsBlocking);

        Assert.Throws<InvalidOperationException>(
            () => reservation.Cancel("Again.", "receptionist", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void No_show_is_only_possible_strictly_after_the_arrival_date()
    {
        var reservation = CreateBookedReservation();

        // On the arrival day itself the guest may still arrive: no-show must wait.
        var exception = Assert.Throws<InvalidOperationException>(
            () => reservation.MarkNoShow(Arrival, "receptionist", DateTimeOffset.UtcNow));

        Assert.Contains("date d'arrivee passee", exception.Message);

        reservation.MarkNoShow(Arrival.AddDays(1), "receptionist", DateTimeOffset.UtcNow);

        Assert.Equal(ReservationStatus.NoShow, reservation.Status);
        Assert.False(reservation.IsBlocking);
    }

    [Fact]
    public void Folio_charge_sign_rules_follow_the_kind()
    {
        var date = new DateOnly(2026, 9, 10);

        // Nights and extras are always billed positively.
        Assert.Throws<ArgumentException>(() => new FolioCharge(date, "Nuit", -100m, ChargeKind.Night));
        Assert.Throws<ArgumentException>(() => new FolioCharge(date, "Minibar", -50m, ChargeKind.Extra));

        // Zero is never a line, whatever the kind.
        Assert.Throws<ArgumentException>(() => new FolioCharge(date, "Rien", 0m, ChargeKind.Settlement));

        // More than two decimals would not survive the numeric(18,2) column.
        Assert.Throws<ArgumentException>(() => new FolioCharge(date, "Nuit", 100.005m, ChargeKind.Night));

        // Settlements and adjustments may be negative - that is how a folio settles to zero.
        var settlement = new FolioCharge(date, "Reglement especes", -36_000m, ChargeKind.Settlement, " REC-0042 ");
        Assert.Equal(-36_000m, settlement.Amount);
        Assert.Equal("REC-0042", settlement.Reference);

        var gesture = new FolioCharge(date, "Geste commercial", -1_000m, ChargeKind.Adjustment);
        Assert.Equal(ChargeKind.Adjustment, gesture.Kind);
    }

    [Fact]
    public void Folio_balance_is_the_sum_of_its_lines()
    {
        var folio = new Folio(Guid.NewGuid(), "HTL-ALGER", "F-0001");
        var date = new DateOnly(2026, 9, 10);

        folio.AddCharge(new FolioCharge(date, "Nuit 1", 12_000m, ChargeKind.Night));
        folio.AddCharge(new FolioCharge(date.AddDays(1), "Nuit 2", 12_000m, ChargeKind.Night));
        folio.AddCharge(new FolioCharge(date.AddDays(1), "Minibar", 1_500m, ChargeKind.Extra));

        Assert.Equal(25_500m, folio.Balance);

        folio.AddCharge(new FolioCharge(date.AddDays(2), "Reglement", -25_500m, ChargeKind.Settlement, "REC-0007"));

        Assert.Equal(0m, folio.Balance);

        // Line numbers are allocated in order of arrival, like invoice lines.
        Assert.Equal(new[] { 1, 2, 3, 4 }, folio.Charges.Select(charge => charge.LineNumber).ToArray());
    }
}
