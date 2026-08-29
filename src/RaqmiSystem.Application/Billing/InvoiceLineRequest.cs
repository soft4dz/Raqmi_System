namespace RaqmiSystem.Application.Billing;

public sealed record InvoiceLineRequest(
    string Designation,
    decimal Quantity,
    decimal UnitPrice,
    decimal VatRate);
