namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Un sejour, reduit a ce dont les indicateurs ont besoin. Comme
/// <see cref="KpiRoomOutageFact"/>, ce fait est NEUTRE vis-a-vis du cycle de vie des
/// reservations : il porte des BOOLEENS de comportement, jamais le statut du module
/// hebergement.
///
/// La raison est concrete : le vocabulaire des statuts de reservation evolue avec le PMS
/// (demande, option, confirmee, garantie...), alors que les trois questions que se pose un
/// indicateur ne changent jamais - ce sejour tient-il la chambre, a-t-il ete annule, le client
/// s'est-il presente ? En posant ces trois questions plutot qu'en lisant un statut, le moteur
/// KPI ne se casse pas quand le PMS s'enrichit, et le chargeur de faits reste le seul endroit a
/// mettre a jour.
///
/// Les dates suivent la convention hoteliere [arrivee, depart[ : la nuit du jour de depart ne
/// fait pas partie du sejour, ce qui permet a une chambre liberee le 10 d'etre reoccupee le 10.
/// </summary>
/// <param name="NightlyRate">
/// Tarif nuit fige a la prise de reservation. Zero signifie une nuitee gratuite ou en house
/// use : elle occupe la chambre mais ne se vend pas, et sort donc du denominateur de l'ADR.
/// </param>
/// <param name="BlocksInventory">
/// Le sejour tient-il la chambre ? C'est la definition unique de "chambre occupee", posee par
/// le module hebergement et simplement transportee ici.
/// </param>
/// <param name="CreatedAt">
/// Date de prise de la reservation dans le systeme, pour le delai moyen de reservation.
/// </param>
public sealed record KpiStayFact(
    string HotelUnitCode,
    Guid RoomId,
    string CustomerCode,
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    int GuestCount,
    decimal NightlyRate,
    bool BlocksInventory,
    bool IsCancelled,
    bool IsNoShow,
    DateTimeOffset CreatedAt)
{
    /// <summary>Nuits couvertes par le sejour entier, quelles que soient les bornes d'analyse.</summary>
    public int TotalNights => DepartureDate.DayNumber - ArrivalDate.DayNumber;

    /// <summary>Nuitee offerte : elle occupe une chambre sans produire de recette.</summary>
    public bool IsComplimentary => NightlyRate == 0m;

    /// <summary>Le sejour couvre-t-il cette nuit ?</summary>
    public bool CoversNight(DateOnly night)
    {
        return ArrivalDate <= night && night < DepartureDate;
    }
}
