namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Une unite du groupe avec ses indicateurs de tete : le premier niveau de descente du tableau
/// de bord. Le detail complet d'une unite s'obtient en rappelant le tableau de bord sur cette
/// unite, et le detail d'une transaction appartient a l'ecran du module qui la possede - la
/// bibliotheque KPI ne reconstruit jamais un journal qui existe deja ailleurs.
/// </summary>
public sealed record KpiUnitCard(
    string HotelUnitCode,
    string HotelUnitName,
    bool IsActive,
    IReadOnlyCollection<KpiMeasureResponse> Headline);
