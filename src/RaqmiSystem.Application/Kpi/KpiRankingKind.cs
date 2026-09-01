namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Les quatre lectures d'un classement inter-unites. Elles sont volontairement SEPAREES et
/// jamais fondues en une note globale : un score composite suppose des ponderations - combien
/// vaut un point d'occupation face a un point de food cost ? - et aucune ponderation par defaut
/// ne serait defendable. Tant que la direction n'a pas fixe les siennes, le comparatif classe
/// indicateur par indicateur, ce qui est exact, ou il ne classe pas.
/// </summary>
public enum KpiRankingKind
{
    /// <summary>Meilleure valeur absolue de l'indicateur, dans le sens de sa polarite.</summary>
    BestPerformance = 1,

    /// <summary>Plus forte progression par rapport a la periode equivalente un an plus tot.</summary>
    StrongestProgress = 2,

    /// <summary>Plus fort ecart au budget, en valeur absolue - au-dessus comme en dessous.</summary>
    LargestBudgetGap = 3,

    /// <summary>Valeur la plus faible de l'indicateur, dans le sens de sa polarite.</summary>
    WeakestPerformance = 4
}
