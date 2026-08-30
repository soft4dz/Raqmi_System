namespace RaqmiSystem.Application.Tariffs;

public sealed record ResolvedNightlyRate(
    decimal Amount, string RatePlanCode, string? ConventionCustomerCode, decimal? DiscountPercent);
