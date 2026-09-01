using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Un bulletin de paie, reduit au cout employeur et au temps paye.
///
/// Seuls les bulletins VALIDES alimentent les indicateurs : une pre-paie en brouillon est
/// recalculee de fond en comble a chaque generation, et la faire entrer dans un ratio de masse
/// salariale ferait bouger l'indicateur sans qu'aucune decision de paie ait ete prise. Le
/// statut est transporte pour que le calculateur reapplique lui-meme la regle.
///
/// <paramref name="EmployerCost"/> est le cout complet employeur - brut imposable, cotisations
/// patronales et taxes sur salaires - tel que le module Paie le calcule et l'imprime sur le
/// bulletin. Le moteur KPI ne recompose jamais ce montant a partir de ses composantes : il
/// serait absurde qu'un tableau de bord et un bulletin ne disent pas le meme chiffre.
/// </summary>
public sealed record KpiPayslipFact(
    string HotelUnitCode,
    string DepartmentCode,
    int Year,
    int Month,
    decimal EmployerCost,
    decimal HoursWorked,
    decimal OvertimeHours,
    PayslipStatus Status);
