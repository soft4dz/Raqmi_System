using RaqmiSystem.Domain.Purchasing;

namespace RaqmiSystem.Application.Purchasing;

/// <summary>
/// Full projection of a purchase order. The reception progress
/// (<paramref name="TotalQuantityOrdered"/> / <paramref name="TotalQuantityReceived"/>) is
/// computed SERVER-side so every screen renders the same figure ("12/20 recus") without
/// re-deriving it locally.
/// </summary>
public sealed record PurchaseOrderResponse(
    Guid Id,
    string? Number,
    string SupplierCode,
    string? SupplierName,
    string WarehouseCode,
    DateOnly OrderDate,
    PurchaseOrderStatus Status,
    decimal TotalExclVat,
    decimal TotalQuantityOrdered,
    decimal TotalQuantityReceived,
    IReadOnlyCollection<PurchaseOrderLineResponse> Lines,
    bool CanEdit,
    bool CanReceive,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    DateTimeOffset? CancelledAt,
    string? CancelledBy,
    string? CancellationReason,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
