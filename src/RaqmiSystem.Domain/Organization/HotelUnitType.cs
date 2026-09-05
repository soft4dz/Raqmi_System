namespace RaqmiSystem.Domain.Organization;

/// <summary>
/// La nature d'un établissement du référentiel. Ouvert au-delà de l'hôtellerie pour que le
/// même référentiel serve un commerce, un cabinet ou une école : le type qualifie le lieu, le
/// <see cref="BusinessSector"/> qualifie l'activité - et c'est le secteur, pas le type, qui
/// décide des paquets proposés.
/// </summary>
/// <remarks>
/// Les valeurs numériques sont stables et la base stocke le nom (contrainte CHECK dans
/// <c>HotelUnitConfiguration</c>) : ajouter un type, c'est l'ajouter ici ET dans la contrainte.
/// </remarks>
public enum HotelUnitType
{
    Hotel = 1,
    Residence = 2,
    BeachClub = 3,
    Marina = 4,
    Restaurant = 5,
    Shop = 6,
    Office = 7,
    Warehouse = 8,
    School = 9,
    Clinic = 10,
    Other = 99
}
