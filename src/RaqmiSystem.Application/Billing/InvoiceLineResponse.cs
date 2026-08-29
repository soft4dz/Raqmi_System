namespace RaqmiSystem.Application.Billing;

public sealed record InvoiceLineResponse(
    Guid Id,
    int LineNumber,
    string Designation,
    decimal Quantity,
    decimal UnitPrice,
    decimal VatRate,
    decimal LineTotalExclVat,
    decimal VatAmount,
    decimal LineTotalInclVat);
