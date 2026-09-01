namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// Sens de lecture d'un indicateur : une hausse est-elle une bonne ou une mauvaise nouvelle ?
///
/// C'est la seule chose qui permet au moteur de dire "favorable" ou "critique" sans qu'un
/// ecran ait a le savoir. Un taux d'occupation qui monte est bon, un food cost qui monte est
/// mauvais, et un nombre de chambres disponibles qui monte n'est ni bon ni mauvais - c'est une
/// capacite, pas une performance.
/// </summary>
public enum KpiPolarity
{
    /// <summary>Ni bon ni mauvais : volume, capacite, effectif. Aucune tendance n'est qualifiee.</summary>
    Neutral = 0,

    /// <summary>Plus c'est haut, mieux c'est : CA, occupation, ADR, RevPAR, marge.</summary>
    HigherIsBetter = 1,

    /// <summary>Plus c'est bas, mieux c'est : food cost, absenteisme, turnover, DSO, annulations.</summary>
    LowerIsBetter = 2
}
