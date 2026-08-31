using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Mice;

/// <summary>
/// Un bloc de chambres tenu pour un groupe : N chambres d'un TYPE donne, sur une periode, jusqu'a
/// une date de release au-dela de laquelle le solde retourne a la vente.
///
/// CE QUE CET OBJET CHANGE DANS LE PRODUIT, ET POURQUOI IL EST DELICAT. Un allotement retire des
/// chambres de la vente publique SANS les nommer : on ne sait pas encore quelle chambre ira a quel
/// participant. La disponibilite doit donc soustraire ce solde, et la creation d'une reservation
/// publique doit refuser d'entamer le bloc. Les deux chemins - recherche ET creation - doivent
/// appliquer la meme regle : n'en couvrir qu'un ferait survendre l'hotel en silence, la recherche
/// affichant moins de chambres que la creation n'en accepte.
///
/// LE BLOC RAISONNE PAR TYPE, PAS PAR CHAMBRE. C'est ce qui permet de tenir "12 doubles" sans
/// bloquer douze numeros precis deux mois a l'avance, et c'est aussi pourquoi le controle ne peut
/// pas se faire par un simple chevauchement de reservation : il faut compter, nuit par nuit.
/// </summary>
public sealed class RoomAllotment : AuditableEntity
{
    public const int ReferenceMaxLength = 24;
    public const int CustomerCodeMaxLength = 32;
    public const int NotesMaxLength = 1000;
    public const int CancelReasonMaxLength = 300;

    /// <summary>Au-dela, c'est une erreur de saisie : aucun groupe ne tient mille chambres.</summary>
    public const int MaxRoomsHeld = 999;

    private RoomAllotment()
    {
    }

