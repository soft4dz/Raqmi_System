namespace RaqmiSystem.Application.Mice;

/// <summary>
/// One priced item of the quote. <paramref name="VatRate"/> must be one of the rates the billing
/// module accepts (0, 9, 19): a quote carrying a rate the invoice would refuse could never be
/// billed.
/// </summary>
public sealed record EventBookingLineRequest(
    string Designation,
    decimal Quantity,
    decimal UnitPrice,
    decimal VatRate);
