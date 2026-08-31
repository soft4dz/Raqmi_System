using RaqmiSystem.Domain.Billing;

namespace RaqmiSystem.Domain.Mice;

/// <summary>
/// One priced item of an event: room hire, coffee break, lunch at a price per head, video
/// projector. Together the lines ARE the quote ("devis"), and they are what the invoice is built
/// from when the event is billed.
///
/// The VAT rate is validated by <see cref="InvoiceLine.RequireAllowedVatRate"/>, the billing
/// module's own rule, rather than by a copy of the allowed rates kept here: a quote that could
/// carry a rate the invoice then refuses would strand the event at billing time.
/// </summary>
public sealed class EventBookingLine
{
    public const int DesignationMaxLength = 200;

    private EventBookingLine()
    {
    }

    public EventBookingLine(string designation, decimal quantity, decimal unitPrice, decimal vatRate)
    {
        Designation = RequireDesignation(designation);
        Quantity = RequireQuantity(quantity);
        UnitPrice = RequireUnitPrice(unitPrice);
        VatRate = InvoiceLine.RequireAllowedVatRate(vatRate, nameof(vatRate));
    }

    /// <summary>
    /// Self-assigned identifier: the EF configuration MUST declare ValueGeneratedNever(),
    /// otherwise EF marks a new line Modified instead of Added.
    /// </summary>
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid EventBookingId { get; private set; }

    public int LineNumber { get; private set; }

    public string Designation { get; private set; } = string.Empty;

    public decimal Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal VatRate { get; private set; }

    public decimal LineTotalExclVat => RoundMoney(Quantity * UnitPrice);

    public decimal VatAmount => RoundMoney(LineTotalExclVat * VatRate / 100m);

    public decimal LineTotalInclVat => LineTotalExclVat + VatAmount;

    public void SetLineNumber(int lineNumber)
    {
        if (lineNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "The line number must be strictly positive.");
        }

        LineNumber = lineNumber;
    }

    private static string RequireDesignation(string designation)
    {
        if (string.IsNullOrWhiteSpace(designation))
        {
            throw new ArgumentException("The designation is required.", nameof(designation));
        }

        var trimmed = designation.Trim();

        if (trimmed.Length > DesignationMaxLength)
        {
            throw new ArgumentException(
                $"The designation cannot exceed {DesignationMaxLength} characters.",
                nameof(designation));
        }

        return trimmed;
    }

    private static decimal RequireQuantity(decimal quantity)
    {
        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "The quantity must be strictly positive.");
        }

        return decimal.Round(quantity, 2, MidpointRounding.AwayFromZero);
    }

    // Zero is allowed: a complimentary item offered to close a deal is a real line of a quote,
    // and hiding it would make the document say less than the negotiation did.
    private static decimal RequireUnitPrice(decimal unitPrice)
    {
        if (unitPrice < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "The unit price cannot be negative.");
        }

        return RoundMoney(unitPrice);
    }

    private static decimal RoundMoney(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
