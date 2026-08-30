namespace RaqmiSystem.Application.Tariffs;

public sealed record UpdateCustomerConventionRequest(
    string RatePlanCode,
    decimal? DiscountPercent,
    DateOnly FromDate,
    DateOnly ToDate);
