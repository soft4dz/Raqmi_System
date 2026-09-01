using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Le comparatif inter-unites : une ligne par etablissement, les memes colonnes pour tous, plus
/// les classements indicateur par indicateur.
///
/// Les colonnes sont des TAUX et des RATIOS PAR CHAMBRE, pas des volumes bruts : comparer les
/// chiffres d'affaires absolus de deux hotels revient a les classer par nombre de chambres, ce
/// que personne n'a besoin d'un tableau de bord pour savoir. Le chiffre d'affaires figure
/// neanmoins dans les colonnes, comme grandeur de contexte, jamais comme critere de classement
/// de performance.
/// </summary>
public sealed record KpiComparisonResponse(
    DateOnly From,
    DateOnly To,
    KpiPeriodGranularity Granularity,
    DateOnly PreviousFrom,
    DateOnly PreviousTo,
    IReadOnlyCollection<string> Codes,
    IReadOnlyCollection<KpiComparisonRow> Rows,
    IReadOnlyCollection<KpiRanking> Rankings,
    DateTimeOffset CalculatedAt,
    KpiBasis Basis);
