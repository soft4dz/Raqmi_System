namespace RaqmiSystem.Application.Tariffs;

public sealed record CustomerConventionResponse(
    Guid Id,
    string CustomerCode,
    string? CustomerName,
    string RatePlanCode,
    decimal? DiscountPercent,
    DateOnly FromDate,
    DateOnly ToDate,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
