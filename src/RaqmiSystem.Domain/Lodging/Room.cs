using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// A physical room of one hotel unit. The number is normalized and unique PER UNIT, and the
/// room always belongs to a <see cref="RoomType"/> OF THE SAME UNIT (enforced by a composite
/// foreign key on (hotel_unit_code, room_type_code) in the EF configuration, and re-checked by
/// the service so the refusal carries a readable message rather than a constraint violation).
/// </summary>
public sealed class Room : AuditableEntity
{
    private Room()
    {
    }

    public Room(string hotelUnitCode, string number, string roomTypeCode, string? floor = null, string? notes = null)
    {
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Number = NormalizeNumber(number);
        RoomTypeCode = RoomType.NormalizeCode(roomTypeCode);
        Floor = NormalizeOptional(floor, nameof(floor), 20);
        Notes = NormalizeOptional(notes, nameof(notes), 300);
        IsActive = true;
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    public string Number { get; private set; } = string.Empty;

    public string RoomTypeCode { get; private set; } = string.Empty;

    /// <summary>
    /// Free-form floor label ("RDC", "1", "Mezzanine", ...): a floor is not always a number, so
    /// it is stored as text, purely descriptive.
    /// </summary>
    public string? Floor { get; private set; }

    /// <summary>Free-form housekeeping / maintenance notes about the physical room.</summary>
    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// Moves the room to another type of the SAME unit (the unit itself never changes: a room
    /// is a physical part of its building). The caller must have verified that the target type
    /// exists and is active within the unit.
    /// </summary>
    public void AssignRoomType(string roomTypeCode)
    {
        RoomTypeCode = RoomType.NormalizeCode(roomTypeCode);
    }

    /// <summary>Updates the descriptive fields of the room (floor label and notes).</summary>
    public void UpdateDetails(string? floor, string? notes)
    {
        Floor = NormalizeOptional(floor, nameof(floor), 20);
        Notes = NormalizeOptional(notes, nameof(notes), 300);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public static string NormalizeNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", nameof(value));
        }

        var trimmed = value.Trim();

        if (trimmed.Length > 20)
        {
            throw new ArgumentException("Value cannot exceed 20 characters.", nameof(value));
        }

        return trimmed.ToUpperInvariant();
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
