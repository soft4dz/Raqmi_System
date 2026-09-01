using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Une saisie de recette journaliere. Le STATUT est transporte tel quel : la regle "seule une
/// recette Validee est du chiffre d'affaires" appartient au calculateur, qui la reapplique sur
/// ce qu'il recoit, et les tests la prouvent donc sur des donnees non filtrees - meme
/// discipline que <c>GroupDashboardCalculator</c>. Le filtre pose cote base n'est qu'une
/// optimisation qui reproduit cette regle, jamais sa definition.
/// </summary>
public sealed record KpiRevenueFact(
    string HotelUnitCode,
    DateOnly BusinessDate,
    decimal Accommodation,
    decimal Food,
    decimal Beverage,
    decimal Other,
    DailyRevenueStatus Status)
{
    public decimal Total => Accommodation + Food + Beverage + Other;
}
