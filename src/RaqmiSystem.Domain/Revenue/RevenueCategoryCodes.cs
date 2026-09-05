using System.Text.RegularExpressions;

namespace RaqmiSystem.Domain.Revenue;

/// <summary>
/// Codes des catégories de recettes livrées d'origine, et règle de forme commune à tous les
/// codes (y compris ceux créés par le paramétrage). Les quatre codes hôteliers reprennent le nom
/// des quatre anciennes colonnes de <see cref="DailyRevenue"/> : c'est ce qui rend la reprise des
/// données mécanique (une colonne devient une ligne portant le code du même nom) et ce qui permet
/// aux accesseurs de compatibilité (<see cref="DailyRevenue.Accommodation"/>...) de retrouver
/// leur montant sans table de correspondance.
/// </summary>
public static class RevenueCategoryCodes
{
    public const int MaxLength = 40;

    // Jeu hôtelier : les anciennes colonnes accommodation / food / beverage / other_revenue.
    public const string Accommodation = "ACCOMMODATION";
    public const string Food = "FOOD";
    public const string Beverage = "BEVERAGE";
    public const string Other = "OTHER";

    // Jeu générique : ce qu'une entreprise quelconque vend.
    public const string Merchandise = "MERCHANDISE";
    public const string Services = "SERVICES";
    public const string OtherIncome = "OTHER_INCOME";

    // Majuscules, chiffres, tiret et soulignement : un code est un identifiant stable qui
    // traverse les exports, les URL et les requêtes SQL de reprise, pas un libellé.
    private static readonly Regex CodePattern = new("^[A-Z0-9][A-Z0-9_-]{1,39}$", RegexOptions.Compiled);

    /// <summary>
    /// Normalise un code saisi (espaces retirés, majuscules) et refuse toute forme qui ne
    /// pourrait pas servir d'identifiant.
    /// </summary>
    public static string Normalize(string code, string argumentName = "code")
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Revenue category code is required.", argumentName);
        }

        var normalized = code.Trim().ToUpperInvariant();

        if (!CodePattern.IsMatch(normalized))
        {
            throw new ArgumentException(
                $"Revenue category code '{code.Trim()}' is invalid: 2 to {MaxLength} characters among A-Z, 0-9, '_' and '-'.",
                argumentName);
        }

        return normalized;
    }

    /// <summary>
    /// Vrai pour les trois codes hôteliers nommés. Le quatrième accesseur de compatibilité,
    /// <see cref="DailyRevenue.Other"/>, additionne tout ce qui n'est pas l'un d'eux : ainsi la
    /// somme des quatre accesseurs reste égale au total quelle que soit la liste des catégories.
    /// </summary>
    public static bool IsNamedHotelCategory(string code)
    {
        return code is Accommodation or Food or Beverage;
    }
}
