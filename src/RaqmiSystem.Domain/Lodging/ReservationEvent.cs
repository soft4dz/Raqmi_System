namespace RaqmiSystem.Domain.Lodging;

/// <summary>Nature d'un evenement du journal de sejour.</summary>
public enum ReservationEventKind
{
    Created = 0,
    StatusChanged = 1,
    RoomAssigned = 2,
    RoomReleased = 3,
    RoomMoved = 4,
    Upgraded = 5,
    Downgraded = 6,
    DatesChanged = 7,
    RateChanged = 8,
    GuestMixChanged = 9,
    GuaranteeChanged = 10,
    CancellationPolicyApplied = 11,
    FolioCharged = 12,
    DepositRecorded = 13,
    EarlyCheckInApplied = 14,
    LateCheckOutApplied = 15,
    Note = 16
}

/// <summary>
/// Une ligne du journal metier d'un sejour : ce qui a change, quand, par qui, et quelles etaient
/// l'ancienne et la nouvelle valeur.
///
/// POURQUOI CE JOURNAL EXISTE ALORS QU'IL Y A DEJA UN AUDIT TRAIL. L'audit global repond a "qui a
/// fait quoi dans le systeme" et se lit par filtre technique ; ce journal-ci repond a "que s'est-il
/// passe sur CE sejour", se lit d'un coup d'oeil au comptoir a cote de la reservation, et survit a
/// la purge de l'audit. Un client qui conteste un surclassement facture ou un changement de
/// chambre attend la seconde reponse, pas la premiere.
///
/// LES LIGNES SONT IMMUABLES. On n'ecrase jamais une entree : un changement de chambre A -> B -> C
/// laisse deux lignes, pas une valeur finale.
/// </summary>
public sealed class ReservationEvent
{
    public const int SummaryMaxLength = 400;
    public const int ValueMaxLength = 200;

    private ReservationEvent()
    {
    }

    public ReservationEvent(
        Guid reservationId,
        ReservationEventKind kind,
        string summary,
        DateTimeOffset occurredAt,
        string actor,
        string? previousValue = null,
        string? newValue = null,
        DateOnly? businessDate = null)
    {
        if (reservationId == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant de la reservation est requis.", nameof(reservationId));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Nature d'evenement inconnue.");
        }

        ReservationId = reservationId;
        Kind = kind;
        Summary = LodgingText.Require(summary, nameof(summary), SummaryMaxLength);
        OccurredAt = occurredAt;
        Actor = LodgingText.Actor(actor);
        PreviousValue = LodgingText.Optional(previousValue, nameof(previousValue), ValueMaxLength);
        NewValue = LodgingText.Optional(newValue, nameof(newValue), ValueMaxLength);
        BusinessDate = businessDate;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ReservationId { get; private set; }

    public ReservationEventKind Kind { get; private set; }

    /// <summary>Phrase lisible au comptoir : "Chambre 101 -> 205, degat des eaux".</summary>
    public string Summary { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Date metier hoteliere du geste, quand elle est connue. Voir <see cref="BusinessDay"/>.</summary>
    public DateOnly? BusinessDate { get; private set; }

    public string Actor { get; private set; } = "system";

    /// <summary>Ancienne valeur, quand le geste en remplace une.</summary>
    public string? PreviousValue { get; private set; }

    /// <summary>Nouvelle valeur, quand le geste en pose une.</summary>
    public string? NewValue { get; private set; }
}
