using RaqmiSystem.Domain.Receivables;

namespace RaqmiSystem.Application.Receivables;

public sealed record ReminderResponse(
    Guid Id,
    string CustomerCode,
    string? CustomerName,
    string InvoiceNumber,
    ReminderLevel Level,
    DateOnly SentAt,
    ReminderChannel Channel,
    string? Notes,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
