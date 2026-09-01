using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Retrait d'UNE chambre de l'exploitation sur une periode, en hors service technique
/// (<see cref="RoomBlockKind.OutOfOrder"/>) ou en indisponibilite d'exploitation
/// (<see cref="RoomBlockKind.OutOfService"/>).
///
/// POURQUOI UNE ENTITE ET PAS UN INDICATEUR SUR LA CHAMBRE. Un indicateur ne repond qu'a la
/// question "aujourd'hui" ; une periode repond a "la nuit du 14 aout", qui est la seule question
/// que la disponibilite pose. Sans dates, bloquer une chambre pour des travaux de la semaine
/// prochaine obligerait soit a la retirer tout de suite - et a perdre les nuits d'ici la - soit a
/// compter sur quelqu'un pour y penser le jour venu. Les deux se paient en chambres vendues qu'on
/// ne peut pas livrer.
///
/// PERIODE EN NUITS, DEMI-OUVERTE : [<see cref="StartDate"/>, <see cref="EndDate"/>) - la nuit du
/// jour de fin n'est PAS bloquee, exactement comme une reservation ne consomme pas la nuit de son
/// depart. Un blocage du 10 au 12 immobilise les nuits du 10 et du 11, et la chambre est
/// revendable des la nuit du 12.
///
/// L'INVARIANT DE NON-CHEVAUCHEMENT n'est pas porte ici : deux blocages ouverts de la meme chambre
/// sur la meme nuit ne cassent rien, la chambre est retiree une fois et non deux. Le service
/// refuse tout de meme le doublon exact, pour que l'ecran ne montre pas deux lignes pour un meme
/// fait.
/// </summary>
public sealed class RoomBlock : AuditableEntity
{
    public const int ReasonMaxLength = 300;
    public const int CommentMaxLength = 1000;
    public const int MaintenanceReferenceMaxLength = 60;

    /// <summary>Au-dela, ce n'est plus un blocage mais une sortie de parc : desactivez la chambre.</summary>
    public const int MaxNights = 3660;

    private RoomBlock()
    {
    }

