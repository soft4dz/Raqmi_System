using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

public sealed record DepositResponse(
    Guid Id,
    Guid ReservationId,
    decimal Amount,
    DateOnly DueDate,
    DepositStatus Status,
    DateOnly? PaidDate,
    string? PaymentMethod,
    string? Reference,
    Guid? AppliedToFolioId,
    DateTimeOffset? AppliedAt,
    string? AppliedBy,
    DateOnly? RefundedDate,
    string? ClosingReason,
    string? Notes,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
