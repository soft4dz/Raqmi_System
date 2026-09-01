namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Un palier d'une <see cref="CancellationPolicy"/> : "a partir de J-<see cref="MinDaysBeforeArrival"/>
/// avant l'arrivee, la penalite est celle-ci".
///
/// Le palier a zero jour est la clause de repli : il s'applique a toute annulation, y compris le
/// jour meme. Sans lui, une annulation de derniere minute ne trouverait aucun palier et sortirait
/// gratuite - ce qui est rarement l'intention.
/// </summary>
public sealed class CancellationPolicyRule
{
    /// <summary>Au-dela, le palier ne decrit plus une politique commerciale.</summary>
    public const int MaxDaysBeforeArrival = 365;

    private CancellationPolicyRule()
    {
    }

    public CancellationPolicyRule(int minDaysBeforeArrival, CancellationChargeBasis basis, decimal value)
    {
        MinDaysBeforeArrival = LodgingText.Count(
            minDaysBeforeArrival,
            nameof(minDaysBeforeArrival),
            MaxDaysBeforeArrival);

        ValidateTerms(basis, value, nameof(value));

        Basis = basis;
        Value = value;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid CancellationPolicyId { get; private set; }

    /// <summary>Delai minimal avant l'arrivee, en jours, a partir duquel ce palier s'applique.</summary>
    public int MinDaysBeforeArrival { get; private set; }

    public CancellationChargeBasis Basis { get; private set; }

    /// <summary>
    /// Valeur du palier : nombre de nuits pour <see cref="CancellationChargeBasis.Nights"/>,
    /// pourcentage pour <see cref="CancellationChargeBasis.PercentOfStay"/>, montant pour
    /// <see cref="CancellationChargeBasis.FixedAmount"/>, ignoree pour les autres.
    /// </summary>
    public decimal Value { get; private set; }

    internal static void ValidateTerms(CancellationChargeBasis basis, decimal value, string argumentName)
    {
        if (!Enum.IsDefined(basis))
        {
            throw new ArgumentOutOfRangeException(nameof(basis), basis, "Base de penalite inconnue.");
        }

        switch (basis)
        {
            case CancellationChargeBasis.Nights when value is <= 0 or > 365:
                throw new ArgumentOutOfRangeException(
                    argumentName,
                    value,
                    "Le nombre de nuits retenues doit etre compris entre 1 et 365.");

            case CancellationChargeBasis.PercentOfStay:
                LodgingText.Percent(value, argumentName);
                break;

            case CancellationChargeBasis.FixedAmount when value <= 0:
                throw new ArgumentOutOfRangeException(
                    argumentName,
                    value,
                    "Un forfait de penalite doit etre strictement positif.");

            case CancellationChargeBasis.FixedAmount:
                LodgingText.Money(value, argumentName);
                break;
        }
    }
}
