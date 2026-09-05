using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Revenue;

/// <summary>
/// La recette d'une journée d'exploitation pour une unité : un montant par catégorie de recettes
/// (<see cref="DailyRevenueLine"/>), des notes, et le workflow Brouillon → Soumise → Validée ou
/// Rejetée. Les catégories sont paramétrables (<see cref="RevenueCategory"/>) : la recette ne
/// connaît que des codes.
///
/// COMPATIBILITÉ — les quatre anciennes colonnes. <see cref="Accommodation"/>, <see cref="Food"/>,
/// <see cref="Beverage"/> et <see cref="Other"/> restent exposées, en lecture, et sont DÉRIVÉES
/// des lignes : les trois premières valent le montant de la ligne du même code (zéro sans ligne),
/// et <see cref="Other"/> additionne toutes les autres lignes, de sorte que la somme des quatre
/// est toujours égale au <see cref="Total"/>, quelle que soit la liste des catégories d'une
/// entreprise. Elles sont aussi persistées, en projection, dans les colonnes historiques : les
/// modules qui lisent la table en SQL (KPI, pilotage, états) continuent d'obtenir les mêmes
/// chiffres sans être modifiés, et une recette chargée sans ses lignes reste juste. Seule cette
/// classe écrit ces quatre valeurs, toujours à partir des lignes : elles ne peuvent pas diverger.
/// </summary>
public sealed class DailyRevenue : AuditableEntity
{
    private readonly List<DailyRevenueLine> _lines = new();

    private DailyRevenue()
    {
    }

    public DailyRevenue(
        DateOnly businessDate,
        string hotelUnitCode,
        IEnumerable<DailyRevenueLine> lines,
        string? notes = null)
    {
        BusinessDate = businessDate;
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        ReplaceLinesCore(lines);
        Notes = NormalizeOptional(notes, nameof(notes), 1000);
        Status = DailyRevenueStatus.Draft;
    }

    /// <summary>
    /// Forme historique à quatre montants : mappée sur les quatre codes hôteliers. Conservée pour
    /// les appelants qui saisissent une recette d'hôtel — l'ancien corps de requête HTTP en
    /// premier lieu — et pour les modules voisins qui construisent des recettes de test.
    /// </summary>
    public DailyRevenue(
        DateOnly businessDate,
        string hotelUnitCode,
        decimal accommodation,
        decimal food,
        decimal beverage,
        decimal other,
        string? notes = null)
        : this(businessDate, hotelUnitCode, HotelLines(accommodation, food, beverage, other), notes)
    {
    }

    public DateOnly BusinessDate { get; private set; }

    public string HotelUnitCode { get; private set; } = string.Empty;

    /// <summary>Les montants par catégorie ; jamais deux lignes pour un même code, jamais de ligne à zéro.</summary>
    public IReadOnlyCollection<DailyRevenueLine> Lines => _lines.AsReadOnly();

    /// <summary>Montant de la catégorie ACCOMMODATION, dérivé des lignes (voir l'en-tête de classe).</summary>
    public decimal Accommodation { get; private set; }

    /// <summary>Montant de la catégorie FOOD, dérivé des lignes.</summary>
    public decimal Food { get; private set; }

    /// <summary>Montant de la catégorie BEVERAGE, dérivé des lignes.</summary>
    public decimal Beverage { get; private set; }

    /// <summary>Somme de toutes les lignes qui ne sont ni ACCOMMODATION, ni FOOD, ni BEVERAGE.</summary>
    public decimal Other { get; private set; }

    public string? Notes { get; private set; }

    public DailyRevenueStatus Status { get; private set; } = DailyRevenueStatus.Draft;

    public DateTimeOffset? SubmittedAt { get; private set; }

    public string? SubmittedBy { get; private set; }

    public DateTimeOffset? ValidatedAt { get; private set; }

    public string? ValidatedBy { get; private set; }

    public string? RejectionReason { get; private set; }

    // Calculé sur la projection et non sur _lines : une recette matérialisée sans ses lignes
    // (requête d'un module voisin) doit rendre le même total qu'une recette complète.
    public decimal Total => Accommodation + Food + Beverage + Other;

    public bool CanEdit => Status is DailyRevenueStatus.Draft or DailyRevenueStatus.Rejected;

    /// <summary>Montant d'une catégorie, zéro si la recette n'en porte pas.</summary>
    public decimal AmountOf(string categoryCode)
    {
        var normalized = RevenueCategoryCodes.Normalize(categoryCode, nameof(categoryCode));

        return _lines.SingleOrDefault(line => line.CategoryCode == normalized)?.Amount ?? 0m;
    }

