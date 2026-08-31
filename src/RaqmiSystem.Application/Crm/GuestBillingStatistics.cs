namespace RaqmiSystem.Application.Crm;

/// <summary>
/// What the billing module knows about a guest, summed up. Cancelled invoices are excluded from
/// both figures, and <paramref name="OutstandingInclVat"/> is what has been issued and not yet
/// paid - the number that decides whether a guest is welcomed or chased.
/// </summary>
public sealed record GuestBillingStatistics(
    int InvoiceCount,
    decimal InvoicedInclVat,
    decimal OutstandingInclVat);
