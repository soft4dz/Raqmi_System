using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Revenue;

public sealed class DailyRevenue : AuditableEntity
{
    private DailyRevenue()
    {
    }

    public DailyRevenue(
        DateOnly businessDate,
        string hotelUnitCode,
        decimal accommodation,
        decimal food,
        decimal beverage,
        decimal other)
    {
        BusinessDate = businessDate;
        HotelUnitCode = RequireValue(hotelUnitCode, nameof(hotelUnitCode));
        Accommodation = RequirePositiveOrZero(accommodation, nameof(accommodation));
        Food = RequirePositiveOrZero(food, nameof(food));
        Beverage = RequirePositiveOrZero(beverage, nameof(beverage));
        Other = RequirePositiveOrZero(other, nameof(other));
    }

    public DateOnly BusinessDate { get; private set; }

    public string HotelUnitCode { get; private set; } = string.Empty;

    public decimal Accommodation { get; private set; }

    public decimal Food { get; private set; }

    public decimal Beverage { get; private set; }

    public decimal Other { get; private set; }

    public decimal Total => Accommodation + Food + Beverage + Other;

    private static string RequireValue(string value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        return value.Trim();
    }

    private static decimal RequirePositiveOrZero(decimal value, string argumentName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "Value cannot be negative.");
        }

        return value;
    }
}