    public RoomBlock(
        string hotelUnitCode,
        Guid roomId,
        RoomBlockKind kind,
        DateOnly startDate,
        DateOnly endDate,
        string reason,
        RoomBlockCategory category = RoomBlockCategory.Unspecified,
        string? maintenanceReference = null,
        string? comment = null)
    {
        if (roomId == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant de la chambre est requis.", nameof(roomId));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Nature de blocage inconnue.");
        }

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category), category, "Categorie de blocage inconnue.");
        }

        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        RoomId = roomId;
        Kind = kind;
        Category = category;
        Reason = LodgingText.Require(reason, nameof(reason), ReasonMaxLength);
        MaintenanceReference = LodgingText.Optional(
            maintenanceReference,
            nameof(maintenanceReference),
            MaintenanceReferenceMaxLength);
        Comment = LodgingText.Optional(comment, nameof(comment), CommentMaxLength);
        Status = RoomBlockStatus.Planned;

        ApplyPeriod(startDate, endDate);
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    public Guid RoomId { get; private set; }

    public RoomBlockKind Kind { get; private set; }

    public RoomBlockCategory Category { get; private set; }

    public DateOnly StartDate { get; private set; }

    /// <summary>Date de fin PREVUE, exclue : la nuit de ce jour est deja revendable.</summary>
    public DateOnly EndDate { get; private set; }

    /// <summary>
    /// Date reelle de remise en service, renseignee a la cloture. Elle peut tomber avant ou apres
    /// la fin prevue - une panne se repare rarement le jour annonce - et c'est elle qui fait foi
    /// une fois le blocage cloture.
    /// </summary>
    public DateOnly? ActualReturnDate { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    /// <summary>Reference de l'intervention de maintenance associee, quand il y en a une.</summary>
    public string? MaintenanceReference { get; private set; }

    public string? Comment { get; private set; }

    public RoomBlockStatus Status { get; private set; } = RoomBlockStatus.Planned;

    public DateTimeOffset? ClosedAt { get; private set; }

    public string? ClosedBy { get; private set; }

    public string? CancelReason { get; private set; }

    /// <summary>Nombre de nuits immobilisees par la periode PREVUE.</summary>
    public int Nights => EndDate.DayNumber - StartDate.DayNumber;

    /// <summary>
    /// Vrai quand le blocage retire encore des nuits de l'exploitation : programme ou en cours.
    /// Un blocage cloture ou annule ne retire plus rien.
    /// </summary>
    public bool IsBlocking => Status is RoomBlockStatus.Planned or RoomBlockStatus.Active;

    /// <summary>
    /// La nuit est-elle immobilisee par ce blocage, du point de vue des INDICATEURS ? La borne
    /// haute est la date de retour REELLE quand elle existe - le blocage est termine, on sait
    /// jusqu'ou il a couru - sinon la fin prevue. Un blocage annule n'a jamais rien immobilise.
    /// </summary>
    public bool CoversNight(DateOnly night)
    {
        if (Status == RoomBlockStatus.Cancelled)
        {
            return false;
        }

        var effectiveEnd = ActualReturnDate ?? EndDate;

        return StartDate <= night && night < effectiveEnd;
    }

    /// <summary>
    /// La nuit est-elle retiree DE LA VENTE ? Seuls les blocages encore ouverts le font : une
    /// chambre remise en service redevient vendable, meme si ses nuits passees restent comptees
    /// comme immobilisees par <see cref="CoversNight"/>.
    /// </summary>
    public bool BlocksSaleOn(DateOnly night)
    {
        return IsBlocking && StartDate <= night && night < EndDate;
    }

    /// <summary>Deux periodes se chevauchent quand chacune commence avant la fin de l'autre.</summary>
    public bool Overlaps(DateOnly startDate, DateOnly endDate)
    {
        return StartDate < endDate && EndDate > startDate;
    }

    /// <summary>Passe le blocage en cours. Idempotent.</summary>
    public void Activate()
    {
        if (Status == RoomBlockStatus.Planned)
        {
            Status = RoomBlockStatus.Active;
        }
    }

    /// <summary>Deplace ou allonge la periode et corrige le motif. Refuse sur un blocage clos.</summary>
    public void Reschedule(
        DateOnly startDate,
        DateOnly endDate,
        string reason,
        RoomBlockCategory category,
        string? maintenanceReference,
        string? comment)
    {
        RequireOpen();

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category), category, "Categorie de blocage inconnue.");
        }

        ApplyPeriod(startDate, endDate);
        Reason = LodgingText.Require(reason, nameof(reason), ReasonMaxLength);
        Category = category;
        MaintenanceReference = LodgingText.Optional(
            maintenanceReference,
            nameof(maintenanceReference),
            MaintenanceReferenceMaxLength);
        Comment = LodgingText.Optional(comment, nameof(comment), CommentMaxLength);
    }

    /// <summary>
    /// Remet la chambre en service a la date indiquee. Celle-ci ne peut pas preceder le debut du
    /// blocage : une chambre ne revient pas avant d'etre partie.
    /// </summary>
    public void Close(DateOnly returnDate, string userName, DateTimeOffset utcNow)
    {
        RequireOpen();

        if (returnDate < StartDate)
        {
            throw new ArgumentException(
                "La date de remise en service ne peut pas preceder le debut du blocage.",
                nameof(returnDate));
        }

        ActualReturnDate = returnDate;
        Status = RoomBlockStatus.Closed;
        ClosedAt = utcNow;
        ClosedBy = LodgingText.Actor(userName);
    }

    /// <summary>Annule un blocage pose par erreur : la chambre redevient vendable sur toute la periode.</summary>
    public void CancelBlock(string reason, string userName, DateTimeOffset utcNow)
    {
        RequireOpen();

        CancelReason = LodgingText.Require(reason, nameof(reason), ReasonMaxLength);
        Status = RoomBlockStatus.Cancelled;
        ClosedAt = utcNow;
        ClosedBy = LodgingText.Actor(userName);
    }

    private void ApplyPeriod(DateOnly startDate, DateOnly endDate)
    {
        if (endDate <= startDate)
        {
            throw new ArgumentException(
                "La date de fin doit etre posterieure a la date de debut (un blocage couvre au moins une nuit).",
                nameof(endDate));
        }

        if (endDate.DayNumber - startDate.DayNumber > MaxNights)
        {
            throw new ArgumentException(
                $"Un blocage ne peut pas couvrir plus de {MaxNights} nuits. Au-dela, desactivez la chambre.",
                nameof(endDate));
        }

        StartDate = startDate;
        EndDate = endDate;
    }

    private void RequireOpen()
    {
        if (!IsBlocking)
        {
            throw new InvalidOperationException(
                "Ce blocage est cloture ou annule : il ne peut plus etre modifie.");
        }
    }
}
