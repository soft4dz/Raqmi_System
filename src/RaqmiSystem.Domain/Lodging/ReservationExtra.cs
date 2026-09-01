namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Un extra ATTACHE a un sejour : ce que le client a demande, aux conditions du jour ou il l'a
/// demande.
///
/// POURQUOI L'ATTACHEMENT NE SE CONFOND PAS AVEC LA LIGNE DE FOLIO. La ligne de folio dit ce qui a
/// ete facture ; cet attachement dit ce qui a ete VENDU, et il existe des la reservation - donc
/// souvent des mois avant qu'une ligne n'existe. Un petit-dejeuner promis a la vente doit
/// apparaitre sur la confirmation, etre repris chaque nuit par le night audit et rester lisible si
/// le sejour est prolonge. Une ligne de folio, elle, est deja du passe.
///
/// LE PRIX EST FIGE ICI AUSSI, meme discipline que le tarif de la nuit : une hausse du tarif du
/// petit-dejeuner ne doit pas reecrire ce qui a ete promis a la vente.
/// </summary>
public sealed class ReservationExtra
{
    public const int LabelMaxLength = 160;
    public const int NotesMaxLength = 500;

    private ReservationExtra()
    {
    }

    public ReservationExtra(
        Guid reservationId,
        string extraCode,
        string labelSnapshot,
        ExtraPricingBasis pricingBasis,
        decimal unitPriceSnapshot,
        decimal vatRateSnapshot,
        ChargeKind chargeKind,
        decimal quantity = 1m,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        string? notes = null)
    {
        if (reservationId == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant de la reservation est requis.", nameof(reservationId));
        }

        if (!Enum.IsDefined(pricingBasis))
        {
            throw new ArgumentOutOfRangeException(nameof(pricingBasis), pricingBasis, "Base de calcul inconnue.");
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "La quantite doit etre positive.");
        }

        if (fromDate is { } from && toDate is { } to && to < from)
        {
            throw new ArgumentException(
                "La date de fin de l'extra ne peut pas preceder sa date de debut.",
                nameof(toDate));
        }

        ReservationId = reservationId;
        ExtraCode = LodgingText.RequireCode(extraCode, nameof(extraCode), ExtraItem.CodeMaxLength);
        LabelSnapshot = LodgingText.Require(labelSnapshot, nameof(labelSnapshot), LabelMaxLength);
        PricingBasis = pricingBasis;
        UnitPriceSnapshot = LodgingText.Money(unitPriceSnapshot, nameof(unitPriceSnapshot));
        VatRateSnapshot = vatRateSnapshot;
        ChargeKind = chargeKind;
        Quantity = quantity;
        FromDate = fromDate;
        ToDate = toDate;
        Notes = LodgingText.Optional(notes, nameof(notes), NotesMaxLength);
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ReservationId { get; private set; }

    public string ExtraCode { get; private set; } = string.Empty;

    /// <summary>Libelle de l'extra a la vente, fige.</summary>
    public string LabelSnapshot { get; private set; } = string.Empty;

    public ExtraPricingBasis PricingBasis { get; private set; }

    public decimal UnitPriceSnapshot { get; private set; }

    public decimal VatRateSnapshot { get; private set; }

    public ChargeKind ChargeKind { get; private set; } = ChargeKind.Extra;

    public decimal Quantity { get; private set; } = 1m;

    /// <summary>
    /// Premiere nuit couverte. Null = toute la duree du sejour. Sert aux prestations partielles
    /// ("demi-pension uniquement les deux premieres nuits").
    /// </summary>
    public DateOnly? FromDate { get; private set; }

    /// <summary>Derniere nuit couverte, incluse. Null = jusqu'au depart.</summary>
    public DateOnly? ToDate { get; private set; }

    /// <summary>
    /// Vrai quand l'extra est compris dans le prix de la nuit (composante d'un forfait ou d'une
    /// pension) : il apparait sur la confirmation et sur le folio a sa valeur de ventilation, mais
    /// il ne s'ajoute pas au total du sejour.
    /// </summary>
    public bool IsIncludedInRate { get; private set; }

    /// <summary>Code du forfait dont cet extra est une composante, quand c'est le cas.</summary>
    public string? PackageCode { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>La nuit est-elle couverte par cet extra ?</summary>
    public bool CoversNight(DateOnly night, DateOnly stayArrival, DateOnly stayDeparture)
    {
        var from = FromDate ?? stayArrival;
        var to = ToDate ?? stayDeparture.AddDays(-1);

        return night >= from && night <= to && night >= stayArrival && night < stayDeparture;
    }

    /// <summary>Marque l'extra comme composante incluse d'un forfait.</summary>
    public void MarkAsPackageComponent(string packageCode)
    {
        PackageCode = LodgingText.RequireCode(packageCode, nameof(packageCode), Package.CodeMaxLength);
        IsIncludedInRate = true;
    }

    public void ChangeQuantity(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "La quantite doit etre positive.");
        }

        Quantity = quantity;
    }

    public void Reschedule(DateOnly? fromDate, DateOnly? toDate)
    {
        if (fromDate is { } from && toDate is { } to && to < from)
        {
            throw new ArgumentException(
                "La date de fin de l'extra ne peut pas preceder sa date de debut.",
                nameof(toDate));
        }

        FromDate = fromDate;
        ToDate = toDate;
    }
}
