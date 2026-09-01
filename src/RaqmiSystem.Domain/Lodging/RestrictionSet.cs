namespace RaqmiSystem.Domain.Lodging;

/// <summary>Nature d'une restriction violee, pour que l'appelant puisse la traiter et pas seulement l'afficher.</summary>
public enum RestrictionViolationKind
{
    /// <summary>Vente fermee sur une nuit du sejour (stop sell).</summary>
    Closed = 0,

    /// <summary>Arrivee interdite a cette date (CTA).</summary>
    ClosedToArrival = 1,

    /// <summary>Depart interdit a cette date (CTD).</summary>
    ClosedToDeparture = 2,

    /// <summary>Sejour trop court (MinLOS).</summary>
    MinimumStay = 3,

    /// <summary>Sejour trop long (MaxLOS).</summary>
    MaximumStay = 4,

    /// <summary>Reservation trop tardive par rapport a l'arrivee.</summary>
    MinimumAdvance = 5,

    /// <summary>Reservation trop lointaine par rapport a l'arrivee.</summary>
    MaximumAdvance = 6
}

/// <summary>Une restriction violee : sa nature, la date en cause quand il y en a une, et le message a montrer.</summary>
public sealed record RestrictionViolation(RestrictionViolationKind Kind, DateOnly? Date, string Message);

/// <summary>
/// Verdict d'un controle de restrictions. Une vente est autorisee quand la liste est vide ; sinon
/// elle porte TOUTES les violations et non seulement la premiere, pour qu'un operateur qui corrige
/// une date ne decouvre pas la suivante au coup d'apres.
/// </summary>
public sealed record RestrictionDecision(IReadOnlyList<RestrictionViolation> Violations)
{
    /// <summary>Verdict favorable.</summary>
    public static RestrictionDecision Allowed { get; } = new([]);

    public bool IsAllowed => Violations.Count == 0;

    /// <summary>Message unique reprenant toutes les violations, pour une reponse d'API.</summary>
    public string Describe()
    {
        return Violations.Count == 0
            ? string.Empty
            : string.Join(" ", Violations.Select(violation => violation.Message));
    }
}

