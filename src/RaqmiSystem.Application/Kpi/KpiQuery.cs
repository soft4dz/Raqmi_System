using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Ce qu'un appelant demande au moteur : une fenetre, un perimetre et une methode de calcul du
/// DSO.
///
/// <see cref="DepartmentCode"/> ne s'applique qu'aux indicateurs de RESSOURCES HUMAINES : le
/// departement est un referentiel du module RH, porte par le poste du collaborateur, et aucune
/// autre donnee de Raqmi System - ni une recette, ni un mouvement de stock, ni une ecriture - ne
/// le porte. Quand il est renseigne, la reponse se limite donc a la famille RH et le dit dans sa
/// base. Restreindre les autres familles sur un critere qu'elles ne portent pas rendrait des
/// chiffres non filtres sous une etiquette de departement, ce qui serait le pire des deux
/// mondes.
///
/// Il n'existe pas de referentiel de CENTRES DE COUTS dans le produit : le departement RH en
/// tient lieu pour la seule masse salariale, et un axe analytique complet - qui devrait aussi
/// porter sur les achats, les stocks et la comptabilite - reste a creer.
/// </summary>
public sealed record KpiQuery(
    DateOnly From,
    DateOnly To,
    string? HotelUnitCode = null,
    string? DepartmentCode = null,
    KpiDsoMethod DsoMethod = KpiDsoMethod.Simple,
    bool CompareToPreviousYear = true,
    bool CompareToBudget = true)
{
    /// <summary>
    /// Plafond de la fenetre d'analyse. L'occupation et la capacite sont comptees jour par jour
    /// en memoire : sans borne, une requete portant dix ans transformerait une lecture d'ecran
    /// en boucle non bornee. Meme plafond que l'occupation du module hebergement et que le
    /// tableau de bord groupe, pour que les trois refusent les memes fenetres.
    /// </summary>
    public const int MaxWindowDays = 366;

    public KpiPeriod ToPeriod() => KpiPeriod.Create(From, To);
}
