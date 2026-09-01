using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Une regle de vente posee sur une periode : fermeture (stop sell), fermeture a l'arrivee (CTA),
/// fermeture au depart (CTD), duree minimale et maximale de sejour, delais de reservation.
///
/// UNE SEULE ENTITE POUR SEPT REGLES, ET C'EST VOULU. Ces regles se posent ensemble, se levent
/// ensemble et portent toujours sur le meme quadruplet (unite, type, tarif, canal) sur une
/// periode. Les separer en sept tables obligerait a repeter ce quadruplet sept fois et surtout a
/// les relire sept fois a chaque recherche de disponibilite, qui est le chemin le plus chaud du
/// produit.
///
/// PORTEE : chaque dimension nulle signifie TOUTES. Une ligne sans type ferme tout l'hotel ; une
/// ligne avec type et sans tarif ferme ce type sur tous les tarifs. C'est ce qui permet de fermer
/// un hotel d'un geste et de rouvrir une exception ensuite.
///
/// CUMUL : plusieurs lignes peuvent couvrir la meme nuit. La regle appliquee est alors LA PLUS
/// RESTRICTIVE de toutes (voir <see cref="RestrictionSet"/>) - jamais la derniere saisie, jamais
/// la plus precise. Une fermeture posee sur l'hotel ne peut donc pas etre contournee par une
/// ligne plus fine, ce qui est exactement ce qu'on attend d'un stop sell.
///
/// BORNES INCLUSIVES sur les deux cotes : une regle du 1er au 15 aout couvre la nuit du 15. C'est
/// la meme convention que <c>RatePeriod</c>, avec laquelle ces regles se lisent cote a cote.
/// </summary>
public sealed class RateRestriction : AuditableEntity
{
    public const int NotesMaxLength = 500;

    /// <summary>Au-dela, ce n'est plus une duree de sejour mais une erreur de saisie.</summary>
    public const int MaxLengthOfStayBound = 365;

    /// <summary>Au-dela, ce n'est plus un delai de reservation mais une erreur de saisie.</summary>
    public const int MaxAdvanceDaysBound = 1095;

    private RateRestriction()
    {
    }

