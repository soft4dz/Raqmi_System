namespace RaqmiSystem.Application.Receivables;

/// <summary>
/// The aged trial balance of receivables. Nothing here is stored: the report is recomputed from
/// finance.invoices on every call.
///
/// <see cref="Scope"/> and <see cref="AgingBasis"/> are part of the payload on purpose. A number
/// like "3 400 000 over 90 days" is meaningless unless the reader knows exactly which invoices
/// were counted and from which date they were aged, and this module makes two decisions the
/// reader cannot guess (issued-only, and aging from the invoice date for lack of a due date on
/// the Invoice aggregate). Shipping them with the figures keeps a screen, an export or a printed
/// report from silently losing the caveat.
/// </summary>
public sealed record AgingBalanceResponse(
    DateOnly AsOfDate,
    string Scope,
    string AgingBasis,
    IReadOnlyCollection<CustomerAgingResponse> Customers,
    AgingBucketsResponse Total);
