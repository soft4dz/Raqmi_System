using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Housekeeping;

/// <summary>
/// One product of the minibar price list of ONE hotel unit. The code is normalized and unique
/// per unit, on the same footing as <see cref="Lodging.RoomType"/>: two units may both sell a
/// "EAU50", one unit may not define it twice.
///
/// The price is strictly positive because a consumption ends up as a folio line, and a folio
/// line may never carry a zero amount (<see cref="Lodging.FolioCharge"/>). A complimentary item
/// is therefore not a zero-priced product: it is a priced product plus a commercial gesture on
/// the folio, which is the form that leaves a trace of the gesture.
/// </summary>
public sealed class MinibarItem : AuditableEntity
{
    private MinibarItem()
    {
    }

    public MinibarItem(string hotelUnitCode, string code, string label, decimal unitPrice)
    {
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Code = NormalizeCode(code);
        Label = RequireValue(label, nameof(label), 160);
        UnitPrice = RequirePrice(unitPrice, nameof(unitPrice));
        IsActive = true;
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public bool IsActive { get; private set; }

    public void UpdateDetails(string label, decimal unitPrice)
    {
        Label = RequireValue(label, nameof(label), 160);
        UnitPrice = RequirePrice(unitPrice, nameof(unitPrice));
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
}
