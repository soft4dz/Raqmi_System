namespace RaqmiSystem.Domain.Billing;

/// <summary>
/// Invoice line, modelled as a child entity with its own table and a required FK to
/// <see cref="Invoice"/> (rather than an EF owned collection): a dedicated entity keeps the
/// snake_case table configuration, named indexes and check constraints explicit and consistent
/// with every other configuration in this repository, and lets lines carry a stable Id that can
/// be referenced from API responses.
/// </summary>
public sealed class InvoiceLine
{
    /// <summary>
    /// Algerian VAT rates in force: exempt, reduced and standard.
    /// </summary>
    public static readonly IReadOnlyCollection<decimal> AllowedVatRates = new[] { 0m, 9m, 19m };

    private InvoiceLine()
    {
    }

    public InvoiceLine(string designation, decimal quantity, decimal unitPrice, decimal vatRate)
    {
        Designation = RequireValue(designation, nameof(designation), 300);
        Quantity = RequireMaxScale(RequireStrictlyPositive(quantity, nameof(quantity)), 3, nameof(quantity));
        UnitPrice = RequireMaxScale(RequirePositiveOrZero(unitPrice, nameof(unitPrice)), 2, nameof(unitPrice));
        VatRate = RequireAllowedVatRate(vatRate, nameof(vatRate));
        LineTotalExclVat = RoundMoney(Quantity * UnitPrice);
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid InvoiceId { get; private set; }

    public int LineNumber { get; private set; }

    public string Designation { get; private set; } = string.Empty;

    public decimal Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal VatRate { get; private set; }

    public decimal LineTotalExclVat { get; private set; }

    public decimal VatAmount => RoundMoney(LineTotalExclVat * VatRate / 100m);

    public decimal LineTotalInclVat => LineTotalExclVat + VatAmount;

    internal void SetLineNumber(int lineNumber)
    {
        LineNumber = lineNumber;
    }

    internal static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Single source of truth for the Algerian VAT rate rule. Exposed because the rate is also
    /// carried outside a line - <c>ApplicationSettings.DefaultVatRate</c> pre-fills new lines with
    /// it, and a default the line constructor would then refuse would be a trap. Both sides must
    /// validate against the very same list, so neither may restate the rule.
    /// </summary>
    public static decimal RequireAllowedVatRate(decimal vatRate, string argumentName)
    {
        if (!AllowedVatRates.Contains(vatRate))
        {
            throw new ArgumentException("VAT rate must be 0, 9 or 19.", argumentName);
        }

        return vatRate;
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

    /// <summary>
    /// The database columns store quantity with 3 decimals and unit price with 2; a value with
    /// more precision would be silently truncated at persistence time and the stored line
    /// total would no longer match the amount the user validated on screen - refuse it upfront.
    /// </summary>
    private static decimal RequireMaxScale(decimal value, int maxScale, string argumentName)
    {
        if (decimal.Round(value, maxScale) != value)
        {
            throw new ArgumentException($"Value cannot have more than {maxScale} decimal places.", argumentName);
        }

        return value;
    }
}
