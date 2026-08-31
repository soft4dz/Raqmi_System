namespace RaqmiSystem.Application.Crm;

public sealed record CreateLoyaltyTierRequest(
    string Code,
    string Label,
    int PointsThreshold,
    string? Benefits = null);
