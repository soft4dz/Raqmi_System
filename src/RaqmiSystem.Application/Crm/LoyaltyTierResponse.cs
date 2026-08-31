namespace RaqmiSystem.Application.Crm;

public sealed record LoyaltyTierResponse(
    Guid Id,
    string Code,
    string Label,
    int PointsThreshold,
    string? Benefits,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
