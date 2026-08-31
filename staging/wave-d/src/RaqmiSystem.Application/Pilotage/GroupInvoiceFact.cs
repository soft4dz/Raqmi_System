using RaqmiSystem.Domain.Billing;

namespace RaqmiSystem.Application.Pilotage;

/// <summary>
/// One invoice dated on or before the period's end, with its status: the calculator applies
/// the receivables module's rule (only Issued invoices are outstanding, aged from the invoice
/// date because the system holds no due dates) itself, in pure code.
/// </summary>
public sealed record GroupInvoiceFact(
    string HotelUnitCode,
    DateOnly InvoiceDate,
    decimal TotalInclVat,
    InvoiceStatus Status);
