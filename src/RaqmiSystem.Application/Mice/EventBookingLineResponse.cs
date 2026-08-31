namespace RaqmiSystem.Application.Mice;

public sealed record EventBookingLineResponse(
    Guid Id,
    int LineNumber,
    string Designation,
    decimal Quantity,
    decimal UnitPrice,
    decimal VatRate,
    decimal LineTotalExclVat,
    decimal VatAmount,
    decimal LineTotalInclVat);
