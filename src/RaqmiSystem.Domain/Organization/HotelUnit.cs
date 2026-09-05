using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Organization;

/// <summary>
/// Un établissement du référentiel : hôtel, résidence, boutique, cabinet, école... Le nom de
/// la classe est historique (381 fichiers le portent) ; le concept, lui, est déjà générique.
/// <see cref="UnitType"/> dit ce qu'est le lieu, <see cref="Sector"/> dit ce que l'entreprise
/// y fait - et c'est le secteur qui décide des paquets fonctionnels proposés par défaut.
/// </summary>
public sealed class HotelUnit : AuditableEntity
{
    private HotelUnit()
    {
    }

    /// <param name="sector">
    /// Secteur d'activité. Hôtellerie par défaut : c'est ce que sont toutes les unités créées
    /// avant que la notion existe, et ce qu'un appelant historique attend sans le dire.
    /// </param>
    public HotelUnit(
        string code,
        string name,
        HotelUnitType unitType,
        int displayOrder = 0,
        BusinessSector sector = BusinessSector.Hospitality)
    {
        Code = NormalizeCode(code);
        Name = RequireValue(name, nameof(name), 160);
        UnitType = RequireDefined(unitType, nameof(unitType));
        DisplayOrder = RequirePositiveOrZero(displayOrder, nameof(displayOrder));
        Sector = RequireDefined(sector, nameof(sector));
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public HotelUnitType UnitType { get; private set; } = HotelUnitType.Hotel;

    /// <summary>Le secteur d'activité de l'établissement. Jamais nul : sans lui, aucun paquet ne saurait s'activer.</summary>
    public BusinessSector Sector { get; private set; } = BusinessSector.Hospitality;

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    /// <param name="sector">
    /// Nul = inchangé. Un client qui ne connaît pas encore le secteur (ancien écran, script
    /// d'import) ne doit pas le remettre à l'hôtellerie en corrigeant un libellé.
    /// </param>
    public void UpdateDetails(string name, HotelUnitType unitType, int displayOrder, BusinessSector? sector = null)
    {
        Name = RequireValue(name, nameof(name), 160);
        UnitType = RequireDefined(unitType, nameof(unitType));
        DisplayOrder = RequirePositiveOrZero(displayOrder, nameof(displayOrder));

        if (sector is { } requested)
        {
            Sector = RequireDefined(requested, nameof(sector));
        }
    }

    public void Rename(string name)
    {
        Name = RequireValue(name, nameof(name), 160);
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

    private static int RequirePositiveOrZero(int value, string argumentName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "Value cannot be negative.");
        }

        return value;
    }

    // Une valeur hors énumération passerait la conversion en chaîne et casserait la contrainte
    // CHECK en base : mieux vaut la refuser ici, avec le nom de l'argument.
    private static TEnum RequireDefined<TEnum>(TEnum value, string argumentName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(argumentName, value, $"Unknown {typeof(TEnum).Name}.");
        }

        return value;
    }
}
