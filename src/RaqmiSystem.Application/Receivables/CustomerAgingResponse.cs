namespace RaqmiSystem.Application.Receivables;

/// <summary>
/// One line of the aged balance: everything a single customer still owes, split by age.
/// </summary>
public sealed record CustomerAgingResponse(
    string CustomerCode,
    string? CustomerName,
    int InvoiceCount,
    DateOnly OldestInvoiceDate,
    int OldestInvoiceAgeInDays,
    AgingBucketsResponse Buckets);
