namespace RaqmiSystem.Domain.Revenue;

/// <summary>
/// Secteur d'activité qu'un paramétrage peut cibler (aujourd'hui : les catégories de recettes).
/// Déclaré ici, dans le module qui en a le premier besoin, parce que l'organisation ne porte pas
/// encore de secteur : chaque <c>HotelUnitType</c> relève de l'hôtellerie. Le jour où
/// l'établissement portera son secteur, ce type peut être déplacé vers Organization sans en
/// changer les valeurs — elles sont stockées en texte, pas en entier.
/// </summary>
public enum BusinessSector
{
    Hospitality = 1,
    Trade = 2,
    Services = 3,
    Industry = 4,
    Other = 99
}
