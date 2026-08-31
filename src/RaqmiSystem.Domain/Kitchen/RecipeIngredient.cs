namespace RaqmiSystem.Domain.Kitchen;

/// <summary>
/// Ingredient line of a recipe sheet, modelled as a child entity with its own table and a
/// required FK to <see cref="RecipeSheet"/> (same design as InvoiceLine): a dedicated entity
/// keeps the snake_case table configuration, named indexes and check constraints explicit,
/// and lets lines carry a stable Id that can be referenced from API responses.
///
/// <c>ItemCode</c> references an inventory item of the stock module by its code. The
/// reference is deliberately NOT a database foreign key: the inventory tables belong to
/// another module and the cross-module contract is the Application-level
/// <c>IStockCostProvider</c>, through which the service verifies the item's existence at
/// save time.
/// </summary>
public sealed class RecipeIngredient
{
    private RecipeIngredient()
    {
    }

    public RecipeIngredient(string itemCode, decimal quantity, string? notes = null)
    {
        ItemCode = NormalizeCode(itemCode);
        Quantity = RequireMaxScale(RequireStrictlyPositive(quantity, nameof(quantity)), 3, nameof(quantity));
        Notes = NormalizeOptional(notes, nameof(notes), 300);
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid RecipeSheetId { get; private set; }

    public int LineNumber { get; private set; }

    /// <summary>Code of the stock item, normalized to uppercase like every code in this repository.</summary>
    public string ItemCode { get; private set; } = string.Empty;

    /// <summary>Quantity needed for the recipe's full yield, at most 3 decimal places (numeric(18,3)).</summary>
    public decimal Quantity { get; private set; }

    public string? Notes { get; private set; }

    internal void SetLineNumber(int lineNumber)
    {
        LineNumber = lineNumber;
    }

    private static string NormalizeCode(string itemCode)
    {
        if (string.IsNullOrWhiteSpace(itemCode))
        {
            throw new ArgumentException("Item code is required.", nameof(itemCode));
        }

        var normalized = itemCode.Trim().ToUpperInvariant();

        if (normalized.Length > 40)
        {
            throw new ArgumentException("Item code cannot exceed 40 characters.", nameof(itemCode));
        }

        return normalized;
    }

    private static decimal RequireStrictlyPositive(decimal value, string argumentName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "Value must be strictly positive.");
        }

        return value;
    }

    /// <summary>
    /// Same rule as InvoiceLine.RequireMaxScale (restated here because that helper is private
    /// to the billing aggregate): the database column stores the quantity with 3 decimals, so
    /// a value with more precision would be silently truncated at persistence time and the
    /// stored recipe would no longer match what the user validated on screen - refuse it upfront.
    /// </summary>
    private static decimal RequireMaxScale(decimal value, int maxScale, string argumentName)
    {
        if (decimal.Round(value, maxScale) != value)
        {
            throw new ArgumentException($"Value cannot have more than {maxScale} decimal places.", argumentName);
        }

        return value;
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
