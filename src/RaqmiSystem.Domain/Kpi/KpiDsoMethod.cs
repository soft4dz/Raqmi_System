namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// Methode de calcul du delai moyen de reglement client (DSO). Les deux existent dans la
/// pratique du controle de gestion et ne disent pas la meme chose ; le moteur les propose
/// toutes les deux et indique toujours laquelle a servi, plutot que d'en imposer une en
/// silence.
/// </summary>
public enum KpiDsoMethod
{
    /// <summary>
    /// Formule classique : encours / chiffre d'affaires a credit de la periode x nombre de jours
    /// de la periode. Simple, universellement comprise, mais elle suppose une activite reguliere :
    /// sur un hotel saisonnier, un encours d'ete rapporte a un chiffre d'affaires d'hiver donne
    /// un delai aberrant.
    /// </summary>
    Simple = 1,

    /// <summary>
    /// Epuisement des creances (count-back) : on remonte le chiffre d'affaires facture du plus
    /// recent au plus ancien jusqu'a absorber l'encours, et le DSO est le nombre de jours ainsi
    /// parcourus. Insensible a la saisonnalite, c'est la methode qu'utilisent les directions
    /// financieres pour piloter reellement le poste client.
    /// </summary>
    CountBack = 2
}
