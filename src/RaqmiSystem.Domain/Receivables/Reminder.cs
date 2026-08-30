using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Receivables;

/// <summary>
/// Trace of a dunning action performed on one issued invoice.
///
/// This is a RECORD OF WHAT A HUMAN DID, not an outbound message: the application sends
/// nothing. There is no mail, SMS or postal infrastructure anywhere in this repository, and
/// this module deliberately does not pretend otherwise - <see cref="SentAt"/> is the day the
/// operator actually called, e-mailed or posted the letter, declared after the fact.
///
/// Two invariants are enforced here, in the entity: the escalation level and the channel must
/// be defined enum members, and an invoice number is required. The two invariants that need
/// the database - the invoice must exist and be Issued, and the same level cannot be recorded
/// twice for the same invoice - live in ReceivablesService, the second one being additionally
/// backed by the unique index ux_reminders_invoice_number_level.
/// </summary>
public sealed class Reminder : AuditableEntity
{
    /// <summary>Mirrors the length of finance.invoices.number ("FAC-{year}-{sequence:D6}").</summary>
    public const int InvoiceNumberMaxLength = 30;

    private Reminder()
    {
    }

    public Reminder(
        string customerCode,
        string invoiceNumber,
        ReminderLevel level,
        DateOnly sentAt,
        ReminderChannel channel,
        string? notes = null)
    {
        CustomerCode = Customer.NormalizeCode(customerCode);
        InvoiceNumber = NormalizeInvoiceNumber(invoiceNumber);
        Level = RequireDefined(level, nameof(level));
        Channel = RequireDefined(channel, nameof(channel));
        SentAt = sentAt;
        Notes = NormalizeOptional(notes, nameof(notes), 1000);
    }

    public string CustomerCode { get; private set; } = string.Empty;

    /// <summary>
    /// The definitive number of the invoice being chased. Invoices are referenced by their legal
    /// number rather than by their surrogate id because that is the identifier the customer and
    /// the operator both use; see ReminderConfiguration for why this is not a foreign key.
    /// </summary>
    public string InvoiceNumber { get; private set; } = string.Empty;

    public ReminderLevel Level { get; private set; } = ReminderLevel.First;

    /// <summary>Business day on which the action was actually carried out.</summary>
    public DateOnly SentAt { get; private set; }

    public ReminderChannel Channel { get; private set; } = ReminderChannel.Phone;

    public string? Notes { get; private set; }

    public static string NormalizeInvoiceNumber(string invoiceNumber)
    {
        return RequireValue(invoiceNumber, nameof(invoiceNumber), InvoiceNumberMaxLength).ToUpperInvariant();
    }

    private static TEnum RequireDefined<TEnum>(TEnum value, string argumentName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentException("Value is not a valid option.", argumentName);
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
