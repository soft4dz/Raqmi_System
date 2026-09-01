using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Un point d'historique : la valeur figee d'un indicateur sur une periode.
///
/// <see cref="FormulaVersion"/> voyage avec le point et n'est pas decoratif : une courbe qui
/// melangerait deux versions de formule mentirait sur une rupture de methode, et personne ne
/// pourrait s'en apercevoir. L'ecran doit marquer la rupture, pas la lisser.
/// </summary>
public sealed record KpiHistoryPoint(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    KpiPeriodGranularity Granularity,
    decimal? Value,
    decimal? Numerator,
    decimal? Denominator,
    KpiQuality Quality,
    KpiSnapshotStatus Status,
    int FormulaVersion,
    DateTimeOffset CalculatedAt,
    DateTimeOffset? ClosedAt,
    string? ClosedBy);
