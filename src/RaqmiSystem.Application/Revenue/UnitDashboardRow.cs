using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Revenue;

public sealed record UnitDashboardRow(
    string HotelUnitCode,
    string HotelUnitName,
    bool HasEntry,
    DailyRevenueStatus? Status,
    decimal? Total,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ValidatedAt);
