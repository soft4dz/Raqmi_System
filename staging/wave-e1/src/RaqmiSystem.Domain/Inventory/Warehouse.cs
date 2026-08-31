using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Inventory;

/// <summary>
/// Storage location (magasin) attached to a hotel unit. A warehouse only scopes the movement
/// registry: it holds no stock figure of its own - the stock of an item in a warehouse is,
/// by construction, the sum of the movements recorded against the pair (see
/// <see cref="StockMovement"/>).
/// </summary>
public sealed class Warehouse : AuditableEntity
{
    private Warehouse()
    {
    }

    public Warehouse(string code, string label, string hotelUnitCode)
    {
        Code = NormalizeCode(code);
        Label = RequireValue(label, nameof(label), 160);
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public string HotelUnitCode { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public void UpdateDetails(string label, string hotelUnitCode)
    {
        Label = RequireValue(label, nameof(label), 160);
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
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
}
