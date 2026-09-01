namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// A quelle maille un indicateur a un sens dans CE produit.
///
/// La distinction n'est pas theorique : elle constate ce que les donnees permettent. La
/// comptabilite de Raqmi System n'est pas analytique - une ecriture porte un compte, un journal
/// et une date, jamais une unite hoteliere - et un ordre de paiement ne porte pas davantage
/// d'unite. Tout ce qui en derive (resultat, marges, decaissements, flux de tresorerie) existe
/// donc au niveau du GROUPE et nulle part ailleurs.
///
/// Repartir ces montants entre les unites au prorata d'une cle quelconque - le chiffre
/// d'affaires, le nombre de chambres - produirait un resultat par unite d'apparence
/// convaincante et sans aucun fondement comptable. Le moteur prefere dire "cet indicateur
/// n'existe qu'au niveau groupe" plutot que d'inventer une comptabilite analytique que
/// l'etablissement n'a pas mise en place.
/// </summary>
public enum KpiScopeLevel
{
    /// <summary>Mesurable par unite et consolidable au groupe.</summary>
    UnitAndGroup = 1,

    /// <summary>
    /// Mesurable au niveau du groupe uniquement : la donnee source ne porte pas d'unite
    /// hoteliere. La reponse par unite est alors vide, jamais nulle par defaut.
    /// </summary>
    GroupOnly = 2
}
