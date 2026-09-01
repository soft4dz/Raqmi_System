using RaqmiSystem.Domain.Kpi;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Les indicateurs d'hebergement, calcules en memoire pure a partir des faits deja rapatries.
/// Aucun acces base : c'est ce qui rend chaque formule testable ligne a ligne, independamment
/// d'EF - meme partage des roles que <c>GroupDashboardCalculator</c>.
///
/// LES DEUX OCCUPATIONS. Le produit publie UN taux d'occupation, qui compte les nuitees
/// gratuites : la chambre est bien occupee, elle n'est simplement pas vendue. L'ADR, lui,
/// divise par les nuitees VENDUES, gratuites exclues, sans quoi une operation commerciale
/// ferait chuter le prix moyen sans qu'aucun tarif ait bouge.
///
/// Consequence assumee, et c'est le point que tout controleur de gestion doit connaitre :
/// l'identite RevPAR = ADR x occupation se verifie contre le taux d'occupation VENDUE
/// (nuitees vendues / disponibles), pas contre le taux publie. Les deux coincident exactement
/// des que l'unite n'a offert aucune nuitee, ce qui est le cas general ; l'ecart, quand il
/// existe, mesure exactement le poids des gratuites. Le test
/// <c>RevPar_equals_adr_times_sold_occupancy</c> epingle cette identite.
///
/// LA CAPACITE. Les nuitees disponibles ne sont pas "chambres x jours" : ce sont les chambres
/// ACTIVES, moins celles retirees de la vente cette nuit-la. Une chambre en travaux n'est pas
/// une chambre vide, et la compter comme disponible ferait porter a l'exploitation la
/// responsabilite d'un probleme technique.
/// </summary>
public sealed class LodgingKpiCalculator
{
    private const string NoCapacity =
        "Aucune nuitee disponible sur la periode : l'unite n'a pas de chambre active, ou toutes "
        + "ses chambres sont indisponibles.";

    private const string NoSoldNight =
        "Aucune nuitee vendue sur la periode : le prix moyen n'a pas d'objet.";

    private const string NoStay = "Aucun sejour sur la periode.";

    private const string NoReservation = "Aucune reservation attendue sur la periode.";

    private const string NoGuestNight = "Aucune nuitee client sur la periode.";

    public IEnumerable<KpiMeasure> Compute(KpiPeriod period, string? unitCode, KpiFactSet facts)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(facts);

        var activeRooms = facts.Rooms.Where(room => room.IsActive).ToArray();
        var blockingStays = facts.Stays.Where(stay => stay.BlocksInventory).ToArray();

        var availableNights = CountAvailableNights(period, activeRooms, facts.RoomOutages);
        var unavailableNights = CountUnavailableNights(period, activeRooms, facts.RoomOutages);
        var occupiedNights = CountOccupiedNights(period, blockingStays, complimentaryOnly: false);
        var complimentaryNights = CountOccupiedNights(period, blockingStays, complimentaryOnly: true);
        var soldNights = occupiedNights - complimentaryNights;
        var guestNights = CountGuestNights(period, blockingStays);

        var validated = facts.Revenues
            .Where(revenue => revenue.Status == DailyRevenueStatus.Validated)
            .ToArray();

        var roomRevenue = validated.Sum(revenue => revenue.Accommodation);
        var totalRevenue = validated.Sum(revenue => revenue.Total);

        yield return KpiMeasure.Amount(KpiCodes.PhysicalRooms, unitCode, facts.Rooms.Count);
        yield return KpiMeasure.Amount(KpiCodes.RoomsAvailable, unitCode, availableNights);
        yield return KpiMeasure.Amount(KpiCodes.RoomsOutOfOrder, unitCode, unavailableNights);
        yield return KpiMeasure.Amount(KpiCodes.RoomsOccupied, unitCode, occupiedNights);
        yield return KpiMeasure.Amount(KpiCodes.ComplimentaryRooms, unitCode, complimentaryNights);
        yield return KpiMeasure.Amount(KpiCodes.RoomsSold, unitCode, soldNights);
        yield return KpiMeasure.Amount(KpiCodes.GuestNights, unitCode, guestNights);

        yield return KpiMeasure.Ratio(
            KpiCodes.OccupancyRate, unitCode, occupiedNights, availableNights, KpiMath.Percent, NoCapacity);

