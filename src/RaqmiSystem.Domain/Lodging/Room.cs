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

    public Room(string hotelUnitCode, string number, string roomTypeCode)
    {
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Number = NormalizeNumber(number);
        RoomTypeCode = RoomType.NormalizeCode(roomTypeCode);
        IsActive = true;
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    public string Number { get; private set; } = string.Empty;

    public string RoomTypeCode { get; private set; } = string.Empty;

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
}
