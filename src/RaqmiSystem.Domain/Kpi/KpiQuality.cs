namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// Etat de fiabilite d'une valeur calculee. C'est la garantie du volet "qualite des donnees" :
/// un indicateur ne doit JAMAIS afficher 0 quand la verite est "je ne sais pas". Un RevPAR
/// sans chambres disponibles renseignees vaut <see cref="MissingData"/>, pas zero.
/// </summary>
public enum KpiQuality
{
    /// <summary>Toutes les donnees necessaires sont presentes ; la valeur fait autorite.</summary>
    Valid = 1,

    /// <summary>
    /// La valeur est calculee mais une partie du perimetre manque (une unite sans chambre
    /// declaree, un mois de paie non genere). Elle est exploitable en tendance, pas en absolu.
    /// </summary>
    Partial = 2,

    /// <summary>
    /// Une donnee indispensable manque : la valeur est nulle et l'ecran affiche un tiret. La
    /// liste portee par la mesure dit precisement ce qui manque.
    /// </summary>
    MissingData = 3,

    /// <summary>
    /// L'indicateur ne s'applique pas a ce perimetre ou sa source n'existe pas encore dans
    /// Raqmi System (voir <see cref="KpiAvailability.AwaitingSource"/>). Ce n'est ni une erreur
    /// ni une donnee manquante : c'est une case qui n'a pas lieu d'etre remplie.
    /// </summary>
    NotApplicable = 4
}
