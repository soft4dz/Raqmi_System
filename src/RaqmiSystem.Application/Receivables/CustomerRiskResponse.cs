using RaqmiSystem.Domain.Receivables;

namespace RaqmiSystem.Application.Receivables;

/// <summary>
/// Credit-risk snapshot of one customer: what is owed, for how long, and how hard it has been
/// chased so far. Computed live from finance.invoices and finance.reminders.
/// </summary>
/// <param name="LastReminderLevel">Level of the most recent reminder, by date sent.</param>
/// <param name="HighestReminderLevel">
/// Highest level ever reached for this customer, which is not always the last one recorded: a
/// formal notice on one invoice is not undone by a first reminder filed later on another.
/// </param>
public sealed record CustomerRiskResponse(
    string CustomerCode,
    string CustomerName,
    bool CustomerIsActive,
    DateOnly AsOfDate,
    string Scope,
    string AgingBasis,
    decimal OutstandingTotal,
    int OutstandingInvoiceCount,
    AgingBucketsResponse Buckets,
    string? OldestOutstandingInvoiceNumber,
    DateOnly? OldestOutstandingInvoiceDate,
    int? OldestOutstandingInvoiceAgeInDays,
    decimal? OldestOutstandingInvoiceAmount,
    int ReminderCount,
    ReminderLevel? LastReminderLevel,
    DateOnly? LastReminderSentAt,
    ReminderLevel? HighestReminderLevel);
