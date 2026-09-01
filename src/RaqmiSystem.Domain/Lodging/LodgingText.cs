namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Normalisation partagee par les entites du PMS. Les entites historiques du module portent
/// chacune leur propre copie de ces gardes ; les entites ajoutees par la passe PMS s'appuient
/// sur celle-ci plutot que de la dupliquer trente fois de plus. Le comportement est identique -
/// meme trim, memes bornes, memes messages - de sorte que les deux familles ne divergent pas.
/// </summary>
internal static class LodgingText
{
    /// <summary>Valeur obligatoire, trimee, bornee.</summary>
    public static string Require(string value, string argumentName, int maxLength)
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

    /// <summary>Code obligatoire : trime, borne, passe en majuscules.</summary>
    public static string RequireCode(string value, string argumentName, int maxLength = 40)
    {
        return Require(value, argumentName, maxLength).ToUpperInvariant();
    }

    /// <summary>Valeur facultative : null quand elle est vide, sinon trimee et bornee.</summary>
    public static string? Optional(string? value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Require(value, argumentName, maxLength);
    }

    /// <summary>Code facultatif : null quand il est vide, sinon normalise en majuscules.</summary>
    public static string? OptionalCode(string? value, string argumentName, int maxLength = 40)
    {
        return Optional(value, argumentName, maxLength)?.ToUpperInvariant();
    }

    /// <summary>Acteur d'une operation : jamais vide, "system" a defaut.</summary>
    public static string Actor(string? userName)
    {
        return string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim();
    }

    /// <summary>Montant monetaire positif ou nul, au plus deux decimales (colonnes numeric(18,2)).</summary>
    public static decimal Money(decimal value, string argumentName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "Le montant ne peut pas etre negatif.");
        }

        if (decimal.Round(value, 2) != value)
        {
            throw new ArgumentException("Le montant ne peut pas porter plus de deux decimales.", argumentName);
        }

        return value;
    }

    /// <summary>Montant signe (avoir, remise), au plus deux decimales.</summary>
    public static decimal SignedMoney(decimal value, string argumentName)
    {
        if (decimal.Round(value, 2) != value)
        {
            throw new ArgumentException("Le montant ne peut pas porter plus de deux decimales.", argumentName);
        }

        return value;
    }

    /// <summary>Pourcentage 0..100, au plus deux decimales.</summary>
    public static decimal Percent(decimal value, string argumentName)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "Le pourcentage doit etre compris entre 0 et 100.");
        }

        if (decimal.Round(value, 2) != value)
        {
            throw new ArgumentException("Le pourcentage ne peut pas porter plus de deux decimales.", argumentName);
        }

        return value;
    }

    /// <summary>Entier positif ou nul, borne.</summary>
    public static int Count(int value, string argumentName, int max)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, "La valeur ne peut pas etre negative.");
        }

        if (value > max)
        {
            throw new ArgumentOutOfRangeException(argumentName, value, $"La valeur ne peut pas depasser {max}.");
        }

        return value;
    }

    /// <summary>
    /// Liste d'equipements stockee a plat : codes normalises, separes par des points-virgules,
    /// dedoublonnes, ordonnes. Les equipements sont DESCRIPTIFS - ils n'entrent ni dans la
    /// disponibilite ni dans la tarification - c'est ce qui autorise cette forme compacte plutot
    /// qu'un referentiel et deux tables de jointure.
    /// </summary>
    public static string? Amenities(IEnumerable<string>? codes, string argumentName, int maxLength = 400)
    {
        if (codes is null)
        {
            return null;
        }

        var normalized = codes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        if (normalized.Length == 0)
        {
            return null;
        }

        var joined = string.Join(';', normalized);

        if (joined.Length > maxLength)
        {
            throw new ArgumentException(
                $"La liste d'equipements ne peut pas depasser {maxLength} caracteres.",
                argumentName);
        }

        return joined;
    }

    /// <summary>Relit une liste d'equipements stockee a plat.</summary>
    public static IReadOnlyList<string> ReadAmenities(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return [];
        }

        return stored.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
