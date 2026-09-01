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

    // ------------------------------ Composition commerciale ------------------------------

    /// <summary>
    /// Nombre maximal d'ADULTES. Zero signifie "non declare" et fait retomber le controle sur
    /// <see cref="Capacity"/> seule, ce qui est l'etat des types crees avant cette distinction.
    ///
    /// La regle qui lie ces trois plafonds a la capacite est simple et elle est verifiee :
    /// adultes + enfants ne peut jamais depasser <see cref="MaxOccupancy"/>, sans quoi le type
    /// annoncerait plus d'occupants que de couchages. Les bebes n'y entrent pas - un berceau
    /// n'est pas un couchage - ils ont leur propre plafond.
    /// </summary>
    public int MaxAdults { get; private set; }

    /// <summary>Nombre maximal d'ENFANTS. Zero signifie "non declare".</summary>
    public int MaxChildren { get; private set; }

    /// <summary>Nombre maximal de BEBES en berceau. Zero signifie "non declare".</summary>
    public int MaxInfants { get; private set; }

    /// <summary>
    /// Tarif de reference du type, purement indicatif : le prix reellement pratique vient TOUJOURS
    /// du module Tarifs (plan tarifaire et periode). Ce champ sert d'affichage au parametrage et de
    /// point de comparaison pour les surclassements, jamais de source de facturation - deux prix
    /// pour une meme nuit finiraient par se contredire.
    /// </summary>
    public decimal BaseRate { get; private set; }

    /// <summary>Surface en metres carres. Zero quand elle n'est pas renseignee.</summary>
    public decimal SurfaceSquareMeters { get; private set; }

    /// <summary>
    /// Rang commercial du type dans l'echelle de l'etablissement : plus il est eleve, plus le type
    /// est haut de gamme. C'est LUI qui distingue un surclassement d'un declassement - sans echelle
    /// declaree, "Suite" et "Double" ne sont que deux codes, et le systeme ne peut pas dire lequel
    /// est une montee en gamme.
    /// </summary>
    public int Rank { get; private set; }

    /// <summary>Equipements standards du type, codes normalises separes par des points-virgules.</summary>
    public string? Amenities { get; private set; }

    /// <summary>Ordre d'affichage dans les listes et sur le plan de reservation.</summary>
    public int DisplayOrder { get; private set; }

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

    /// <summary>
    /// Declare la composition commerciale : adultes, enfants et bebes maximum.
    ///
    /// Le refus quand adultes + enfants depasse <see cref="MaxOccupancy"/> est le meme genre de
    /// garde que celui du couchage : un type qui annonce cinq occupants pour trois couchages fera
    /// vendre une chambre trop petite, et la reception paiera l'ecart. Zero partout est accepte et
    /// signifie "non declare" : le controle retombe alors sur la capacite seule.
    /// </summary>
    public void SetGuestMix(int maxAdults, int maxChildren, int maxInfants)
    {
        var adults = RequireOccupantCount(maxAdults, nameof(maxAdults));
        var children = RequireOccupantCount(maxChildren, nameof(maxChildren));
        var infants = RequireOccupantCount(maxInfants, nameof(maxInfants));

        if (adults + children > MaxOccupancy)
        {
            throw new ArgumentException(
                $"La composition declaree accueille {adults + children} personne(s) alors que le type, "
                + $"lits d'appoint compris, en couche {MaxOccupancy}. Corrigez la capacite, les lits "
                + "d'appoint ou la composition.",
                nameof(maxAdults));
        }

        MaxAdults = adults;
        MaxChildren = children;
        MaxInfants = infants;
    }

    /// <summary>Renseigne les attributs commerciaux : tarif de reference, surface, rang, ordre.</summary>
    public void SetCommercialProfile(decimal baseRate, decimal surfaceSquareMeters, int rank, int displayOrder)
    {
        BaseRate = LodgingText.Money(baseRate, nameof(baseRate));
        SurfaceSquareMeters = LodgingText.Money(surfaceSquareMeters, nameof(surfaceSquareMeters));
        Rank = LodgingText.Count(rank, nameof(rank), MaxRank);
        DisplayOrder = LodgingText.Count(displayOrder, nameof(displayOrder), MaxDisplayOrder);
    }

    /// <summary>Remplace la liste des equipements standards du type.</summary>
    public void SetAmenities(IEnumerable<string>? amenities)
    {
        Amenities = LodgingText.Amenities(amenities, nameof(amenities));
    }

    /// <summary>Relit les equipements standards sous forme de liste.</summary>
    public IReadOnlyList<string> GetAmenities()
    {
        return LodgingText.ReadAmenities(Amenities);
    }

    /// <summary>
    /// Le type peut-il accueillir cette composition ? Un plafond non declare (zero) ne bloque
    /// jamais : il signifie "non renseigne", pas "aucun adulte admis".
    /// </summary>
    public bool CanHost(int adults, int children, int infants)
    {
        if (adults <= 0)
        {
            return false;
        }

        if (adults + children > MaxOccupancy)
        {
            return false;
        }

        if (MaxAdults > 0 && adults > MaxAdults)
        {
            return false;
        }

        if (MaxChildren > 0 && children > MaxChildren)
        {
            return false;
        }

        if (MaxInfants > 0 && infants > MaxInfants)
        {
            return false;
        }

        // Aucun plafond bebe declare : un berceau reste soumis au nombre de berceaux installables.
        return MaxInfants > 0 || infants <= MaxCots;
    }

    private static int RequireOccupantCount(int value, string argumentName)
    {
        return LodgingText.Count(value, argumentName, MaxOccupantCount);
    }

    /// <summary>Au-dela, c'est un dortoir : la saisie est refusee.</summary>
    public const int MaxOccupantCount = 30;

    /// <summary>Echelle de gamme bornee : au-dela, le rang ne veut plus rien dire.</summary>
    public const int MaxRank = 99;

    /// <summary>Borne de l'ordre d'affichage.</summary>
    public const int MaxDisplayOrder = 9999;

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
