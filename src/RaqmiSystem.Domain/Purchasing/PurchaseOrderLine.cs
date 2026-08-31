namespace RaqmiSystem.Domain.Purchasing;

/// <summary>
/// Purchase order line, modelled - like <c>InvoiceLine</c> - as a child entity with its own
/// table and a required FK to <see cref="PurchaseOrder"/>: the snake_case configuration, named
/// indexes and check constraints stay explicit, and a line carries a stable Id that the
/// reception workflow references ("receive 4 units against THIS line").
///
/// The designation is FROZEN at capture time: it describes what was ordered, worded as it was
/// ordered, and a later rename of the stock item must not rewrite an order already sent to the
/// supplier. Same scale rules as InvoiceLine: quantities carry at most 3 decimals, monetary
/// values at most 2 (the columns are numeric(18,3) / numeric(18,2), and a value with more
/// precision would be silently truncated at persistence time).
/// </summary>
public sealed class PurchaseOrderLine
{
    private PurchaseOrderLine()
    {
    }

    public PurchaseOrderLine(string itemCode, string designation, decimal quantity, decimal unitPrice)
    {
        ItemCode = NormalizeItemCode(itemCode);
        Designation = RequireValue(designation, nameof(designation), 300);
        Quantity = RequireMaxScale(RequireStrictlyPositive(quantity, nameof(quantity)), 3, nameof(quantity));
        UnitPrice = RequireMaxScale(RequirePositiveOrZero(unitPrice, nameof(unitPrice)), 2, nameof(unitPrice));
        LineTotalExclVat = RoundMoney(Quantity * UnitPrice);
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid PurchaseOrderId { get; private set; }

    public int LineNumber { get; private set; }

    /// <summary>Code of the stock item being purchased (referential owned by the stock module).</summary>
    public string ItemCode { get; private set; } = string.Empty;

    public string Designation { get; private set; } = string.Empty;

    public decimal Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotalExclVat { get; private set; }

    /// <summary>
    /// Cumulative quantity received so far, across every (possibly partial) delivery.
    /// </summary>
    public decimal QuantityReceived { get; private set; }

    public decimal RemainingQuantity => Quantity - QuantityReceived;

    public bool IsFullyReceived => RemainingQuantity == 0m;

    internal void SetLineNumber(int lineNumber)
    {
        LineNumber = lineNumber;
    }

    /// <summary>
    /// Adds one delivery to the cumulative received quantity. Guarded by the aggregate
    /// (<see cref="PurchaseOrder.RegisterReceipt"/>) for the order-level rules; the line itself
    /// enforces the per-line invariant: never receive more than what remains to be received.
    /// </summary>
    internal void RegisterReceipt(decimal quantityReceivedNow)
    {
        RequireMaxScale(
            RequireStrictlyPositive(quantityReceivedNow, nameof(quantityReceivedNow)),
            3,
            nameof(quantityReceivedNow));

        if (quantityReceivedNow > RemainingQuantity)
        {
            throw new InvalidOperationException(
                $"Line {LineNumber} ({ItemCode}): the received quantity ({quantityReceivedNow}) exceeds the " +
                $"remaining quantity to receive ({RemainingQuantity}).");
        }

        QuantityReceived += quantityReceivedNow;
    }

    public static string NormalizeItemCode(string value)
    {
        return RequireValue(value, nameof(value), 40).ToUpperInvariant();
    }

    internal static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    internal static decimal RequireMaxScale(decimal value, int maxScale, string argumentName)
    {
        if (decimal.Round(value, maxScale) != value)
        {
            throw new ArgumentException($"Value cannot have more than {maxScale} decimal places.", argumentName);
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

    private static decimal RequireStrictlyPositive(decimal value, string argumentName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "Value must be strictly positive.");
        }

        return value;
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