/// <summary>
/// Le moteur de restrictions, en calcul pur : il prend les lignes de <see cref="RateRestriction"/>
/// deja chargees et rend un verdict. Aucune dependance a la base, donc testable a la ligne et
/// reutilisable tel quel par la recherche de disponibilite, la creation de reservation, le moteur
/// de reservation directe et le channel manager - qui doivent tous appliquer LA MEME regle.
///
/// COMBINAISON : quand plusieurs lignes couvrent la meme date, la plus restrictive gagne. Une
/// fermeture l'emporte sur une ouverture, le plus grand MinLOS l'emporte, le plus petit MaxLOS
/// l'emporte. C'est la seule combinaison qui ne permet jamais de contourner un stop sell en
/// ajoutant une ligne plus fine.
///
/// SEMANTIQUE DES DUREES : MinLOS et MaxLOS sont lus SUR LA DATE D'ARRIVEE. C'est la convention
/// des channel managers et c'est celle qui a un sens commercial - "trois nuits minimum si vous
/// arrivez le 14 aout" - alors qu'un minimum lu sur chaque nuit rendrait la regle indecidable
/// quand deux nuits d'un meme sejour portent des minimums differents.
/// </summary>
public static class RestrictionSet
{
    /// <summary>
    /// Controle un sejour [<paramref name="arrival"/>, <paramref name="departure"/>) contre les
    /// restrictions applicables. <paramref name="bookingDate"/> est la date metier a laquelle la
    /// vente est prise : c'est elle, et non la date systeme, qui mesure les delais de reservation.
    /// </summary>
    public static RestrictionDecision Evaluate(
        IEnumerable<RateRestriction> restrictions,
        DateOnly arrival,
        DateOnly departure,
        DateOnly bookingDate,
        string? roomTypeCode,
        string? ratePlanCode,
        string? channelCode)
    {
        ArgumentNullException.ThrowIfNull(restrictions);

        if (departure <= arrival)
        {
            throw new ArgumentException(
                "La date de depart doit etre posterieure a la date d'arrivee.",
                nameof(departure));
        }

        var applicable = restrictions
            .Where(restriction => restriction.IsActive)
            .Where(restriction => restriction.AppliesTo(roomTypeCode, ratePlanCode, channelCode))
            .ToArray();

        if (applicable.Length == 0)
        {
            return RestrictionDecision.Allowed;
        }

        var violations = new List<RestrictionViolation>();
        var nights = departure.DayNumber - arrival.DayNumber;

        // 1. Stop sell : une seule nuit fermee suffit a refuser le sejour entier.
        for (var night = arrival; night < departure; night = night.AddDays(1))
        {
            if (applicable.Any(restriction => restriction.IsClosed && restriction.Covers(night)))
            {
                violations.Add(new RestrictionViolation(
                    RestrictionViolationKind.Closed,
                    night,
                    $"La vente est fermee pour la nuit du {Format(night)}."));
            }
        }

        // 2. CTA sur la date d'arrivee. Le sejour deja commence n'est pas concerne : c'est
        //    exactement ce qui distingue une fermeture a l'arrivee d'un stop sell.
        if (applicable.Any(restriction => restriction.IsClosedToArrival && restriction.Covers(arrival)))
        {
            violations.Add(new RestrictionViolation(
                RestrictionViolationKind.ClosedToArrival,
                arrival,
                $"Aucune arrivee n'est acceptee le {Format(arrival)} (CTA)."));
        }

        // 3. CTD sur la date de DEPART - qui n'est pas une nuit du sejour, d'ou son controle
        //    separe : une regle posee sur le 20 interdit de partir le 20, pas d'y dormir.
        if (applicable.Any(restriction => restriction.IsClosedToDeparture && restriction.Covers(departure)))
        {
            violations.Add(new RestrictionViolation(
                RestrictionViolationKind.ClosedToDeparture,
                departure,
                $"Aucun depart n'est accepte le {Format(departure)} (CTD)."));
        }

        var onArrival = applicable.Where(restriction => restriction.Covers(arrival)).ToArray();

        // 4. Duree minimale : le plus EXIGEANT des minimums lus sur la date d'arrivee.
        var minimumStay = onArrival
            .Where(restriction => restriction.MinimumStay > 0)
            .Select(restriction => restriction.MinimumStay)
            .DefaultIfEmpty(0)
            .Max();

        if (minimumStay > 0 && nights < minimumStay)
        {
            violations.Add(new RestrictionViolation(
                RestrictionViolationKind.MinimumStay,
                arrival,
                $"Un sejour commencant le {Format(arrival)} doit compter au moins {minimumStay} nuit(s) ; "
                + $"celui-ci en compte {nights}."));
        }

        // 5. Duree maximale : le plus CONTRAIGNANT des maximums.
        var maximumStay = onArrival
            .Where(restriction => restriction.MaximumStay > 0)
            .Select(restriction => restriction.MaximumStay)
            .DefaultIfEmpty(0)
            .Min();

        if (maximumStay > 0 && nights > maximumStay)
        {
            violations.Add(new RestrictionViolation(
                RestrictionViolationKind.MaximumStay,
                arrival,
                $"Un sejour commencant le {Format(arrival)} ne peut pas depasser {maximumStay} nuit(s) ; "
                + $"celui-ci en compte {nights}."));
        }

        // 6. Delais de reservation, mesures depuis la DATE METIER de la vente.
        var advanceDays = arrival.DayNumber - bookingDate.DayNumber;

        var minAdvance = onArrival
            .Where(restriction => restriction.MinAdvanceDays > 0)
            .Select(restriction => restriction.MinAdvanceDays)
            .DefaultIfEmpty(0)
            .Max();

        if (minAdvance > 0 && advanceDays < minAdvance)
        {
            violations.Add(new RestrictionViolation(
                RestrictionViolationKind.MinimumAdvance,
                arrival,
                $"Ce tarif exige une reservation au moins {minAdvance} jour(s) avant l'arrivee ; "
                + $"il en reste {Math.Max(advanceDays, 0)}."));
        }

        var maxAdvance = onArrival
            .Where(restriction => restriction.MaxAdvanceDays > 0)
            .Select(restriction => restriction.MaxAdvanceDays)
            .DefaultIfEmpty(0)
            .Min();

        if (maxAdvance > 0 && advanceDays > maxAdvance)
        {
            violations.Add(new RestrictionViolation(
                RestrictionViolationKind.MaximumAdvance,
                arrival,
                $"Ce tarif n'ouvre pas plus de {maxAdvance} jour(s) avant l'arrivee ; "
                + $"celle-ci est dans {advanceDays} jour(s)."));
        }

        return violations.Count == 0 ? RestrictionDecision.Allowed : new RestrictionDecision(violations);
    }

    /// <summary>
    /// Les nuits fermees d'une periode, pour l'affichage du planning : la recherche de
    /// disponibilite en a besoin pour dire POURQUOI une date ne propose rien.
    /// </summary>
    public static IReadOnlySet<DateOnly> ClosedNights(
        IEnumerable<RateRestriction> restrictions,
        DateOnly from,
        DateOnly to,
        string? roomTypeCode,
        string? ratePlanCode,
        string? channelCode)
    {
        ArgumentNullException.ThrowIfNull(restrictions);

        var applicable = restrictions
            .Where(restriction => restriction.IsActive && restriction.IsClosed)
            .Where(restriction => restriction.AppliesTo(roomTypeCode, ratePlanCode, channelCode))
            .ToArray();

        var closed = new HashSet<DateOnly>();

        for (var night = from; night < to; night = night.AddDays(1))
        {
            if (applicable.Any(restriction => restriction.Covers(night)))
            {
                closed.Add(night);
            }
        }

        return closed;
    }

    private static string Format(DateOnly date)
    {
        return date.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
    }
}
