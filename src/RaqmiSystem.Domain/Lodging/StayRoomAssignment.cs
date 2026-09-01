namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Une chambre reellement occupee par un sejour, sur une plage de temps. Un sejour deplace deux
/// fois porte trois lignes : 101, puis 205, puis 310.
///
/// POURQUOI L'HISTORIQUE NE PEUT PAS ETRE DEDUIT DU JOURNAL. Le journal de sejour dit "le client a
/// change de chambre" ; cette table dit "il a dormi en 101 les nuits du 10 et du 11". C'est la
/// seconde forme dont ont besoin le housekeeping (quelle chambre passer en sale, et quand), les
/// objets trouves, la facturation d'une degradation et la fiche de police. La deduire d'un journal
/// textuel a chaque lecture serait fragile et lent.
///
/// LA LIGNE COURANTE est celle dont <see cref="ReleasedAt"/> est nulle. Il ne peut y en avoir
/// qu'une par sejour : la liberer est le premier geste de tout deplacement.
/// </summary>
public sealed class StayRoomAssignment
{
    public const int ReasonMaxLength = 300;

    private StayRoomAssignment()
    {
    }

    public StayRoomAssignment(
        Guid reservationId,
        Guid roomId,
        string roomNumber,
        string roomTypeCode,
        DateTimeOffset assignedAt,
        string actor,
        string? reason = null)
    {
        if (reservationId == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant de la reservation est requis.", nameof(reservationId));
        }

        if (roomId == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant de la chambre est requis.", nameof(roomId));
        }

        ReservationId = reservationId;
        RoomId = roomId;
        RoomNumber = LodgingText.Require(roomNumber, nameof(roomNumber), 20).ToUpperInvariant();
        RoomTypeCode = RoomType.NormalizeCode(roomTypeCode);
        AssignedAt = assignedAt;
        AssignedBy = LodgingText.Actor(actor);
        Reason = LodgingText.Optional(reason, nameof(reason), ReasonMaxLength);
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ReservationId { get; private set; }

    public Guid RoomId { get; private set; }

    /// <summary>
    /// Numero de la chambre AU MOMENT de l'affectation, fige. Une renumerotation du parc ne doit
    /// pas reecrire l'historique d'un sejour deja passe.
    /// </summary>
    public string RoomNumber { get; private set; } = string.Empty;

    /// <summary>Type de la chambre au moment de l'affectation, fige pour la meme raison.</summary>
    public string RoomTypeCode { get; private set; } = string.Empty;

    public DateTimeOffset AssignedAt { get; private set; }

    public string AssignedBy { get; private set; } = "system";

    /// <summary>Null tant que la chambre est celle du sejour.</summary>
    public DateTimeOffset? ReleasedAt { get; private set; }

    public string? ReleasedBy { get; private set; }

    /// <summary>Motif du deplacement ou de l'affectation, quand il y en a un.</summary>
    public string? Reason { get; private set; }

    /// <summary>Vrai tant que le sejour occupe cette chambre.</summary>
    public bool IsCurrent => ReleasedAt is null;

    /// <summary>Ferme la ligne. Idempotent : reliberer une ligne deja liberee ne change rien.</summary>
    public void Release(DateTimeOffset releasedAt, string actor, string? reason = null)
    {
        if (ReleasedAt is not null)
        {
            return;
        }

        ReleasedAt = releasedAt;
        ReleasedBy = LodgingText.Actor(actor);

        if (!string.IsNullOrWhiteSpace(reason))
        {
            Reason = LodgingText.Optional(reason, nameof(reason), ReasonMaxLength);
        }
    }
}
