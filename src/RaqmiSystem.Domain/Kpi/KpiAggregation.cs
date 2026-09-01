namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// Comment la valeur GROUPE se deduit des valeurs par unite. C'est la regle la plus souvent
/// fausse dans les outils de pilotage hotelier, et elle est donc portee par la definition de
/// chaque indicateur plutot que par le code d'un ecran.
///
/// LA REGLE : un TAUX ne se moyenne jamais. L'ADR groupe n'est pas la moyenne des ADR des
/// unites, c'est la somme des revenus chambres divisee par la somme des chambres vendues.
/// Moyenner donnerait le meme poids a un hotel de 20 chambres et a un hotel de 300. Le moteur
/// consolide donc en additionnant NUMERATEUR et DENOMINATEUR, puis en refaisant la division -
/// c'est le sens de <see cref="RatioOfSums"/>, l'agregation de la quasi-totalite des taux.
/// </summary>
public enum KpiAggregation
{
    /// <summary>
    /// Additif : la valeur groupe est la somme des valeurs par unite (chiffre d'affaires,
    /// nuitees, chambres disponibles, masse salariale).
    /// </summary>
    Sum = 1,

    /// <summary>
    /// Ratio recalcule : somme des numerateurs / somme des denominateurs (occupation, ADR,
    /// RevPAR, food cost, masse salariale sur CA, DSO). JAMAIS une moyenne des taux.
    /// </summary>
    RatioOfSums = 2,

    /// <summary>
    /// Moyenne simple, legitime uniquement quand chaque observation pese autant que les autres
    /// et qu'aucun denominateur ne porte le poids (score de satisfaction, NPS).
    /// </summary>
    Average = 3,

    /// <summary>
    /// Non consolidable : l'indicateur n'a de sens qu'a l'echelle d'une unite, ou son cumul
    /// groupe serait trompeur. L'API renvoie alors une valeur groupe nulle et le detail par
    /// unite.
    /// </summary>
    NotAggregatable = 4
}
