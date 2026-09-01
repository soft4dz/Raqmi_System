using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Une prestation vendable en plus de la nuitee : lit d'appoint, petit-dejeuner, transfert,
/// parking, spa, blanchisserie, taxe de sejour.
///
/// POURQUOI UN REFERENTIEL PLUTOT QUE DES LIGNES LIBRES. Une ligne libre saisie au comptoir porte
/// un libelle approximatif, un prix de memoire et un taux de TVA au hasard. Le referentiel fixe
/// les trois, et surtout il fixe la BASE DE CALCUL : c'est elle qui permet de poser
/// automatiquement "petit-dejeuner x 2 personnes x 3 nuits" sans que personne n'ait a multiplier.
/// Le controle de gestion peut alors lire ce que chaque prestation a rapporte, ce qu'un libelle
/// libre interdit definitivement.
/// </summary>
public sealed class ExtraItem : AuditableEntity
{
    public const int CodeMaxLength = 40;
    public const int LabelMaxLength = 160;
    public const int DescriptionMaxLength = 500;

    private ExtraItem()
    {
    }

    public ExtraItem(
        string hotelUnitCode,
        string code,
        string label,
        ExtraPricingBasis pricingBasis,
        decimal unitPrice,
        decimal vatRate,
        ChargeKind chargeKind = ChargeKind.Extra,
        string? description = null)
    {
        if (!Enum.IsDefined(pricingBasis))
        {
            throw new ArgumentOutOfRangeException(nameof(pricingBasis), pricingBasis, "Base de calcul inconnue.");
        }

        if (chargeKind is not (ChargeKind.Extra or ChargeKind.Tax or ChargeKind.Package))
        {
            throw new ArgumentOutOfRangeException(
                nameof(chargeKind),
                chargeKind,
                "Un extra ne peut produire qu'une ligne d'extra, de taxe ou de composante de forfait.");
        }

        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Code = LodgingText.RequireCode(code, nameof(code), CodeMaxLength);
        Label = LodgingText.Require(label, nameof(label), LabelMaxLength);
        Description = LodgingText.Optional(description, nameof(description), DescriptionMaxLength);
        PricingBasis = pricingBasis;
        UnitPrice = LodgingText.Money(unitPrice, nameof(unitPrice));
        VatRate = InvoiceLine.RequireAllowedVatRate(vatRate, nameof(vatRate));
        ChargeKind = chargeKind;
        IsActive = true;
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    /// <summary>Code de l'extra, unique dans l'unite.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public ExtraPricingBasis PricingBasis { get; private set; }

    /// <summary>Prix unitaire TTC, dans la devise de l'unite.</summary>
    public decimal UnitPrice { get; private set; }

    public decimal VatRate { get; private set; }

    /// <summary>Nature de la ligne de folio produite : extra, taxe ou composante de forfait.</summary>
    public ChargeKind ChargeKind { get; private set; } = ChargeKind.Extra;

    /// <summary>
    /// Vrai quand l'extra est pose automatiquement par le night audit sur chaque nuit du sejour
    /// qui le porte (petit-dejeuner d'une demi-pension, taxe de sejour). Faux pour ce qui se
    /// consomme a la demande.
    /// </summary>
    public bool IsPostedByNightAudit { get; private set; }

    public bool IsActive { get; private set; }

    public int DisplayOrder { get; private set; }

    /// <summary>
    /// Calcule le montant TTC pour un sejour : la base de calcul dit ce qu'il faut multiplier.
    /// <paramref name="quantity"/> n'est lu que pour <see cref="ExtraPricingBasis.PerQuantity"/> ;
    /// pour les autres bases, la quantite est deduite du sejour, ce qui est precisement l'interet
    /// de la base.
    /// </summary>
    public decimal ComputeAmount(int nights, int guests, decimal quantity = 1m)
    {
        if (nights <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nights), nights, "Le nombre de nuits doit etre positif.");
        }

        if (guests <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(guests), guests, "Le nombre d'occupants doit etre positif.");
        }

        var multiplier = PricingBasis switch
        {
            ExtraPricingBasis.PerStay => 1m,
            ExtraPricingBasis.PerNight => nights,
            ExtraPricingBasis.PerPerson => guests,
            ExtraPricingBasis.PerPersonPerNight => (decimal)nights * guests,
            ExtraPricingBasis.PerQuantity => quantity,
            _ => throw new InvalidOperationException("Base de calcul inconnue.")
        };

        return Math.Round(UnitPrice * multiplier, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>La quantite portee par la ligne de folio, pour la meme base de calcul.</summary>
    public decimal ComputeQuantity(int nights, int guests, decimal quantity = 1m)
    {
        return PricingBasis switch
        {
            ExtraPricingBasis.PerStay => 1m,
            ExtraPricingBasis.PerNight => nights,
            ExtraPricingBasis.PerPerson => guests,
            ExtraPricingBasis.PerPersonPerNight => (decimal)nights * guests,
            ExtraPricingBasis.PerQuantity => quantity,
            _ => 1m
        };
    }

    public void UpdateDetails(
        string label,
        ExtraPricingBasis pricingBasis,
        decimal unitPrice,
        decimal vatRate,
        ChargeKind chargeKind,
        string? description,
        int displayOrder)
    {
        if (!Enum.IsDefined(pricingBasis))
        {
            throw new ArgumentOutOfRangeException(nameof(pricingBasis), pricingBasis, "Base de calcul inconnue.");
        }

        if (chargeKind is not (ChargeKind.Extra or ChargeKind.Tax or ChargeKind.Package))
        {
            throw new ArgumentOutOfRangeException(
                nameof(chargeKind),
                chargeKind,
                "Un extra ne peut produire qu'une ligne d'extra, de taxe ou de composante de forfait.");
        }

        Label = LodgingText.Require(label, nameof(label), LabelMaxLength);
        Description = LodgingText.Optional(description, nameof(description), DescriptionMaxLength);
        PricingBasis = pricingBasis;
        UnitPrice = LodgingText.Money(unitPrice, nameof(unitPrice));
        VatRate = InvoiceLine.RequireAllowedVatRate(vatRate, nameof(vatRate));
        ChargeKind = chargeKind;
        DisplayOrder = LodgingText.Count(displayOrder, nameof(displayOrder), RoomType.MaxDisplayOrder);
    }

    public void SetPostedByNightAudit(bool isPostedByNightAudit)
    {
        IsPostedByNightAudit = isPostedByNightAudit;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
