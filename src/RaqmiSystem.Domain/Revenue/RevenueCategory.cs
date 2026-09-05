using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Revenue;

/// <summary>
/// Une catégorie de recettes paramétrable : le code est sa clé (il ne change jamais, les lignes
/// de recettes et de budget le référencent), le libellé et l'ordre servent à l'affichage, et le
/// secteur cible optionnel dit à quel type d'entreprise elle est proposée
/// (voir <see cref="RevenueCategoryCatalog.Applicable"/> pour la règle).
///
/// Pas d'entité auditable ici : le jeu par défaut est semé par la configuration EF (HasData),
/// qui exige des valeurs stables d'une génération de migration à l'autre — un CreatedAt à
/// l'instant courant ferait détecter un changement à chaque fois. La traçabilité des créations
/// et modifications passe par le journal d'audit, comme pour les unités.
/// </summary>
public sealed class RevenueCategory
{
    private RevenueCategory()
    {
    }

    public RevenueCategory(string code, string label, int displayOrder, BusinessSector? sector = null)
    {
        Code = RevenueCategoryCodes.Normalize(code, nameof(code));
        Label = RequireLabel(label, nameof(label));
        DisplayOrder = RequireDisplayOrder(displayOrder, nameof(displayOrder));
        Sector = RequireSector(sector, nameof(sector));
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public BusinessSector? Sector { get; private set; }

    public void Update(string label, int displayOrder, BusinessSector? sector, bool isActive)
    {
        Label = RequireLabel(label, nameof(label));
        DisplayOrder = RequireDisplayOrder(displayOrder, nameof(displayOrder));
        Sector = RequireSector(sector, nameof(sector));
        IsActive = isActive;
    }

    private static string RequireLabel(string label, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("Revenue category label is required.", argumentName);
        }

        var trimmed = label.Trim();

        if (trimmed.Length > 160)
        {
            throw new ArgumentException("Revenue category label cannot exceed 160 characters.", argumentName);
        }

        return trimmed;
    }

    private static int RequireDisplayOrder(int displayOrder, string argumentName)
    {
        if (displayOrder < 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, displayOrder, "Display order cannot be negative.");
        }

        return displayOrder;
    }

    private static BusinessSector? RequireSector(BusinessSector? sector, string argumentName)
    {
        if (sector.HasValue && !Enum.IsDefined(sector.Value))
        {
            throw new ArgumentOutOfRangeException(argumentName, sector, "Business sector is not supported.");
        }

        return sector;
    }
}