        yield return KpiMeasure.Ratio(
            KpiCodes.Adr, unitCode, roomRevenue, soldNights, KpiMath.Divide, NoSoldNight);

        yield return KpiMeasure.Ratio(
            KpiCodes.RevPar, unitCode, roomRevenue, availableNights, KpiMath.Divide, NoCapacity);

        yield return KpiMeasure.Ratio(
            KpiCodes.TRevPar, unitCode, totalRevenue, availableNights, KpiMath.Divide, NoCapacity);

        yield return KpiMeasure.Ratio(
            KpiCodes.RevenuePerGuest, unitCode, totalRevenue, guestNights, KpiMath.Divide, NoGuestNight);

        foreach (var measure in ComputeStayMeasures(period, unitCode, facts))
        {
            yield return measure;
        }
    }

    /// <summary>
    /// Les indicateurs qui comptent des SEJOURS et non des nuits. Ils se lisent tous sur les
    /// sejours dont l'ARRIVEE tombe dans la periode : un sejour a cheval sur deux mois ne doit
    /// etre compte qu'une fois, et c'est son arrivee qui le date commercialement.
    /// </summary>
    private static IEnumerable<KpiMeasure> ComputeStayMeasures(
        KpiPeriod period,
        string? unitCode,
        KpiFactSet facts)
    {
        var arrivals = facts.Stays
            .Where(stay => stay.ArrivalDate >= period.From && stay.ArrivalDate <= period.To)
            .ToArray();

        var blockingArrivals = arrivals.Where(stay => stay.BlocksInventory).ToArray();

        // Duree moyenne de sejour : nuits du sejour ENTIER, y compris celles qui debordent de la
        // periode - amputer un sejour de ses nuits hors fenetre raccourcirait mecaniquement
        // l'ALOS de toutes les fins de mois.
        yield return KpiMeasure.Ratio(
            KpiCodes.Alos,
            unitCode,
            blockingArrivals.Sum(stay => stay.TotalNights),
            blockingArrivals.Length,
            KpiMath.Divide,
            NoStay);

        // Denominateur du taux d'annulation : les vraies reservations, c'est-a-dire celles qui
        // ont tenu la chambre a un moment ou l'autre, plus celles qui ont ete annulees ou qui se
        // sont soldees par un no-show. Une simple demande jamais confirmee n'a jamais ete une
        // reservation et n'a donc pas a en gonfler le denominateur.
        var reservations = arrivals
            .Where(stay => stay.BlocksInventory || stay.IsCancelled || stay.IsNoShow)
            .ToArray();

        yield return KpiMeasure.Ratio(
            KpiCodes.CancellationRate,
            unitCode,
            reservations.Count(stay => stay.IsCancelled),
            reservations.Length,
            KpiMath.Percent,
            NoReservation);

        // Denominateur du no-show : les arrivees ATTENDUES, donc hors annulations - une
        // reservation annulee n'attendait plus personne.
        var expected = reservations.Where(stay => !stay.IsCancelled).ToArray();

        yield return KpiMeasure.Ratio(
            KpiCodes.NoShowRate,
            unitCode,
            expected.Count(stay => stay.IsNoShow),
            expected.Length,
            KpiMath.Percent,
            NoReservation);

        yield return KpiMeasure.Amount(
            KpiCodes.NoShowLostRevenue,
            unitCode,
            expected.Where(stay => stay.IsNoShow).Sum(stay => stay.NightlyRate * stay.TotalNights));

        // Delai de reservation : jamais negatif. Une reservation saisie apres l'arrivee est un
        // walk-in enregistre en retard, pas une machine a remonter le temps ; la compter pour
        // zero jour est exact, la compter en negatif raccourcirait faussement la moyenne.
        var leadTimeStays = arrivals.Where(stay => !stay.IsCancelled).ToArray();

        yield return KpiMeasure.Ratio(
            KpiCodes.BookingLeadTime,
            unitCode,
            leadTimeStays.Sum(stay => Math.Max(
                0,
                stay.ArrivalDate.DayNumber - DateOnly.FromDateTime(stay.CreatedAt.UtcDateTime).DayNumber)),
            leadTimeStays.Length,
            KpiMath.Divide,
            NoReservation);

        yield return KpiMeasure.Ratio(
            KpiCodes.RepeatGuestRate,
            unitCode,
            blockingArrivals.Count(stay => facts.ReturningCustomerCodes.Contains(stay.CustomerCode)),
            blockingArrivals.Length,
            KpiMath.Percent,
            NoStay);
    }

    /// <summary>
    /// Nuitees vendables : pour chaque nuit, les chambres actives qu'aucune indisponibilite ne
    /// couvre. Le comptage est fait nuit par nuit et non "chambres x jours moins blocages",
    /// parce que deux blocages peuvent se chevaucher sur la meme chambre et que la soustraction
    /// retirerait alors deux fois la meme nuit.
    /// </summary>
    private static int CountAvailableNights(
        KpiPeriod period,
        IReadOnlyCollection<KpiRoomFact> activeRooms,
        IReadOnlyCollection<KpiRoomOutageFact> outages)
    {
        if (activeRooms.Count == 0)
        {
            return 0;
        }

        var activeRoomIds = activeRooms.Select(room => room.RoomId).ToHashSet();
        var relevantOutages = outages.Where(outage => activeRoomIds.Contains(outage.RoomId)).ToArray();
        var available = 0;

        for (var day = period.From; day <= period.To; day = day.AddDays(1))
        {
            var night = day;
            var blocked = relevantOutages
                .Where(outage => outage.CoversNight(night))
                .Select(outage => outage.RoomId)
                .Distinct()
                .Count();

            available += activeRooms.Count - blocked;
        }

        return available;
    }

    /// <summary>Le complement du precedent : la capacite perdue, nuit par nuit.</summary>
    private static int CountUnavailableNights(
        KpiPeriod period,
        IReadOnlyCollection<KpiRoomFact> activeRooms,
        IReadOnlyCollection<KpiRoomOutageFact> outages)
    {
        if (activeRooms.Count == 0)
        {
            return 0;
        }

        var activeRoomIds = activeRooms.Select(room => room.RoomId).ToHashSet();
        var relevantOutages = outages.Where(outage => activeRoomIds.Contains(outage.RoomId)).ToArray();
        var unavailable = 0;

        for (var day = period.From; day <= period.To; day = day.AddDays(1))
        {
            var night = day;

            unavailable += relevantOutages
                .Where(outage => outage.CoversNight(night))
                .Select(outage => outage.RoomId)
                .Distinct()
                .Count();
        }

        return unavailable;
    }

    /// <summary>
    /// Nuitees occupees : pour chaque nuit, les chambres DISTINCTES couvertes par un sejour
    /// tenant l'inventaire. Le distinct est conserve meme si l'invariant anti-double-reservation
    /// devrait le rendre inutile : des donnees anterieures a cet invariant ne doivent pas
    /// pouvoir gonfler un taux d'occupation au-dela de cent pour cent.
    /// </summary>
    private static int CountOccupiedNights(
        KpiPeriod period,
        IReadOnlyCollection<KpiStayFact> blockingStays,
        bool complimentaryOnly)
    {
        var stays = complimentaryOnly
            ? blockingStays.Where(stay => stay.IsComplimentary).ToArray()
            : blockingStays.ToArray();

        if (stays.Length == 0)
        {
            return 0;
        }

        var occupied = 0;

        for (var day = period.From; day <= period.To; day = day.AddDays(1))
        {
            var night = day;

            occupied += stays
                .Where(stay => stay.CoversNight(night))
                .Select(stay => stay.RoomId)
                .Distinct()
                .Count();
        }

        return occupied;
    }

    /// <summary>
    /// Nuitees clients : le nombre de PERSONNES hebergees chaque nuit. Contrairement aux
    /// nuitees chambres, il n'y a pas de distinct a faire - deux personnes dans la meme chambre
    /// sont bien deux nuitees clients.
    /// </summary>
    private static int CountGuestNights(KpiPeriod period, IReadOnlyCollection<KpiStayFact> blockingStays)
    {
        if (blockingStays.Count == 0)
        {
            return 0;
        }

        var guestNights = 0;

        for (var day = period.From; day <= period.To; day = day.AddDays(1))
        {
            var night = day;

            guestNights += blockingStays
                .Where(stay => stay.CoversNight(night))
                .Sum(stay => stay.GuestCount);
        }

        return guestNights;
    }
}
