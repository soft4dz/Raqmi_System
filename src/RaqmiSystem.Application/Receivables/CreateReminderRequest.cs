using RaqmiSystem.Domain.Receivables;

namespace RaqmiSystem.Application.Receivables;

/// <summary>
/// Records a dunning action that has ALREADY been performed by a human. The system sends
/// nothing; <see cref="SentAt"/> is a declaration, not a schedule.
///
/// The customer is not part of the request: it is read from the chased invoice, so a reminder
/// can never be filed against a customer who is not the one actually invoiced.
/// </summary>
public sealed record CreateReminderRequest(
    string InvoiceNumber,
    ReminderLevel Level,
    DateOnly SentAt,
    ReminderChannel Channel,
    string? Notes = null);
