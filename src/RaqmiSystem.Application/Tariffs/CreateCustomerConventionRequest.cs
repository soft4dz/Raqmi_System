namespace RaqmiSystem.Application.Tariffs;

public sealed record CreateCustomerConventionRequest(
    string CustomerCode,
    string RatePlanCode,
    decimal? DiscountPercent,
    DateOnly FromDate,
    DateOnly ToDate);
