using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

public sealed record FolioResponse(
    Guid Id,
    Guid ReservationId,
    decimal Balance,
    IReadOnlyCollection<FolioChargeResponse> Charges,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    string Number = "",
    FolioKind Kind = FolioKind.Guest,
    FolioStatus Status = FolioStatus.Open,
    string? BillToCustomerCode = null,
    string? Label = null,
    decimal TotalCharges = 0m,
    decimal TotalSettlements = 0m,
    DateTimeOffset? ClosedAt = null,
    string? ClosedBy = null,
    Guid? InvoiceId = null);
