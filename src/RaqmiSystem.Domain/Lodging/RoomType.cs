using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// A category of rooms within one hotel unit (double room, suite, bungalow, ...). The code is
/// normalized and unique PER UNIT: two units may both have a "DBL" type, one unit may not have
/// two. Capacity is the maximum number of guests a room of this type can host, which caps
/// <see cref="Reservation.GuestCount"/> at booking time.
/// </summary>
public sealed class RoomType : AuditableEntity
{
    private RoomType()
    {
    }

    public RoomType(string hotelUnitCode, string code, string label, int capacity, string? description = null)
    {
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Code = NormalizeCode(code);
        Label = RequireValue(label, nameof(label), 160);
        Capacity = RequireStrictlyPositive(capacity, nameof(capacity));
        Description = NormalizeOptional(description, nameof(description), 300);
        IsActive = true;
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public int Capacity { get; private set; }

    /// <summary>Free-form commercial description of the type, for the setup screen.</summary>
    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public void UpdateDetails(string label, int capacity, string? description = null)
    {
        Label = RequireValue(label, nameof(label), 160);
        Capacity = RequireStrictlyPositive(capacity, nameof(capacity));
        Description = NormalizeOptional(description, nameof(description), 300);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public static string NormalizeCode(string value)
    {
        return RequireValue(value, nameof(value), 40).ToUpperInvariant();
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

    private static int RequireStrictlyPositive(int value, string argumentName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "Value must be strictly positive.");
        }

        return value;
    }
}
