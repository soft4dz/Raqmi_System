using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>Une regle de seuils configuree, telle que l'API la rend.</summary>
public sealed record KpiThresholdResponse(
    Guid Id,
    string KpiCode,
    string KpiName,
    KpiUnit Unit,
    KpiPolarity Polarity,
    string? HotelUnitCode,
    decimal? FavorableThreshold,
    decimal? CriticalThreshold,
    decimal? TargetValue,
    string? OwnerRole,
    string? Notes,
    bool IsActive);
