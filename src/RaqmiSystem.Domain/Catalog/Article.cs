using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Domain.Catalog;

/// <summary>
/// Article vendable : ce que l'entreprise met sur une ligne de facture, avec son prix de vente
/// hors taxes et son taux de TVA. C'est le referentiel COMMERCIAL ; il est distinct de l'article
/// de STOCK (<see cref="StockItem"/>), qui ne connait ni prix de vente ni TVA et ne sert qu'a
/// tenir les quantites.
///
/// POURQUOI DEUX REFERENTIELS ET PAS UN. Une nuitee, une prestation de salle ou un forfait spa se
/// vendent sans jamais sortir d'un magasin ; une bouteille du minibar, elle, sort d'un stock. Le
/// lien est donc porte par l'article vendable, et il est optionnel : <see cref="TracksStock"/>
/// dit si la vente sort des quantites, <see cref="StockItemCode"/> dit d'ou. Un seul referentiel
/// aurait oblige chaque prestation de service a porter un stock fictif.
///
/// LE TAUX DE TVA EST CELUI DU DOMAINE FACTURATION : la regle des taux admis vit dans
/// <see cref="InvoiceLine.RequireAllowedVatRate"/> et n'est pas restituee ici, sinon un article
/// pourrait porter un taux que la ligne de facture refuserait ensuite.
/// </summary>
public sealed class Article : AuditableEntity
{
    public const int CodeMaxLength = 40;
    public const int DesignationMaxLength = 200;
    public const int FamilyMaxLength = 80;
    public const int UnitOfMeasureMaxLength = 20;

    private Article()
    {
    }

    public Article(
        string code,
        string designation,
        string unitOfMeasure,
        decimal vatRate,
        decimal unitPriceExclVat,
        string? family = null,
        bool tracksStock = false,
        string? stockItemCode = null)
    {
        Code = NormalizeCode(code);
        ApplyDetails(designation, unitOfMeasure, vatRate, unitPriceExclVat, family, tracksStock, stockItemCode);
        IsActive = true;
    }

    /// <summary>Code normalise en majuscules : c'est lui que porte la ligne de facture.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Designation { get; private set; } = string.Empty;

    /// <summary>Famille libre ("Hebergement", "Boissons", "Prestations") : un regroupement de lecture, pas une regle.</summary>
    public string? Family { get; private set; }

    /// <summary>Unite de vente : nuit, piece, heure, kg...</summary>
    public string UnitOfMeasure { get; private set; } = string.Empty;

    /// <summary>Taux de TVA applique par defaut a la ligne de facture (0, 9 ou 19).</summary>
    public decimal VatRate { get; private set; }

    /// <summary>Prix de vente unitaire hors taxes propose par defaut ; la ligne de facture peut le surcharger.</summary>
    public decimal UnitPriceExclVat { get; private set; }

    /// <summary>Vrai quand l'emission d'une facture portant cet article sort les quantites du stock.</summary>
    public bool TracksStock { get; private set; }

    /// <summary>Article de stock dont la vente sort les quantites. Present si et seulement si <see cref="TracksStock"/>.</summary>
    public string? StockItemCode { get; private set; }

    public bool IsActive { get; private set; }

    public void UpdateDetails(
        string designation,
        string unitOfMeasure,
        decimal vatRate,
        decimal unitPriceExclVat,
        string? family,
        bool tracksStock,
        string? stockItemCode)
    {
        ApplyDetails(designation, unitOfMeasure, vatRate, unitPriceExclVat, family, tracksStock, stockItemCode);
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
        return RequireValue(value, nameof(value), CodeMaxLength).ToUpperInvariant();
    }

    private void ApplyDetails(
        string designation,
        string unitOfMeasure,
        decimal vatRate,
        decimal unitPriceExclVat,
        string? family,
        bool tracksStock,
        string? stockItemCode)
    {
        Designation = RequireValue(designation, nameof(designation), DesignationMaxLength);
        UnitOfMeasure = RequireValue(unitOfMeasure, nameof(unitOfMeasure), UnitOfMeasureMaxLength);
        VatRate = InvoiceLine.RequireAllowedVatRate(vatRate, nameof(vatRate));
        UnitPriceExclVat = RequireUnitPrice(unitPriceExclVat);
        Family = NormalizeOptional(family, nameof(family), FamilyMaxLength);
        StockItemCode = RequireStockLink(tracksStock, stockItemCode);
        TracksStock = tracksStock;
    }

    /// <summary>
    /// Le lien vers le stock est tout ou rien : un article suivi sans article de stock ne
    /// saurait pas d'ou sortir, et un article non suivi qui pointerait vers un stock laisserait
    /// croire a une sortie qui n'aura jamais lieu.
    /// </summary>
    private static string? RequireStockLink(bool tracksStock, string? stockItemCode)
    {
        if (tracksStock)
        {
            if (string.IsNullOrWhiteSpace(stockItemCode))
            {
                throw new ArgumentException(
                    "Un article suivi en stock doit referencer l'article de stock dont la vente sort les quantites.",
                    nameof(stockItemCode));
            }

            return StockItem.NormalizeCode(stockItemCode);
        }

        if (!string.IsNullOrWhiteSpace(stockItemCode))
        {
            throw new ArgumentException(
                "Un article qui ne suit pas le stock ne reference pas d'article de stock.",
                nameof(stockItemCode));
        }

        return null;
    }

    /// <summary>
    /// Meme regle que le prix unitaire d'une ligne de facture (InvoiceLine) : positif ou nul, deux
    /// decimales au plus - la colonne est numeric(18,2) et la ligne qui reprendra ce prix
    /// refuserait une precision superieure.
    /// </summary>
    private static decimal RequireUnitPrice(decimal value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Le prix de vente ne peut pas etre negatif.");
        }

        if (decimal.Round(value, 2) != value)
        {
            throw new ArgumentException("Le prix de vente ne peut pas porter plus de deux decimales.", nameof(value));
        }

        return value;
    }

    private static string RequireValue(string value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("La valeur est requise.", argumentName);
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"La valeur ne peut pas depasser {maxLength} caracteres.", argumentName);
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
            throw new ArgumentException($"La valeur ne peut pas depasser {maxLength} caracteres.", argumentName);
        }

        return trimmed;
    }
}
