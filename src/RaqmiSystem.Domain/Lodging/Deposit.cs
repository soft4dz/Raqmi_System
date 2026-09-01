using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Lodging;

/// <summary>Cycle de vie d'un acompte.</summary>
public enum DepositStatus
{
    /// <summary>Demande : l'hotel l'exige, le client ne l'a pas encore verse.</summary>
    Requested = 0,

    /// <summary>Verse : l'argent est encaisse et attend d'etre impute.</summary>
    Paid = 1,

    /// <summary>Impute au folio du sejour : il a reduit le solde a payer.</summary>
    Applied = 2,

    /// <summary>Rembourse au client.</summary>
    Refunded = 3,

    /// <summary>Conserve par l'hotel a titre de penalite (annulation tardive, no-show).</summary>
    Forfeited = 4
}

/// <summary>
/// Un acompte attache a une reservation.
///
/// POURQUOI CE N'EST PAS UN SIMPLE ENCAISSEMENT. Un acompte est de l'argent recu AVANT toute
/// prestation : tant qu'il n'est pas impute, il ne doit pas apparaitre comme un reglement du
/// sejour, sinon le folio afficherait un solde negatif pour une chambre pas encore occupee. Son
/// cycle - demande, verse, impute, rembourse ou conserve - est ce qui permet de repondre a la
/// seule question qui compte a l'annulation : est-ce que cet argent revient au client ou reste a
/// l'hotel ?
///
/// LE MOUVEMENT DE CAISSE RESTE AU MODULE TRESORERIE. Cette entite porte la reference de la piece
/// et son etat vis-a-vis du sejour ; elle ne cree aucune ecriture, ce que ferait un second moteur
/// d'encaissement.
/// </summary>
public sealed class Deposit : AuditableEntity
{
    public const int ReferenceMaxLength = 100;
    public const int PaymentMethodMaxLength = 40;
    public const int NotesMaxLength = 500;

    private Deposit()
    {
    }

    public Deposit(Guid reservationId, decimal amount, DateOnly dueDate, string? notes = null)
    {
        if (reservationId == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant de la reservation est requis.", nameof(reservationId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Un acompte doit etre strictement positif.");
        }

        ReservationId = reservationId;
        Amount = LodgingText.Money(amount, nameof(amount));
        DueDate = dueDate;
        Notes = LodgingText.Optional(notes, nameof(notes), NotesMaxLength);
        Status = DepositStatus.Requested;
    }

    public Guid ReservationId { get; private set; }

    public decimal Amount { get; private set; }

    /// <summary>Date a laquelle l'acompte est attendu.</summary>
    public DateOnly DueDate { get; private set; }

    public DepositStatus Status { get; private set; } = DepositStatus.Requested;

    public DateOnly? PaidDate { get; private set; }

    /// <summary>Moyen de paiement (ESPECES, CB, VIREMENT, CHEQUE).</summary>
    public string? PaymentMethod { get; private set; }

    /// <summary>Reference de la piece de tresorerie qui porte l'encaissement.</summary>
    public string? Reference { get; private set; }

    /// <summary>Folio sur lequel l'acompte a ete impute.</summary>
    public Guid? AppliedToFolioId { get; private set; }

    public DateTimeOffset? AppliedAt { get; private set; }

    public string? AppliedBy { get; private set; }

    public DateOnly? RefundedDate { get; private set; }

    public string? ClosingReason { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>Vrai quand l'argent est acquis a l'hotel et disponible pour imputation.</summary>
    public bool IsAvailableForApplication => Status == DepositStatus.Paid;

    /// <summary>Enregistre le versement.</summary>
    public void MarkPaid(DateOnly paidDate, string paymentMethod, string? reference)
    {
        if (Status != DepositStatus.Requested)
        {
            throw new InvalidOperationException("Seul un acompte demande peut etre enregistre comme verse.");
        }

        PaidDate = paidDate;
        PaymentMethod = LodgingText.RequireCode(paymentMethod, nameof(paymentMethod), PaymentMethodMaxLength);
        Reference = LodgingText.Optional(reference, nameof(reference), ReferenceMaxLength);
        Status = DepositStatus.Paid;
    }

    /// <summary>Impute l'acompte au folio : le folio recoit en contrepartie une ligne de reglement.</summary>
    public void ApplyTo(Guid folioId, string userName, DateTimeOffset utcNow)
    {
        if (Status != DepositStatus.Paid)
        {
            throw new InvalidOperationException("Seul un acompte verse peut etre impute a un folio.");
        }

        if (folioId == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant du folio est requis.", nameof(folioId));
        }

        AppliedToFolioId = folioId;
        AppliedAt = utcNow;
        AppliedBy = LodgingText.Actor(userName);
        Status = DepositStatus.Applied;
    }

    /// <summary>Rembourse l'acompte au client.</summary>
    public void Refund(DateOnly refundedDate, string reason)
    {
        if (Status != DepositStatus.Paid)
        {
            throw new InvalidOperationException(
                "Seul un acompte verse et non impute peut etre rembourse.");
        }

        RefundedDate = refundedDate;
        ClosingReason = LodgingText.Require(reason, nameof(reason), NotesMaxLength);
        Status = DepositStatus.Refunded;
    }

    /// <summary>Conserve l'acompte a titre de penalite.</summary>
    public void Forfeit(string reason)
    {
        if (Status != DepositStatus.Paid)
        {
            throw new InvalidOperationException("Seul un acompte verse et non impute peut etre conserve.");
        }

        ClosingReason = LodgingText.Require(reason, nameof(reason), NotesMaxLength);
        Status = DepositStatus.Forfeited;
    }

    public void Reschedule(decimal amount, DateOnly dueDate, string? notes)
    {
        if (Status != DepositStatus.Requested)
        {
            throw new InvalidOperationException("Seul un acompte encore demande peut etre modifie.");
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Un acompte doit etre strictement positif.");
        }

        Amount = LodgingText.Money(amount, nameof(amount));
        DueDate = dueDate;
        Notes = LodgingText.Optional(notes, nameof(notes), NotesMaxLength);
    }
}
