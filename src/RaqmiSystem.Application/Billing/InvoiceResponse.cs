using RaqmiSystem.Domain.Billing;

namespace RaqmiSystem.Application.Billing;

public sealed record InvoiceResponse(
    Guid Id,
    string? Number,
    string CustomerCode,
    string? CustomerName,
    string HotelUnitCode,
    DateOnly InvoiceDate,
    InvoiceStatus Status,
    decimal TotalExclVat,
    decimal TotalVat,
    decimal TotalInclVat,
    IReadOnlyCollection<InvoiceLineResponse> Lines,
    bool CanEdit,
    DateTimeOffset? IssuedAt,
    string? IssuedBy,
    DateTimeOffset? PaidAt,
    string? PaidBy,
    DateTimeOffset? CancelledAt,
    string? CancelledBy,
    string? CancellationReason,
    string? IssuerName,
    string? IssuerNif,
    string? IssuerRc,
    string? IssuerAi,
    string? IssuerNis,
    string? IssuerAddress,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
