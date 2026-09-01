namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Une composante d'un <see cref="Package"/> : ce que vaut, dans le prix global, la part
/// d'hebergement, celle du petit-dejeuner ou celle du spa.
///
/// LE MONTANT N'EST PAS UN PRIX DE VENTE, C'EST UNE VENTILATION. Le client paie le prix global du
/// forfait ; ce montant dit a quel service en attribuer la recette. C'est la difference entre "le
/// spa a fait 1 500 DA" et "l'hebergement a fait 25 000 DA", qui est la version fausse.
/// </summary>
public sealed class PackageComponent
{
    public const int LabelMaxLength = 160;

    private PackageComponent()
    {
    }

    public PackageComponent(
        string label,
        decimal amount,
        ChargeKind chargeKind,
        string? extraCode = null,
        ExtraPricingBasis pricingBasis = ExtraPricingBasis.PerStay)
    {
        if (!Enum.IsDefined(pricingBasis))
        {
            throw new ArgumentOutOfRangeException(nameof(pricingBasis), pricingBasis, "Base de calcul inconnue.");
        }

        if (chargeKind is ChargeKind.Settlement or ChargeKind.Adjustment)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chargeKind),
                chargeKind,
                "Une composante de forfait est une prestation : ni un reglement ni un ajustement.");
        }

        Label = LodgingText.Require(label, nameof(label), LabelMaxLength);
        Amount = LodgingText.Money(amount, nameof(amount));

        if (Amount == 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Une composante a zero ne ventile rien : retirez-la plutot que de la declarer.");
        }

        ChargeKind = chargeKind;
        ExtraCode = LodgingText.OptionalCode(extraCode, nameof(extraCode), ExtraItem.CodeMaxLength);
        PricingBasis = pricingBasis;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid PackageId { get; private set; }

    public string Label { get; private set; } = string.Empty;

    /// <summary>Part du prix global attribuee a cette composante, TTC.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Nature de la ligne de folio produite : nuit, extra, taxe ou composante.</summary>
    public ChargeKind ChargeKind { get; private set; } = ChargeKind.Package;

    /// <summary>Extra du referentiel correspondant, quand il y en a un.</summary>
    public string? ExtraCode { get; private set; }

    /// <summary>Base sur laquelle la composante se repete pendant le sejour.</summary>
    public ExtraPricingBasis PricingBasis { get; private set; } = ExtraPricingBasis.PerStay;
}
