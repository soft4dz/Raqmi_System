using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Un indicateur tel qu'il est rendu par l'API : sa fiche d'identite, sa valeur, ses trois
/// references (annee precedente, budget, objectif), sa tendance, son verdict de sante et l'etat
/// de sa donnee.
///
/// TOUT EST DANS LA MEME REPONSE, deliberement : un ecran ne doit jamais avoir a rappeler l'API
/// pour savoir ce que compte un chiffre ou pourquoi il est vide. <see cref="Formula"/> et
/// <see cref="SourceDetail"/> voyagent avec la valeur, ce qui rend le chiffre discutable plutot
/// qu'a croire sur parole - et permet au poste client d'afficher la formule en infobulle sans
/// jamais la reecrire de son cote.
/// </summary>
public sealed record KpiMeasureResponse(
    string Code,
    string Name,
    string ShortName,
    KpiCategory Category,
    string Description,
    string Formula,
    KpiUnit Unit,
    KpiPolarity Polarity,
    KpiAggregation Aggregation,
    KpiScopeLevel ScopeLevel,
    KpiAvailability Availability,
    KpiSourceModule SourceModule,
    string SourceDetail,
    int FormulaVersion,
    string? HotelUnitCode,
    string? HotelUnitName,
    decimal? Value,
    decimal? Numerator,
    decimal? Denominator,
    KpiQuality Quality,
    IReadOnlyCollection<string> MissingData,
    decimal? PreviousValue,
    decimal? PreviousVarianceAmount,
    decimal? PreviousVariancePercent,
    decimal? BudgetValue,
    decimal? BudgetVarianceAmount,
    decimal? BudgetVariancePercent,
    decimal? TargetValue,
    decimal? TargetVarianceAmount,
    decimal? TargetVariancePercent,
    KpiTrend Trend,
    KpiHealth Health,
    decimal? FavorableThreshold,
    decimal? CriticalThreshold,
    string? OwnerRole,
    KpiSnapshotStatus? SnapshotStatus,
    decimal? SnapshotValue);
