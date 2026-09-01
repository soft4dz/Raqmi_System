using System.Globalization;
using System.Text;

namespace RaqmiSystem.Application.Navigation;

/// <summary>
/// Normalisation partagée par toutes les recherches de navigation (accueil, barre latérale,
/// arbre) : une seule façon de trouver un écran, donc jamais un résultat d'un côté et pas de
/// l'autre.
/// </summary>
public static class NavigationSearch
{
    /// <summary>
    /// Minuscules sans accent : « Hebergement » doit trouver « Hébergement », et « TVA »
    /// comme « tva ». La décomposition Unicode sépare la lettre de son accent, qu'il suffit
    /// alors d'écarter. À appliquer aussi à la saisie avant toute comparaison.
    /// </summary>
    public static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Vrai si la saisie, une fois normalisée, apparaît dans le texte déjà normalisé.
    /// Une saisie vide correspond à tout : c'est l'absence de filtre, pas un filtre vide.
    /// </summary>
    public static bool Matches(string normalizedSearchText, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return normalizedSearchText.Contains(Normalize(query.Trim()), StringComparison.Ordinal);
    }
}
