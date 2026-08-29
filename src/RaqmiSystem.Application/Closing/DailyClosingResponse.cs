using RaqmiSystem.Domain.Closing;

namespace RaqmiSystem.Application.Closing;

public sealed record DailyClosingResponse(
    Guid Id,
    DateOnly BusinessDate,
    string HotelUnitCode,
    string? HotelUnitName,
    ClosingStatus Status,
    DateTimeOffset ClosedAt,
    string ClosedBy,
    DateTimeOffset? ReopenedAt,
    string? ReopenedBy,
    string? ReopenReason,
    string? Notes,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