    public RoomAllotment(
        string hotelUnitCode,
        string reference,
        string customerCode,
        string roomTypeCode,
        DateOnly arrivalDate,
        DateOnly departureDate,
        int roomsHeld,
        DateOnly? releaseDate,
        string? notes = null)
    {
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Reference = RequireText(reference, nameof(reference), ReferenceMaxLength).ToUpperInvariant();
        CustomerCode = RequireText(customerCode, nameof(customerCode), CustomerCodeMaxLength).ToUpperInvariant();
        RoomTypeCode = RoomType.NormalizeCode(roomTypeCode);
        Notes = NormalizeOptional(notes, NotesMaxLength);
        Status = RoomAllotmentStatus.Draft;

        ApplyBlock(arrivalDate, departureDate, roomsHeld, releaseDate);
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    /// <summary>Reference du bloc, unique dans l'unite. Citee au telephone et sur le contrat.</summary>
    public string Reference { get; private set; } = string.Empty;

    /// <summary>Client porteur du groupe : agence, entreprise, organisateur.</summary>
    public string CustomerCode { get; private set; } = string.Empty;

    public string RoomTypeCode { get; private set; } = string.Empty;

    public DateOnly ArrivalDate { get; private set; }

    /// <summary>Date de depart, EXCLUE : un bloc du 10 au 12 couvre les nuits du 10 et du 11.</summary>
    public DateOnly DepartureDate { get; private set; }

    /// <summary>Nombre de chambres tenues pour le groupe.</summary>
    public int RoomsHeld { get; private set; }

    /// <summary>
    /// Date limite au-dela de laquelle le solde non consomme retourne a la vente. Null signifie que
    /// le bloc tient jusqu'au depart - un engagement ferme, a n'utiliser qu'en connaissance de
    /// cause, puisque des chambres invendues resteront bloquees jusqu'au bout.
    /// </summary>
    public DateOnly? ReleaseDate { get; private set; }

    public RoomAllotmentStatus Status { get; private set; } = RoomAllotmentStatus.Draft;

    public string? Notes { get; private set; }

    public string? CancelReason { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public string? ConfirmedBy { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public string? ClosedBy { get; private set; }

    /// <summary>Nombre de nuits couvertes par le bloc.</summary>
    public int Nights => DepartureDate.DayNumber - ArrivalDate.DayNumber;

    /// <summary>Un bloc annule ou libere ne tient plus rien.</summary>
    public bool IsOpen => Status is RoomAllotmentStatus.Draft or RoomAllotmentStatus.Confirmed;

    /// <summary>
    /// Le bloc tient-il encore des chambres la nuit demandee, vu de la date <paramref name="asOf"/> ?
    ///
    /// La date d'observation compte : passee la date de release, le solde est rendu, et une nuit
    /// pourtant comprise dans la periode cesse d'etre tenue. C'est bien le comportement voulu -
    /// c'est tout l'objet d'une date de release - mais cela signifie que la disponibilite d'une
    /// meme nuit peut changer d'un jour a l'autre sans qu'aucune reservation n'ait bouge.
    /// </summary>
    public bool IsHoldingOn(DateOnly night, DateOnly asOf)
    {
        if (!IsOpen)
        {
            return false;
        }

        if (night < ArrivalDate || night >= DepartureDate)
        {
            return false;
        }

        return ReleaseDate is not { } release || asOf <= release;
    }

    /// <summary>Modifie le bloc. L'appelant DOIT revalider les reservations deja prises dessus.</summary>
    public void UpdateBlock(
        DateOnly arrivalDate,
        DateOnly departureDate,
        int roomsHeld,
        DateOnly? releaseDate,
        string? notes)
    {
        RequireOpen();
        ApplyBlock(arrivalDate, departureDate, roomsHeld, releaseDate);
        Notes = NormalizeOptional(notes, NotesMaxLength);
    }

    public void Confirm(string userName, DateTimeOffset utcNow)
    {
        RequireOpen();

        if (Status == RoomAllotmentStatus.Confirmed)
        {
            return;
        }

        Status = RoomAllotmentStatus.Confirmed;
        ConfirmedAt = utcNow;
        ConfirmedBy = RequireText(userName, nameof(userName), 160);
    }

    /// <summary>
    /// Rend le SOLDE a la vente avant terme. Les chambres deja prises sur le bloc restent
    /// reservees : liberer un allotement ne desengage personne.
    /// </summary>
    public void Release(string userName, DateTimeOffset utcNow)
    {
        RequireOpen();

        Status = RoomAllotmentStatus.Released;
        ClosedAt = utcNow;
        ClosedBy = RequireText(userName, nameof(userName), 160);
    }

    /// <summary>
    /// Annule le bloc entier. Refuse tant que des reservations y sont rattachees : l'appelant doit
    /// d'abord les traiter, faute de quoi elles pointeraient vers un bloc inexistant.
    /// </summary>
    public void Cancel(string reason, string userName, DateTimeOffset utcNow)
    {
        if (Status == RoomAllotmentStatus.Cancelled)
        {
            throw new InvalidOperationException("Cet allotement est deja annule.");
        }

        Status = RoomAllotmentStatus.Cancelled;
        CancelReason = RequireText(reason, nameof(reason), CancelReasonMaxLength);
        ClosedAt = utcNow;
        ClosedBy = RequireText(userName, nameof(userName), 160);
    }

    private void ApplyBlock(DateOnly arrivalDate, DateOnly departureDate, int roomsHeld, DateOnly? releaseDate)
    {
        if (departureDate <= arrivalDate)
        {
            throw new ArgumentException(
                "La date de depart doit etre posterieure a la date d'arrivee.",
                nameof(departureDate));
        }

        if (roomsHeld <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(roomsHeld),
                "Le nombre de chambres tenues doit etre strictement positif.");
        }

        if (roomsHeld > MaxRoomsHeld)
        {
            throw new ArgumentOutOfRangeException(
                nameof(roomsHeld),
                $"Le nombre de chambres tenues ne peut pas depasser {MaxRoomsHeld}.");
        }

        // Une date de release posterieure a l'arrivee n'a pas de sens : le release sert a rendre le
        // solde AVANT que le groupe n'arrive, pour pouvoir revendre.
        if (releaseDate is { } release && release > arrivalDate)
        {
            throw new ArgumentException(
                "La date de release doit etre anterieure ou egale a la date d'arrivee.",
                nameof(releaseDate));
        }

        ArrivalDate = arrivalDate;
        DepartureDate = departureDate;
        RoomsHeld = roomsHeld;
        ReleaseDate = releaseDate;
    }

    private void RequireOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException(
                "Cet allotement est cloture : il ne tient plus de chambres et ne peut plus etre modifie.");
        }
    }

    private static string RequireText(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("La valeur est requise.", parameterName);
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"La valeur ne peut pas depasser {maxLength} caracteres.", parameterName);
        }

        return trimmed;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
