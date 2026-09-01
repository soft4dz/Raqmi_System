using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// A physical room of one hotel unit. The number is normalized and unique PER UNIT, and the
/// room always belongs to a <see cref="RoomType"/> OF THE SAME UNIT (enforced by a composite
/// foreign key on (hotel_unit_code, room_type_code) in the EF configuration, and re-checked by
/// the service so the refusal carries a readable message rather than a constraint violation).
/// </summary>
public sealed class Room : AuditableEntity
{
    private readonly List<RoomBed> beds = [];

    private Room()
    {
    }

    public Room(string hotelUnitCode, string number, string roomTypeCode, string? floor = null, string? notes = null)
    {
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Number = NormalizeNumber(number);
        RoomTypeCode = RoomType.NormalizeCode(roomTypeCode);
        Floor = NormalizeOptional(floor, nameof(floor), 20);
        Notes = NormalizeOptional(notes, nameof(notes), 300);
        IsActive = true;
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    public string Number { get; private set; } = string.Empty;

    public string RoomTypeCode { get; private set; } = string.Empty;

    /// <summary>
    /// Free-form floor label ("RDC", "1", "Mezzanine", ...): a floor is not always a number, so
    /// it is stored as text, purely descriptive.
    /// </summary>
    public string? Floor { get; private set; }

    /// <summary>Free-form housekeeping / maintenance notes about the physical room.</summary>
    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    // ------------------------------- Localisation physique -------------------------------

    /// <summary>
    /// Batiment, quand l'unite en compte plusieurs. Libelle libre pour la meme raison que l'etage :
    /// un batiment s'appelle "Annexe" ou "Bloc B" aussi souvent qu'il porte un numero.
    /// </summary>
    public string? Building { get; private set; }

    /// <summary>Aile ou secteur a l'interieur du batiment ("Est", "Piscine", "Mer").</summary>
    public string? Wing { get; private set; }

    /// <summary>
    /// Code interne de la chambre, distinct du numero affiche : c'est celui que portent la
    /// serrure, la centrale telephonique ou l'ancien systeme. Unique dans l'unite quand il est
    /// renseigne, pour qu'un rapprochement automatique ne puisse jamais viser deux chambres.
    /// </summary>
    public string? InternalCode { get; private set; }

    // -------------------------------- Attributs commerciaux --------------------------------

    /// <summary>Vue depuis la chambre ("MER", "JARDIN", "COUR"). Code normalise.</summary>
    public string? View { get; private set; }

    /// <summary>Equipements propres a la chambre, codes normalises separes par des points-virgules.</summary>
    public string? Amenities { get; private set; }

    /// <summary>Chambre accessible aux personnes a mobilite reduite.</summary>
    public bool IsAccessible { get; private set; }

    /// <summary>Chambre fumeur. Faux par defaut : le non-fumeur est la norme.</summary>
    public bool IsSmoking { get; private set; }

    /// <summary>Ordre d'affichage sur le plan et dans les listes. Zero suit le numero de chambre.</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>
    /// Couchage PROPRE a cette chambre. Vide signifie "cette chambre suit son type" - c'est le cas
    /// courant. Aucun indicateur separe ne double cette information : deux champs pour un meme fait
    /// finiraient par se contredire.
    ///
    /// Une chambre surcharge sa COMPOSITION, jamais sa CAPACITE : la 101 peut etre en lit double la
    /// ou le type est en deux lits simples, mais elle couche toujours le meme nombre de personnes.
    /// La recherche de disponibilite se fie a la capacite du TYPE ; une chambre qui coucherait plus
    /// que son type rendrait cette recherche fausse.
    /// </summary>
    public IReadOnlyCollection<RoomBed> Beds => beds.AsReadOnly();

    /// <summary>Lits d'appoint propres a la chambre. Null = valeur du type.</summary>
    public int? MaxExtraBeds { get; private set; }

    /// <summary>Berceaux propres a la chambre. Null = valeur du type.</summary>
    public int? MaxCots { get; private set; }

    /// <summary>Vrai quand la chambre declare son propre couchage.</summary>
    public bool OverridesBeds => beds.Count > 0;

    /// <summary>
    /// Remplace le couchage propre a la chambre. Une liste vide efface la surcharge et fait
    /// retomber la chambre sur son type, ce qui est le geste normal pour annuler une exception.
    /// </summary>
    public void ReplaceBeds(IEnumerable<RoomBed> newBeds, int roomTypeCapacity)
    {
        var materialized = newBeds.ToList();

        if (materialized.Count > 0)
        {
            var sleeps = materialized.Sum(bed => bed.Sleeps);

            if (sleeps != roomTypeCapacity)
            {
                throw new ArgumentException(
                    $"Le couchage de cette chambre couche {sleeps} personne(s) alors que son type en accueille "
                    + $"{roomTypeCapacity}. Une chambre change de composition, pas de capacite : la recherche de "
                    + "disponibilite raisonne sur le type.",
                    nameof(newBeds));
            }
        }

        beds.Clear();
        beds.AddRange(materialized);
    }

    /// <summary>Fixe les couchages d'appoint propres a la chambre. Null pour suivre le type.</summary>
    public void SetExtraSleeping(int? maxExtraBeds, int? maxCots)
    {
        MaxExtraBeds = RequireOptionalExtraCount(maxExtraBeds, nameof(maxExtraBeds));
        MaxCots = RequireOptionalExtraCount(maxCots, nameof(maxCots));
    }

    private static int? RequireOptionalExtraCount(int? value, string argumentName)
    {
        if (value is null)
        {
            return null;
        }

        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "La valeur ne peut pas etre negative.");
        }

