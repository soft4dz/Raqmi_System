using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Revenue;

/// <summary>
/// Une recette telle que renvoyée au client. <see cref="Lines"/> porte le détail par catégorie ;
/// les quatre montants hôteliers restent exposés (dérivés des lignes, voir
/// <see cref="DailyRevenue"/>) pour les clients qui les lisent encore.
/// </summary>
public sealed record DailyRevenueResponse(
    Guid Id,
    DateOnly BusinessDate,
    string HotelUnitCode,
    string? HotelUnitName,
    decimal Accommodation,
    decimal Food,
    decimal Beverage,
    decimal Other,
    decimal Total,
    string? Notes,
    DailyRevenueStatus Status,
    bool CanEdit,
    DateTimeOffset? SubmittedAt,
    string? SubmittedBy,
    DateTimeOffset? ValidatedAt,
    string? ValidatedBy,
    string? RejectionReason,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    IReadOnlyCollection<DailyRevenueLineResponse> Lines);
