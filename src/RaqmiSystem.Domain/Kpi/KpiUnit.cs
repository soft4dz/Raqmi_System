namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// Unite de mesure d'un indicateur. Elle commande le FORMATAGE cote client et rien d'autre :
/// le moteur ne convertit jamais entre unites, et deux indicateurs d'unites differentes ne
/// sont jamais additionnes.
/// </summary>
public enum KpiUnit
{
    /// <summary>Montant dans la devise de l'etablissement.</summary>
    Currency = 1,

    /// <summary>Pourcentage deja exprime sur 100 (42.5 signifie 42,5 %).</summary>
    Percentage = 2,

    /// <summary>Nombre entier d'occurrences (chambres, sejours, salaries).</summary>
    Count = 3,

    /// <summary>Nombre de nuitees.</summary>
    Nights = 4,

    /// <summary>Nombre de jours.</summary>
    Days = 5,

    /// <summary>Nombre d'heures.</summary>
    Hours = 6,

    /// <summary>Rapport sans dimension (rotation de stock, indice).</summary>
    Ratio = 7,

    /// <summary>Score sur une echelle bornee propre a l'indicateur (NPS : -100 a +100).</summary>
    Score = 8
}