        if (value > RoomType.MaxExtraBedCount)
        {
            throw new ArgumentOutOfRangeException(
                argumentName,
                value,
                $"La valeur ne peut pas depasser {RoomType.MaxExtraBedCount}.");
        }

        return value;
    }

    /// <summary>
    /// Moves the room to another type of the SAME unit (the unit itself never changes: a room
    /// is a physical part of its building). The caller must have verified that the target type
    /// exists and is active within the unit.
    /// </summary>
    public void AssignRoomType(string roomTypeCode)
    {
        RoomTypeCode = RoomType.NormalizeCode(roomTypeCode);
    }

    /// <summary>Updates the descriptive fields of the room (floor label and notes).</summary>
    public void UpdateDetails(string? floor, string? notes)
    {
        Floor = NormalizeOptional(floor, nameof(floor), 20);
        Notes = NormalizeOptional(notes, nameof(notes), 300);
    }

    /// <summary>Renseigne la localisation physique : batiment, aile, code interne.</summary>
    public void SetLocation(string? building, string? wing, string? internalCode)
    {
        Building = NormalizeOptional(building, nameof(building), 60);
        Wing = NormalizeOptional(wing, nameof(wing), 60);
        InternalCode = NormalizeOptional(internalCode, nameof(internalCode), 40)?.ToUpperInvariant();
    }

    /// <summary>
    /// Renseigne les attributs commerciaux de la chambre. Ils sont DESCRIPTIFS : ils qualifient et
    /// filtrent, ils ne changent ni la capacite ni le prix, qui restent portes par le type et par
    /// le module Tarifs.
    /// </summary>
    public void SetAttributes(
        string? view,
        IEnumerable<string>? amenities,
        bool isAccessible,
        bool isSmoking,
        int displayOrder)
    {
        View = NormalizeOptional(view, nameof(view), 40)?.ToUpperInvariant();
        Amenities = LodgingText.Amenities(amenities, nameof(amenities));
        IsAccessible = isAccessible;
        IsSmoking = isSmoking;
        DisplayOrder = LodgingText.Count(displayOrder, nameof(displayOrder), RoomType.MaxDisplayOrder);
    }

    /// <summary>Relit les equipements de la chambre sous forme de liste.</summary>
    public IReadOnlyList<string> GetAmenities()
    {
        return LodgingText.ReadAmenities(Amenities);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public static string NormalizeNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", nameof(value));
        }

        var trimmed = value.Trim();

        if (trimmed.Length > 20)
        {
            throw new ArgumentException("Value cannot exceed 20 characters.", nameof(value));
        }

        return trimmed.ToUpperInvariant();
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
}
