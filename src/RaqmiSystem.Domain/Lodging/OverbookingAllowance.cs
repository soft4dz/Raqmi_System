using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Autorisation de vendre AU-DELA de la capacite physique, pour un type de chambre et sur une
/// periode. C'est la seule facon d'ouvrir une survente dans ce produit : sans ligne, la vente
/// s'arrete a la derniere chambre reelle.
///
/// POURQUOI UNE AUTORISATION EXPLICITE ET DATEE. La surreservation est une prise de risque
/// commerciale assumee - on parie sur les annulations et les no-shows - et elle se decide par
/// periode et par type, jamais globalement. Un hotel accepte +2 doubles en semaine et zero le
/// week-end du 15 aout. Un simple indicateur "surreservation autorisee" ne saurait pas dire cela,
/// et un pourcentage global ferait vendre des suites qu'on ne peut ni deplacer ni compenser.
///
/// LA VENTE EN SURRESERVATION EST TRACEE : la reservation qui franchit la capacite physique porte
/// <c>Reservation.IsOverbooking</c>. Sans cette marque, la reception decouvrirait le jour meme
/// qu'elle a une chambre de trop a reloger, sans savoir laquelle ni pourquoi.
///
/// BORNES INCLUSIVES, comme <see cref="RateRestriction"/> et <c>RatePeriod</c>.
/// </summary>
public sealed class OverbookingAllowance : AuditableEntity
{
    public const int NotesMaxLength = 500;

    /// <summary>
    /// Plafond dur : au-dela, ce n'est plus une survente maitrisee mais une vente a decouvert que
    /// l'hotel ne pourra pas reloger.
    /// </summary>
    public const int MaxExtraRooms = 50;

    private OverbookingAllowance()
    {
    }

    public OverbookingAllowance(
        string hotelUnitCode,
        string roomTypeCode,
        DateOnly fromDate,
        DateOnly toDate,
        int extraRooms,
        string? notes = null)
    {
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        RoomTypeCode = RoomType.NormalizeCode(roomTypeCode);
        Notes = LodgingText.Optional(notes, nameof(notes), NotesMaxLength);
        IsActive = true;

        ApplyTerms(fromDate, toDate, extraRooms);
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    /// <summary>
    /// Type autorise en survente. Obligatoire, contrairement aux restrictions : une survente
    /// "tous types confondus" n'a pas de sens operationnel, on ne reloge pas une suite dans une
    /// single.
    /// </summary>
    public string RoomTypeCode { get; private set; } = string.Empty;

    public DateOnly FromDate { get; private set; }

    /// <summary>Derniere nuit couverte, INCLUSE.</summary>
    public DateOnly ToDate { get; private set; }

    /// <summary>Nombre de chambres vendables en plus de la capacite physique.</summary>
    public int ExtraRooms { get; private set; }

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>L'autorisation couvre-t-elle cette nuit ?</summary>
    public bool Covers(DateOnly night)
    {
        return IsActive && FromDate <= night && night <= ToDate;
    }

    /// <summary>Deux autorisations du meme type se chevauchent-elles ? Bornes incluses.</summary>
    public bool Overlaps(OverbookingAllowance other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return HotelUnitCode == other.HotelUnitCode
            && RoomTypeCode == other.RoomTypeCode
            && FromDate <= other.ToDate
            && other.FromDate <= ToDate;
    }

    public void UpdateTerms(DateOnly fromDate, DateOnly toDate, int extraRooms, string? notes)
    {
        ApplyTerms(fromDate, toDate, extraRooms);
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

    private void ApplyTerms(DateOnly fromDate, DateOnly toDate, int extraRooms)
    {
        if (fromDate > toDate)
        {
            throw new ArgumentException(
                "La date de debut ne peut pas etre posterieure a la date de fin.",
                nameof(fromDate));
        }

        if (extraRooms <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(extraRooms),
                extraRooms,
                "Une autorisation de surreservation doit ouvrir au moins une chambre. Pour fermer la "
                + "surreservation, desactivez la ligne.");
        }

        FromDate = fromDate;
        ToDate = toDate;
        ExtraRooms = LodgingText.Count(extraRooms, nameof(extraRooms), MaxExtraRooms);
    }
}
