namespace RaqmiSystem.Application.Budgeting;

/// <summary>
/// Une ligne de recette réalisée qui alimente le rapport d'écarts : la date d'exploitation, le
/// code de catégorie et le montant, projetés de <c>exploitation.daily_revenue_lines</c>. The
/// caller is responsible for having already filtered the rows to the hotel unit, the period AND
/// the Validated status - see <see cref="BudgetVarianceCalculator"/> for why only validated
/// revenue counts as actual.
/// </summary>
public sealed record BudgetActualRevenue(
    DateOnly BusinessDate,
    string Category,
    decimal Amount);
