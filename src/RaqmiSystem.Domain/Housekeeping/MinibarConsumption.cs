using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Housekeeping;

/// <summary>
/// What a guest took from the minibar of their room, recorded by housekeeping and billed on the
/// stay's folio. Every commercial field is a SNAPSHOT of the price list at recording time (code,
/// label and unit price), for the same reason the nightly rate is frozen into a reservation: the
/// day the price list is edited must not silently rewrite what a guest was charged last week.
///
/// The row is the HOUSEKEEPING trace of the consumption; the money lives on the folio line it
/// produced. The two are written in the SAME database transaction (see HousekeepingService), and
/// the folio line carries this row's <see cref="AuditableEntity.Id"/> as its reference, so a
/// disputed line on a guest's bill can always be traced back to who recorded it and when.
/// </summary>
public sealed class MinibarConsumption : AuditableEntity
{
    private MinibarConsumption()
    {
    }

    public MinibarConsumption(
        string hotelUnitCode,
        Guid roomId,
        string roomNumber,
        Guid reservationId,
        string itemCode,
        string itemLabel,
        decimal unitPrice,
        int quantity,
        DateOnly consumedOn,
        string? notes = null)
    {
        if (roomId == Guid.Empty)
        {
            throw new ArgumentException("Room id is required.", nameof(roomId));
        }

        if (reservationId == Guid.Empty)
        {
            throw new ArgumentException("Reservation id is required.", nameof(reservationId));
        }

        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        RoomId = roomId;
        RoomNumber = Room.NormalizeNumber(roomNumber);
        ReservationId = reservationId;
        ItemCode = MinibarItem.NormalizeCode(itemCode);
        ItemLabel = RequireValue(itemLabel, nameof(itemLabel), 160);
        UnitPrice = RequirePrice(unitPrice, nameof(unitPrice));
        Quantity = RequireStrictlyPositive(quantity, nameof(quantity));
        ConsumedOn = consumedOn;
        Notes = NormalizeOptional(notes, nameof(notes), 300);

        // Stored rather than computed on read: it is the amount actually posted to the folio,
        // and a stored total cannot drift from it when the rounding rule is revisited.
        TotalAmount = decimal.Round(UnitPrice * Quantity, 2);
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    public Guid RoomId { get; private set; }

    /// <summary>Room number at recording time. Display and history only; <see cref="RoomId"/> is the identity.</summary>
    public string RoomNumber { get; private set; } = string.Empty;

    /// <summary>The checked-in stay that was billed. A minibar line has no meaning without one.</summary>
    public Guid ReservationId { get; private set; }

    public string ItemCode { get; private set; } = string.Empty;

    public string ItemLabel { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal TotalAmount { get; private set; }

    public DateOnly ConsumedOn { get; private set; }

    public string? Notes { get; private set; }

    private static decimal RequirePrice(decimal value, string argumentName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                argumentName,
                value,
                "The unit price must be strictly positive.");
        }

        if (decimal.Round(value, 2) != value)
        {
            throw new ArgumentException("Value cannot have more than 2 decimal places.", argumentName);
        }

        return value;
    }

    private static int RequireStrictlyPositive(int value, string argumentName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "Value must be strictly positive.");
        }

        return value;
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
