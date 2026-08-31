using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Inventory;

/// <summary>
/// Stock item (article). Carries the identification and the alert threshold only: the item
/// deliberately has NO quantity column - current stock is always the sum of the movement
/// registry (see <see cref="StockMovement"/>), so it can never drift out of sync.
/// </summary>
public sealed class StockItem : AuditableEntity
{
    private StockItem()
    {
    }

    public StockItem(
        string code,
        string designation,
        string unitOfMeasure,
        StockItemCategory category,
        decimal minimumQuantity = 0m)
    {
        Code = NormalizeCode(code);
        Designation = RequireValue(designation, nameof(designation), 200);
        UnitOfMeasure = RequireValue(unitOfMeasure, nameof(unitOfMeasure), 20);
        Category = RequireDefinedCategory(category);
        MinimumQuantity = RequireQuantityThreshold(minimumQuantity);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Designation { get; private set; } = string.Empty;

    /// <summary>Free short unit label chosen by the establishment: kg, L, piece...</summary>
    public string UnitOfMeasure { get; private set; } = string.Empty;

    public StockItemCategory Category { get; private set; } = StockItemCategory.Autre;

    /// <summary>
    /// Optional alert threshold: 0 means "no alert". An item whose current stock in a
    /// warehouse falls strictly below this quantity shows up in the low-stock report.
    /// </summary>
    public decimal MinimumQuantity { get; private set; }

    public bool IsActive { get; private set; }

    public void UpdateDetails(
        string designation,
        string unitOfMeasure,
        StockItemCategory category,
        decimal minimumQuantity)
    {
        Designation = RequireValue(designation, nameof(designation), 200);
        UnitOfMeasure = RequireValue(unitOfMeasure, nameof(unitOfMeasure), 20);
        Category = RequireDefinedCategory(category);
        MinimumQuantity = RequireQuantityThreshold(minimumQuantity);
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

    private static StockItemCategory RequireDefinedCategory(StockItemCategory category)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentException("Item category is not valid.", nameof(category));
        }

        return category;
    }

    /// <summary>
    /// Same precision rule as every quantity of the module: at most 3 decimal places
    /// (the columns are numeric(18,3)), never negative.
    /// </summary>
    private static decimal RequireQuantityThreshold(decimal value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Minimum quantity cannot be negative.");
        }

        if (decimal.Round(value, 3) != value)
        {
            throw new ArgumentException("Minimum quantity cannot have more than 3 decimal places.", nameof(value));
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
