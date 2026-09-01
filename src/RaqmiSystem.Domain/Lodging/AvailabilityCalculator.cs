namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// La disponibilite d'un type de chambre sur une periode, nuit par nuit, et ce qu'elle autorise.
/// </summary>
public sealed record RoomTypeAvailability(
    string RoomTypeCode,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<NightInventory> Nights)
{
    /// <summary>
    /// Chambres vendables au public sur TOUTE la periode : le minimum des nuits, pas la moyenne.
    /// Un sejour a besoin de la chambre chaque nuit ; une nuit a zero suffit a rendre le sejour
    /// impossible, quelle que soit la generosite des autres.
    /// </summary>
    public int PublicAvailable => Nights.Count == 0 ? 0 : Nights.Min(night => night.PublicAvailable);

    /// <summary>Chambres vendables sur toute la periode, surreservation comprise.</summary>
    public int CommercialAvailable => Nights.Count == 0 ? 0 : Nights.Min(night => night.CommercialAvailable);

    /// <summary>Capacite physique minimale exploitable sur la periode.</summary>
    public int SellableCapacity => Nights.Count == 0 ? 0 : Nights.Min(night => night.SellableCapacity);

    /// <summary>Vrai quand aucune chambre physique n'est libre mais que la surreservation en ouvre.</summary>
    public bool RequiresOverbooking => PublicAvailable == 0 && CommercialAvailable > 0;

    /// <summary>La nuit qui limite la periode : celle dont le disponible public est le plus faible.</summary>
    public NightInventory? BottleneckNight => Nights.Count == 0
        ? null
        : Nights.OrderBy(night => night.PublicAvailable).ThenBy(night => night.Night).First();
}

/// <summary>
/// LE calcul de disponibilite du produit, en fonction pure.
///
/// POURQUOI IL EST ISOLE ICI. Le principe fondamental du PMS est qu'il n'existe qu'UNE source de
/// verite pour l'inventaire : la recherche de disponibilite, la creation de reservation, le
/// forecast, le moteur de reservation directe et le channel manager doivent tous compter de la
/// meme facon. La seule maniere robuste d'y arriver est que ces cinq chemins appellent la meme
/// fonction, et que cette fonction ne sache rien de la base - sans quoi chacun finit par
/// reimplementer "sa" soustraction, et l'hotel survend en silence.
///
/// Les entrees sont deja chargees par l'infrastructure ; ici il n'y a que de l'arithmetique, ce qui
/// rend chaque regle testable a la ligne.
/// </summary>
public static class AvailabilityCalculator
{
    /// <summary>
    /// Construit l'inventaire nuit par nuit d'un type sur [<paramref name="from"/>,
    /// <paramref name="to"/>). Les dictionnaires sont creux : une nuit absente vaut zero.
    /// </summary>
    public static RoomTypeAvailability Build(
        string roomTypeCode,
        DateOnly from,
        DateOnly to,
        int physicalRooms,
        IReadOnlyDictionary<DateOnly, int>? blockedRooms,
        IReadOnlyDictionary<DateOnly, int>? soldRooms,
        IReadOnlyDictionary<DateOnly, int>? allotmentHolds,
        IReadOnlyDictionary<DateOnly, int>? overbookingAllowed)
    {
        if (to <= from)
        {
            throw new ArgumentException(
                "La date de fin doit etre posterieure a la date de debut.",
                nameof(to));
        }

        var nights = new List<NightInventory>(to.DayNumber - from.DayNumber);

        for (var night = from; night < to; night = night.AddDays(1))
        {
            nights.Add(new NightInventory(
                night,
                physicalRooms,
                Read(blockedRooms, night),
                Read(soldRooms, night),
                Read(allotmentHolds, night),
                Read(overbookingAllowed, night)));
        }

        return new RoomTypeAvailability(RoomType.NormalizeCode(roomTypeCode), from, to, nights);
    }

    /// <summary>
    /// Combien de chambres de ce type une vente PUBLIQUE peut-elle encore prendre sur la periode,
    /// et cette vente franchit-elle la capacite physique ?
    ///
    /// <paramref name="allowOverbooking"/> traduit deux decisions distinctes qui doivent etre
    /// vraies ensemble : l'unite autorise la surreservation, et l'operateur a le droit de
    /// l'utiliser. Une seule des deux ne suffit pas.
    /// </summary>
    public static SaleCapacity CapacityForPublicSale(RoomTypeAvailability availability, bool allowOverbooking)
    {
        ArgumentNullException.ThrowIfNull(availability);

        var physical = availability.PublicAvailable;

        if (!allowOverbooking)
        {
            return new SaleCapacity(physical, false, availability.BottleneckNight);
        }

        var commercial = availability.CommercialAvailable;

        return new SaleCapacity(commercial, physical == 0 && commercial > 0, availability.BottleneckNight);
    }

    /// <summary>
    /// Agrege les inventaires de plusieurs types en un inventaire d'unite, nuit par nuit. Sert au
    /// forecast et aux indicateurs, jamais a decider d'une vente : on ne loge pas un client dans la
    /// moyenne des types.
    /// </summary>
    public static IReadOnlyList<NightInventory> Aggregate(IEnumerable<RoomTypeAvailability> availabilities)
    {
        ArgumentNullException.ThrowIfNull(availabilities);

        return availabilities
            .SelectMany(availability => availability.Nights)
            .GroupBy(night => night.Night)
            .OrderBy(group => group.Key)
            .Select(group => group.Aggregate((left, right) => left + right))
            .ToArray();
    }

    private static int Read(IReadOnlyDictionary<DateOnly, int>? source, DateOnly night)
    {
        return source is not null && source.TryGetValue(night, out var value) ? Math.Max(0, value) : 0;
    }
}

/// <summary>
/// Ce qu'une vente peut prendre : le nombre de chambres, et si la prochaine franchit la capacite
/// physique. Le second champ n'est pas cosmetique - c'est lui qui marque le dossier en
/// surreservation et permet a la reception d'organiser le relogement avant le jour J.
/// </summary>
public sealed record SaleCapacity(int Rooms, bool NextSaleIsOverbooking, NightInventory? BottleneckNight)
{
    public bool CanSell => Rooms > 0;
}