    public RateRestriction(
        string hotelUnitCode,
        DateOnly fromDate,
        DateOnly toDate,
        string? roomTypeCode = null,
        string? ratePlanCode = null,
        string? channelCode = null)
    {
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        RoomTypeCode = LodgingText.OptionalCode(roomTypeCode, nameof(roomTypeCode));
        RatePlanCode = LodgingText.OptionalCode(ratePlanCode, nameof(ratePlanCode));
        ChannelCode = LodgingText.OptionalCode(channelCode, nameof(channelCode));
        IsActive = true;

        ApplyPeriod(fromDate, toDate);
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    /// <summary>Type vise. Null = tous les types de l'unite.</summary>
    public string? RoomTypeCode { get; private set; }

    /// <summary>Plan tarifaire vise. Null = tous les plans.</summary>
    public string? RatePlanCode { get; private set; }

    /// <summary>Canal de distribution vise. Null = tous les canaux.</summary>
    public string? ChannelCode { get; private set; }

    public DateOnly FromDate { get; private set; }

    /// <summary>Derniere date couverte, INCLUSE.</summary>
    public DateOnly ToDate { get; private set; }

    /// <summary>Stop sell : aucune nuit de la periode ne peut etre vendue.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>
    /// CTA : aucun sejour ne peut COMMENCER une date couverte. Les clients deja presents
    /// poursuivent leur sejour sans etre inquietes - c'est toute la difference avec un stop sell.
    /// </summary>
    public bool IsClosedToArrival { get; private set; }

    /// <summary>
    /// CTD : aucun sejour ne peut SE TERMINER une date couverte. Sert a empecher les departs un
    /// jour de forte arrivee, pour ne pas se retrouver avec un parc a nettoyer d'un coup.
    /// </summary>
    public bool IsClosedToDeparture { get; private set; }

    /// <summary>Duree minimale de sejour, en nuits. Zero = pas de minimum.</summary>
    public int MinimumStay { get; private set; }

    /// <summary>Duree maximale de sejour, en nuits. Zero = pas de maximum.</summary>
    public int MaximumStay { get; private set; }

    /// <summary>Delai minimal entre la reservation et l'arrivee, en jours. Zero = aucun.</summary>
    public int MinAdvanceDays { get; private set; }

    /// <summary>Delai maximal entre la reservation et l'arrivee, en jours. Zero = aucun.</summary>
    public int MaxAdvanceDays { get; private set; }

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>Vrai quand la ligne ne restreint rien : elle est alors inutile et le service la refuse.</summary>
    public bool IsEmpty =>
        !IsClosed
        && !IsClosedToArrival
        && !IsClosedToDeparture
        && MinimumStay == 0
        && MaximumStay == 0
        && MinAdvanceDays == 0
        && MaxAdvanceDays == 0;

    /// <summary>La regle couvre-t-elle cette date ? Bornes incluses.</summary>
    public bool Covers(DateOnly date)
    {
        return IsActive && FromDate <= date && date <= ToDate;
    }

    /// <summary>
    /// La regle s'applique-t-elle a cette vente ? Une dimension nulle sur la regle accepte tout ;
    /// une dimension nulle sur la DEMANDE (canal inconnu, par exemple) ne fait match que sur une
    /// regle elle-meme sans canal - une regle ciblee ne doit pas s'appliquer par defaut.
    /// </summary>
    public bool AppliesTo(string? roomTypeCode, string? ratePlanCode, string? channelCode)
    {
        return Matches(RoomTypeCode, roomTypeCode)
            && Matches(RatePlanCode, ratePlanCode)
            && Matches(ChannelCode, channelCode);
    }

    public void SetRules(
        bool isClosed,
        bool isClosedToArrival,
        bool isClosedToDeparture,
        int minimumStay,
        int maximumStay,
        int minAdvanceDays,
        int maxAdvanceDays)
    {
        var min = LodgingText.Count(minimumStay, nameof(minimumStay), MaxLengthOfStayBound);
        var max = LodgingText.Count(maximumStay, nameof(maximumStay), MaxLengthOfStayBound);

        if (min > 0 && max > 0 && min > max)
        {
            throw new ArgumentException(
                "La duree minimale de sejour ne peut pas depasser la duree maximale.",
                nameof(minimumStay));
        }

        var minAdvance = LodgingText.Count(minAdvanceDays, nameof(minAdvanceDays), MaxAdvanceDaysBound);
        var maxAdvance = LodgingText.Count(maxAdvanceDays, nameof(maxAdvanceDays), MaxAdvanceDaysBound);

        if (minAdvance > 0 && maxAdvance > 0 && minAdvance > maxAdvance)
        {
            throw new ArgumentException(
                "Le delai minimal de reservation ne peut pas depasser le delai maximal.",
                nameof(minAdvanceDays));
        }

        IsClosed = isClosed;
        IsClosedToArrival = isClosedToArrival;
        IsClosedToDeparture = isClosedToDeparture;
        MinimumStay = min;
        MaximumStay = max;
        MinAdvanceDays = minAdvance;
        MaxAdvanceDays = maxAdvance;
    }

    public void Reschedule(DateOnly fromDate, DateOnly toDate)
    {
        ApplyPeriod(fromDate, toDate);
    }

    public void SetScope(string? roomTypeCode, string? ratePlanCode, string? channelCode)
    {
        RoomTypeCode = LodgingText.OptionalCode(roomTypeCode, nameof(roomTypeCode));
        RatePlanCode = LodgingText.OptionalCode(ratePlanCode, nameof(ratePlanCode));
        ChannelCode = LodgingText.OptionalCode(channelCode, nameof(channelCode));
    }

    public void SetNotes(string? notes)
    {
        Notes = LodgingText.Optional(notes, nameof(notes), NotesMaxLength);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    private void ApplyPeriod(DateOnly fromDate, DateOnly toDate)
    {
        if (fromDate > toDate)
        {
            throw new ArgumentException(
                "La date de debut ne peut pas etre posterieure a la date de fin.",
                nameof(fromDate));
        }

        FromDate = fromDate;
        ToDate = toDate;
    }

    private static bool Matches(string? ruleValue, string? requestValue)
    {
        if (ruleValue is null)
        {
            return true;
        }

        return requestValue is not null
            && string.Equals(ruleValue, requestValue.Trim().ToUpperInvariant(), StringComparison.Ordinal);
    }
}
