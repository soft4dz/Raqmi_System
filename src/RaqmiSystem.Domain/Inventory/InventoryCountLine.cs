namespace RaqmiSystem.Domain.Inventory;

/// <summary>
/// One counted item of a physical inventory. A child entity with its own table and a required
/// FK to <see cref="InventoryCount"/> (not an EF owned collection), for the same reasons as
/// InvoiceLine: explicit snake_case configuration, named indexes, and a stable Id usable from
/// API responses.
/// </summary>
public sealed class InventoryCountLine
{
    private InventoryCountLine()
    {
    }

    public InventoryCountLine(string itemCode, decimal countedQuantity)
    {
        ItemCode = StockItem.NormalizeCode(itemCode);
        CountedQuantity = RequireCountedQuantity(countedQuantity);
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid InventoryCountId { get; private set; }

    public int LineNumber { get; private set; }

    public string ItemCode { get; private set; } = string.Empty;

    /// <summary>Physical quantity found on the shelf. Zero is a meaningful count ("nothing left").</summary>
    public decimal CountedQuantity { get; private set; }

    internal void SetLineNumber(int lineNumber)
    {
        LineNumber = lineNumber;
    }

    /// <summary>
    /// Counted quantities follow the module's quantity rule: never negative (you cannot count
    /// less than nothing) and at most 3 decimal places (numeric(18,3)).
    /// </summary>
    private static decimal RequireCountedQuantity(decimal value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Counted quantity cannot be negative.");
        }

        if (decimal.Round(value, 3) != value)
        {
            throw new ArgumentException("Counted quantity cannot have more than 3 decimal places.", nameof(value));
        }

        return value;
    }
}
