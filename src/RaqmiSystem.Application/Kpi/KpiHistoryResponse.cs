using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// L'historique d'un indicateur sur un perimetre : les instantanes deja poses, du plus ancien
/// au plus recent.
///
/// L'API ne calcule RIEN ici : elle rend ce qui a ete conserve. Recalculer l'historique a la
/// volee reecrirait le passe a chaque ouverture d'ecran, et le chiffre communique au conseil
/// d'administration ne serait plus retrouvable trois mois plus tard.
/// <see cref="FormulaVersionChanged"/> previent quand la serie couvre plusieurs versions de
/// formule et n'est donc pas comparable de bout en bout.
/// </summary>
public sealed record KpiHistoryResponse(
    string Code,
    string Name,
    KpiUnit Unit,
    KpiPolarity Polarity,
    string? HotelUnitCode,
    string? HotelUnitName,
    int CurrentFormulaVersion,
    bool FormulaVersionChanged,
    IReadOnlyCollection<KpiHistoryPoint> Points);
