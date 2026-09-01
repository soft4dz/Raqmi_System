using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Le compte d'un sejour. Un sejour en porte UN AU MOINS - le folio client, ouvert a l'arrivee -
/// et peut en porter plusieurs : chambre sur le compte societe, extras a la charge du client,
/// voucher agence a part.
///
/// POURQUOI PLUSIEURS FOLIOS ET PAS UNE VENTILATION AU DEPART. La ventilation se decide a la
/// vente, pas au comptoir : "la societe prend la chambre et le petit-dejeuner". Si le systeme ne
/// sait porter qu'un compte, la reception doit refaire ce partage a la main devant le client qui
/// attend, sur des dizaines de lignes, et l'erreur se retrouve en facture.
///
/// LE SOLDE N'EST QUE LA SOMME DES LIGNES : les nuits et les extras le font monter, les reglements
/// et les ajustements negatifs le ramenent, et le depart est refuse tant qu'il n'est pas
/// exactement zero sur TOUS les folios du sejour.
/// </summary>
public sealed class Folio : AuditableEntity
{
    public const int NumberMaxLength = 32;
    public const int LabelMaxLength = 160;

    private readonly List<FolioCharge> _charges = new();

    private Folio()
    {
    }

    public Folio(
        Guid reservationId,
        string hotelUnitCode,
        string number,
        FolioKind kind = FolioKind.Guest,
        string? billToCustomerCode = null,
        string? label = null)
    {
        if (reservationId == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant de la reservation est requis.", nameof(reservationId));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Nature de folio inconnue.");
        }

        ReservationId = reservationId;
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Number = LodgingText.RequireCode(number, nameof(number), NumberMaxLength);
        Kind = kind;
        BillToCustomerCode = billToCustomerCode is null
            ? null
            : Customer.NormalizeCode(billToCustomerCode);
        Label = LodgingText.Optional(label, nameof(label), LabelMaxLength);
        Status = FolioStatus.Open;
    }

    public Guid ReservationId { get; private set; }

    public string HotelUnitCode { get; private set; } = string.Empty;

    /// <summary>Numero du folio, unique dans l'unite. Cite au comptoir et repris sur la facture.</summary>
    public string Number { get; private set; } = string.Empty;

    public FolioKind Kind { get; private set; } = FolioKind.Guest;

    /// <summary>
    /// Client a facturer. Null signifie "le client du sejour", ce qui est le cas du folio client.
    /// Un folio societe ou agence porte ici le code du payeur reel.
    /// </summary>
    public string? BillToCustomerCode { get; private set; }

    /// <summary>Libelle libre ("Chambre + petit-dejeuner", "Extras").</summary>
    public string? Label { get; private set; }

    public FolioStatus Status { get; private set; } = FolioStatus.Open;

    public DateTimeOffset? ClosedAt { get; private set; }

    public string? ClosedBy { get; private set; }

    /// <summary>Facture produite depuis ce folio, quand il en existe une.</summary>
    public Guid? InvoiceId { get; private set; }

    public IReadOnlyCollection<FolioCharge> Charges => _charges.AsReadOnly();

    public decimal Balance => _charges.Sum(charge => charge.Amount);

    /// <summary>Total des prestations : tout sauf les reglements. Ce que le sejour a produit.</summary>
    public decimal TotalCharges => _charges
        .Where(charge => charge.Kind != ChargeKind.Settlement)
        .Sum(charge => charge.Amount);

    /// <summary>Total encaisse (en positif) : l'oppose de la somme des lignes de reglement.</summary>
    public decimal TotalSettlements => -_charges
        .Where(charge => charge.Kind == ChargeKind.Settlement)
        .Sum(charge => charge.Amount);

    public bool IsOpen => Status == FolioStatus.Open;

    /// <summary>
    /// Ajoute une ligne. Refusee sur un folio ferme : une correction posterieure passe par un
    /// avoir du module Facturation et non par une reecriture du compte.
    /// </summary>
    public void AddCharge(FolioCharge charge)
    {
        ArgumentNullException.ThrowIfNull(charge);

        if (!IsOpen)
        {
            throw new InvalidOperationException(
                "Ce folio est ferme : il n'accepte plus de ligne. Passez par un avoir.");
        }

        // Garde d'idempotence cote memoire. L'index unique en base est la garantie reelle ; ce
        // controle-ci evite d'aller jusqu'a la violation de contrainte quand les lignes sont deja
        // chargees, et rend le rejeu explicite plutot qu'accidentel.
        if (charge.SourceReference is { } source
            && _charges.Any(existing => existing.SourceReference == source))
        {
            throw new InvalidOperationException(
                $"Une ligne portant la reference de geste '{source}' existe deja sur ce folio.");
        }

        charge.SetLineNumber(_charges.Count + 1);
        _charges.Add(charge);
    }

    /// <summary>Vrai quand un geste a deja produit sa ligne sur ce folio.</summary>
    public bool HasCharge(string sourceReference)
    {
        return _charges.Any(charge => charge.SourceReference == sourceReference);
    }

    /// <summary>
    /// Ferme le folio. Refuse tant que le solde n'est pas nul : un compte ferme a decouvert est une
    /// creance que plus personne ne voit.
    /// </summary>
    public void Close(string userName, DateTimeOffset utcNow)
    {
        if (!IsOpen)
        {
            return;
        }

        if (Balance != 0m)
        {
            throw new InvalidOperationException(
                $"Le folio ne peut pas etre ferme tant que son solde n'est pas nul (solde actuel : {Balance:0.00}).");
        }

        Status = FolioStatus.Closed;
        ClosedAt = utcNow;
        ClosedBy = LodgingText.Actor(userName);
    }

    /// <summary>Rattache la facture produite depuis ce folio.</summary>
    public void AttachInvoice(Guid invoiceId)
    {
        if (invoiceId == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant de la facture est requis.", nameof(invoiceId));
        }

        InvoiceId = invoiceId;
    }

    /// <summary>Change le payeur et le libelle du folio, tant qu'il est ouvert.</summary>
    public void Retarget(string? billToCustomerCode, string? label)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("Un folio ferme ne peut plus changer de destinataire.");
        }

        BillToCustomerCode = billToCustomerCode is null ? null : Customer.NormalizeCode(billToCustomerCode);
        Label = LodgingText.Optional(label, nameof(label), LabelMaxLength);
    }
}
