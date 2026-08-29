namespace RaqmiSystem.Application.Billing;

public sealed record CreateInvoiceRequest(
    string CustomerCode,
    string HotelUnitCode,
    DateOnly InvoiceDate,
    IReadOnlyCollection<InvoiceLineRequest> Lines);
