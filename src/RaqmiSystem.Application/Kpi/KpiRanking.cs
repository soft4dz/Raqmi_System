namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Une unite distinguee sur un indicateur, dans l'une des quatre lectures du comparatif. Chaque
/// classement porte l'indicateur sur lequel il porte : "meilleure performance" tout court ne
/// veut rien dire.
/// </summary>
public sealed record KpiRanking(
    KpiRankingKind Kind,
    string KpiCode,
    string KpiName,
    string HotelUnitCode,
    string HotelUnitName,
    decimal? Value,
    decimal? ComparisonValue,
    string Explanation);
