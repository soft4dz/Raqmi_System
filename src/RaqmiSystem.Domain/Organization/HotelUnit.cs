using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Organization;

public sealed class HotelUnit : AuditableEntity
{
    private HotelUnit()
    {
    }

    public HotelUnit(string code, string name, HotelUnitType unitType, int displayOrder = 0)
    {
        Code = NormalizeCode(code);
        Name = RequireValue(name, nameof(name), 160);
        UnitType = unitType;
        DisplayOrder = RequirePositiveOrZero(displayOrder, nameof(displayOrder));
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public HotelUnitType UnitType { get; private set; } = HotelUnitType.Hotel;

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    public void UpdateDetails(string name, HotelUnitType unitType, int displayOrder)
    {
        Name = RequireValue(name, nameof(name), 160);
        UnitType = unitType;
        DisplayOrder = RequirePositiveOrZero(displayOrder, nameof(displayOrder));
    }

    public void Rename(string name)
    {
        Name = RequireValue(name, nameof(name), 160);
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

    private static int RequirePositiveOrZero(int value, string argumentName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "Value cannot be negative.");
        }

        return value;
    }
}
