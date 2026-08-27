namespace RaqmiSystem.Application.Revenue;

public sealed record RevenueSummary(
    decimal Accommodation,
    decimal Food,
    decimal Beverage,
    decimal Other,
    decimal Total);
