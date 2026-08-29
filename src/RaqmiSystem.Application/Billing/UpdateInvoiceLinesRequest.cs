namespace RaqmiSystem.Application.Billing;

public sealed record UpdateInvoiceLinesRequest(
    IReadOnlyCollection<InvoiceLineRequest> Lines);
