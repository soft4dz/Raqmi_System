using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Revenue;

public sealed record DailyRevenueSummaryResponse(
    DateOnly? From,
    DateOnly? To,
    string? HotelUnitCode,
    DailyRevenueStatus? Status,
    int EntryCount,
    int DraftCount,
    int SubmittedCount,
    int ValidatedCount,
    int RejectedCount,
    decimal Accommodation,
    decimal Food,
    decimal Beverage,
    decimal Other,
    decimal Total);
