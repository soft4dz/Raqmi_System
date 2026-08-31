namespace RaqmiSystem.Application.Crm;

public sealed record UpdateLoyaltyTierRequest(
    string Label,
    int PointsThreshold,
    string? Benefits = null);
