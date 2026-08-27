using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Revenue;

public sealed record DailyRevenueResponse(
    Guid Id,
    DateOnly BusinessDate,
    string HotelUnitCode,
    string? HotelUnitName,
    decimal Accommodation,
    decimal Food,
    decimal Beverage,
    decimal Other,
    decimal Total,
    string? Notes,
    DailyRevenueStatus Status,
    bool CanEdit,
    DateTimeOffset? SubmittedAt,
    string? SubmittedBy,
    DateTimeOffset? ValidatedAt,
    string? ValidatedBy,
    string? RejectionReason,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
