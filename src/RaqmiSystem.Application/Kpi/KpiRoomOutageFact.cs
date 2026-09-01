namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Une periode d'indisponibilite d'une chambre, exprimee en bornes de nuits sur la convention
/// hoteliere [debut, fin[ : la nuit du jour de fin n'est plus immobilisee.
///
/// Ce fait est volontairement NEUTRE vis-a-vis du module qui le produit. Le moteur KPI n'a pas
/// a savoir si l'indisponibilite vient d'un etat housekeeping ou d'un blocage de chambre date :
/// il a besoin de deux choses seulement, la fenetre de nuits concernee et la nature de
/// l'indisponibilite. C'est ce qui permet au calcul de l'occupation de survivre a une
/// refonte du module hebergement sans une ligne de changement.
///
/// <paramref name="IsOutOfOrder"/> distingue le hors service TECHNIQUE (panne, travaux : la
/// chambre n'est pas louable, point) du hors service d'EXPLOITATION (nettoyage approfondi,
/// usage interne : la chambre est en etat mais retiree de la vente). Les deux sortent de la
/// capacite vendable ; les distinguer permet de dire a la direction si sa capacite perdue vient
/// d'un probleme technique ou d'un choix d'exploitation.
///
/// Une indisponibilite ouverte (sans fin connue) est representee par une borne de fin lointaine
/// posee par le chargeur, jamais par un null : un intervalle sans fin ne se compare pas.
/// </summary>
public sealed record KpiRoomOutageFact(
    string HotelUnitCode,
    Guid RoomId,
    DateOnly From,
    DateOnly ToExclusive,
    bool IsOutOfOrder)
{
    /// <summary>Cette nuit-la est-elle immobilisee par cette indisponibilite ?</summary>
    public bool CoversNight(DateOnly night)
    {
        return From <= night && night < ToExclusive;
    }
}