    /// <summary>
    /// Remplace tous les montants de la recette. Les lignes existantes sont ajustées sur place
    /// (mêmes raisons que BudgetPlan.ReplaceLines : l'index unique et la stabilité des Id), une
    /// catégorie absente du nouveau jeu est retirée, un montant nul retire sa ligne.
    /// </summary>
    public void UpdateLines(IEnumerable<DailyRevenueLine> lines, string? notes)
    {
        if (!CanEdit)
        {
            throw new InvalidOperationException("Only draft or rejected revenue entries can be edited.");
        }

        ReplaceLinesCore(lines);
        Notes = NormalizeOptional(notes, nameof(notes), 1000);

        if (Status == DailyRevenueStatus.Rejected)
        {
            Status = DailyRevenueStatus.Draft;
            SubmittedAt = null;
            SubmittedBy = null;
            ValidatedAt = null;
            ValidatedBy = null;
            RejectionReason = null;
        }
    }

    /// <summary>Forme historique à quatre montants de <see cref="UpdateLines"/>.</summary>
    public void UpdateAmounts(
        decimal accommodation,
        decimal food,
        decimal beverage,
        decimal other,
        string? notes)
    {
        UpdateLines(HotelLines(accommodation, food, beverage, other), notes);
    }

    public void Submit(string userName, DateTimeOffset utcNow)
    {
        if (Status != DailyRevenueStatus.Draft)
        {
            throw new InvalidOperationException("Only draft revenue entries can be submitted.");
        }

        Status = DailyRevenueStatus.Submitted;
        SubmittedAt = utcNow;
        SubmittedBy = RequireActor(userName);
    }

    public void Validate(string userName, DateTimeOffset utcNow)
    {
        if (Status != DailyRevenueStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted revenue entries can be validated.");
        }

        Status = DailyRevenueStatus.Validated;
        ValidatedAt = utcNow;
        ValidatedBy = RequireActor(userName);
        RejectionReason = null;
    }

    public void Reject(string reason, string userName, DateTimeOffset utcNow)
    {
        if (Status != DailyRevenueStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted revenue entries can be rejected.");
        }

        Status = DailyRevenueStatus.Rejected;
        ValidatedAt = utcNow;
        ValidatedBy = RequireActor(userName);
        RejectionReason = RequireValue(reason, nameof(reason), 500);
    }

    /// <summary>Les quatre montants hôteliers sous forme de lignes, dans l'ordre historique.</summary>
    public static IReadOnlyList<DailyRevenueLine> HotelLines(
        decimal accommodation,
        decimal food,
        decimal beverage,
        decimal other)
    {
        // Les noms d'argument sont ceux des anciens paramètres : un appelant historique qui
        // passe -1 en 'food' reçoit toujours une exception qui désigne 'food'.
        return
        [
            new DailyRevenueLine(RevenueCategoryCodes.Accommodation, RequirePositiveOrZero(accommodation, nameof(accommodation))),
            new DailyRevenueLine(RevenueCategoryCodes.Food, RequirePositiveOrZero(food, nameof(food))),
            new DailyRevenueLine(RevenueCategoryCodes.Beverage, RequirePositiveOrZero(beverage, nameof(beverage))),
            new DailyRevenueLine(RevenueCategoryCodes.Other, RequirePositiveOrZero(other, nameof(other)))
        ];
    }

    private void ReplaceLinesCore(IEnumerable<DailyRevenueLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var incoming = new List<DailyRevenueLine>();

        foreach (var line in lines)
        {
            ArgumentNullException.ThrowIfNull(line);

            // Deux montants pour la même catégorie sont une erreur de l'appelant : en retenir un
            // au hasard persisterait une recette que personne n'a saisie.
            if (incoming.Any(current => current.CategoryCode == line.CategoryCode))
            {
                throw new ArgumentException(
                    $"Duplicate revenue line for category {line.CategoryCode}.",
                    nameof(lines));
            }

            incoming.Add(line);
        }

        // Un montant nul n'est pas une recette : l'absence de ligne dit la même chose et évite
        // de remplir la table d'une ligne à zéro par catégorie et par jour.
        var retained = incoming.Where(line => line.Amount != 0m).ToArray();

        _lines.RemoveAll(existing => retained.All(line => line.CategoryCode != existing.CategoryCode));

        foreach (var line in retained)
        {
            var existing = _lines.SingleOrDefault(current => current.CategoryCode == line.CategoryCode);

            if (existing is null)
            {
                _lines.Add(line);
            }
            else
            {
                existing.UpdateAmount(line.Amount);
            }
        }

        RefreshProjection();
    }

    // Les quatre valeurs de compatibilité, recalculées à chaque changement de lignes : c'est
    // l'unique endroit qui les écrit.
    private void RefreshProjection()
    {
        Accommodation = 0m;
        Food = 0m;
        Beverage = 0m;
        Other = 0m;

        foreach (var line in _lines)
        {
            switch (line.CategoryCode)
            {
                case RevenueCategoryCodes.Accommodation:
                    Accommodation += line.Amount;
                    break;
                case RevenueCategoryCodes.Food:
                    Food += line.Amount;
                    break;
                case RevenueCategoryCodes.Beverage:
                    Beverage += line.Amount;
                    break;
                default:
                    Other += line.Amount;
                    break;
            }
        }
    }

    private static string RequireActor(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return "system";
        }

        return userName.Trim();
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

    private static decimal RequirePositiveOrZero(decimal value, string argumentName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "Value cannot be negative.");
        }

        return value;
    }
}
