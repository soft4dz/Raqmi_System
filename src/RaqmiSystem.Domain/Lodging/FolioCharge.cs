namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// One line of a <see cref="Folio"/>. Modelled as a child entity with its own table and a
/// required FK (same rationale as InvoiceLine): explicit snake_case configuration, named
/// constraints, and a stable Id that API responses can reference.
///
/// Sign rule: a NEGATIVE amount is only legitimate on a <see cref="ChargeKind.Settlement"/>
/// (a payment applied to the folio) or a <see cref="ChargeKind.Adjustment"/> (commercial
/// gesture); a night or an extra is always billed positively. Zero is never a line.
/// </summary>
public sealed class FolioCharge
{
    private FolioCharge()
    {
    }

    public FolioCharge(DateOnly chargeDate, string label, decimal amount, ChargeKind kind, string? reference = null)
    {
        ChargeDate = chargeDate;
        Label = RequireValue(label, nameof(label), 300);
        Kind = kind;
        Amount = RequireAmount(amount, kind);
        Reference = NormalizeReference(reference);
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid FolioId { get; private set; }

    public int LineNumber { get; private set; }

    public DateOnly ChargeDate { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public ChargeKind Kind { get; private set; }

    /// <summary>
    /// Free reference, typically the treasury receipt number a Settlement line mirrors.
    /// </summary>
    public string? Reference { get; private set; }

    internal void SetLineNumber(int lineNumber)
    {
        LineNumber = lineNumber;
    }

    private static decimal RequireAmount(decimal value, ChargeKind kind)
    {
        if (value == 0)
        {
            throw new ArgumentException("A folio line cannot carry a zero amount.", nameof(value));
        }

        if (decimal.Round(value, 2) != value)
        {
            throw new ArgumentException("Value cannot have more than 2 decimal places.", nameof(value));
        }

        if (value < 0 && kind is not (ChargeKind.Settlement or ChargeKind.Adjustment))
        {
            throw new ArgumentException(
                "Only settlement or adjustment lines may carry a negative amount.",
                nameof(value));
        }

        return value;
    }

    private static string? NormalizeReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var trimmed = reference.Trim();

        if (trimmed.Length > 100)
        {
            throw new ArgumentException("Value cannot exceed 100 characters.", nameof(reference));
        }

        return trimmed;
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
