namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Un objectif budgetaire mensuel, toutes categories confondues pour une unite et un mois. Le
/// budget de Raqmi System est mensuel par construction ; le decouper au jour inventerait une
/// saisonnalite que personne n'a budgetee, aussi une periode qui touche un mois compte ce mois
/// EN ENTIER - regle deja posee par le tableau de bord groupe et reprise telle quelle.
/// </summary>
public sealed record KpiBudgetTargetFact(string HotelUnitCode, int Year, int Month, decimal AmountTarget);
