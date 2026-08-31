using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// A category of rooms within one hotel unit (double room, suite, bungalow, ...). The code is
/// normalized and unique PER UNIT: two units may both have a "DBL" type, one unit may not have
/// two. Capacity is the maximum number of guests a room of this type can host, which caps
/// <see cref="Reservation.GuestCount"/> at booking time.
/// </summary>
public sealed class RoomType : AuditableEntity
{
    /// <summary>Au-dela, c'est une saisie erronee : aucune chambre ne recoit 10 lits d'appoint.</summary>
    public const int MaxExtraBedCount = 10;

    private readonly List<RoomTypeBed> beds = [];

    private RoomType()
    {
    }

    public RoomType(string hotelUnitCode, string code, string label, int capacity, string? description = null)
    {
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Code = NormalizeCode(code);
        Label = RequireValue(label, nameof(label), 160);
        Capacity = RequireStrictlyPositive(capacity, nameof(capacity));
        Description = NormalizeOptional(description, nameof(description), 300);
        IsActive = true;
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public int Capacity { get; private set; }

    /// <summary>Free-form commercial description of the type, for the setup screen.</summary>
    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// Couchage STANDARD du type. Vide tant qu'il n'a pas ete declare : les types crees avant
    /// l'arrivee du couchage restent valides, seule leur composition est inconnue.
    ///
    /// Quand il est declare, sa capacite de couchage doit EGALER <see cref="Capacity"/>. Capacity
    /// reste la valeur de reference - c'est elle, et elle seule, que la recherche de disponibilite
    /// compare au nombre de personnes. Le couchage la decrit sans jamais la contredire.
    /// </summary>
    public IReadOnlyCollection<RoomTypeBed> Beds => beds.AsReadOnly();

    /// <summary>Lits d'appoint installables en plus du couchage fixe.</summary>
    public int MaxExtraBeds { get; private set; }

    /// <summary>Berceaux installables. Comptes a part : un berceau n'est pas un couchage adulte.</summary>
    public int MaxCots { get; private set; }

    /// <summary>Personnes couchees par le couchage declare. Zero tant qu'il ne l'est pas.</summary>
    public int DeclaredSleeps => beds.Sum(bed => bed.Sleeps);

    /// <summary>Occupation maximale, lits d'appoint compris. Les berceaux n'y entrent pas.</summary>
    public int MaxOccupancy => Capacity + MaxExtraBeds;

    public void UpdateDetails(string label, int capacity, string? description = null)
    {
        Label = RequireValue(label, nameof(label), 160);
        Capacity = RequireStrictlyPositive(capacity, nameof(capacity));
        Description = NormalizeOptional(description, nameof(description), 300);
    }

    /// <summary>
    /// Remplace le couchage standard. Une liste vide efface la declaration et ramene le type a
    /// "composition inconnue", ce qui reste un etat legitime.
    ///
    /// Le refus en cas d'ecart avec <see cref="Capacity"/> est delibere : un type declare capacite
    /// 2 mais compose de quatre couchages ferait vendre une chambre pour deux a quatre personnes,
    /// ou l'inverse. Les deux erreurs se paient a la reception.
    /// </summary>
    public void ReplaceBeds(IEnumerable<RoomTypeBed> newBeds)
    {
        var materialized = newBeds.ToList();

        if (materialized.Count > 0)
        {
            var sleeps = materialized.Sum(bed => bed.Sleeps);

            if (sleeps != Capacity)
            {
                throw new ArgumentException(
                    $"Le couchage declare couche {sleeps} personne(s) alors que la capacite du type est {Capacity}. "
                    + "Corrigez l'un ou l'autre : la recherche de disponibilite se fie a la capacite.",
                    nameof(newBeds));
            }
        }

        beds.Clear();
        beds.AddRange(materialized);
    }

    public void SetExtraSleeping(int maxExtraBeds, int maxCots)
    {
        MaxExtraBeds = RequireExtraCount(maxExtraBeds, nameof(maxExtraBeds));
        MaxCots = RequireExtraCount(maxCots, nameof(maxCots));
    }

    private static int RequireExtraCount(int value, string argumentName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "La valeur ne peut pas etre negative.");
        }

        if (value > MaxExtraBedCount)
        {
            throw new ArgumentOutOfRangeException(
                argumentName,
                value,
                $"La valeur ne peut pas depasser {MaxExtraBedCount}.");
        }

        return value;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public static string NormalizeCode(string value)
    {
        return RequireValue(value, nameof(value), 40).ToUpperInvariant();
    }

    private static string RequireValue(string value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }

    private static string? NormalizeOptional(string? value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }

    private static int RequireStrictlyPositive(int value, string argumentName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "Value must be strictly positive.");
        }

        return value;
    }
}
