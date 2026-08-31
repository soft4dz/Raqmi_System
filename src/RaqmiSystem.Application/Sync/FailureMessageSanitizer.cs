using System.Text.RegularExpressions;

namespace RaqmiSystem.Application.Sync;

/// <summary>
/// Retire d'un message de diagnostic ce qui ne doit jamais quitter le poste ni etre stocke en base.
///
/// Cette classe vit dans la couche Application et non dans le client WPF pour UNE raison precise :
/// le projet de tests ne peut pas referencer un projet net10.0-windows. Laisser ce code dans le
/// client aurait rendu la seule piece reellement sensible du module 29 impossible a couvrir par un
/// test - inacceptable pour la fonction chargee d'empecher une fuite de jeton ou de mot de passe.
///
/// Le masquage est CIBLE et non exhaustif, et c'est un compromis assume : un message d'erreur doit
/// rester lisible pour servir au diagnostic. La vraie protection est en amont - rien de metier
/// n'est jamais place dans ce champ, ni corps de requete, ni valeur saisie.
/// </summary>
public static class FailureMessageSanitizer
{
    public const int MessageMaxLength = 512;

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);

    // Un jeton JWT est reconnaissable a sa structure ; il n'a rien a faire dans un journal.
    private static readonly Regex JwtPattern = new(
        @"eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex BearerPattern = new(
        @"\bBearer\s+\S+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    // Couples cle/valeur sensibles, sous forme JSON ("password": "x") comme formulaire (token=x).
    private static readonly Regex SecretPairPattern = new(
        @"(?<key>""?\b(?:password|passwd|pwd|mot_?de_?passe|token|access_?token|refresh_?token|secret|api[_-]?key|authorization|jeton)\b""?)\s*[:=]\s*(?<value>""[^""]*""|'[^']*'|[^\s,;}&]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    public static string Sanitize(string? rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            return "(sans message)";
        }

        var value = rawMessage.Trim();

        try
        {
            // L'ORDRE EST CRITIQUE, et l'inverser rouvre une fuite reelle : sur
            // "Authorization: Bearer abc.def.ghi", le motif cle/valeur s'arrete au premier espace
            // et ne masque donc que le mot "Bearer", laissant le jeton en clair juste apres. On
            // traite donc d'abord les formes porteuses d'un espace (Bearer, puis JWT), et les
            // couples cle/valeur en dernier, quand il ne reste plus que des valeurs compactes.
            value = BearerPattern.Replace(value, "Bearer [masque]");
            value = JwtPattern.Replace(value, "[jeton masque]");
            value = SecretPairPattern.Replace(value, match => $"{match.Groups["key"].Value}: [masque]");

            // Les passes successives peuvent accoler deux marqueurs ; on les fusionne pour que le
            // message reste lisible.
            while (value.Contains("[masque] [masque]", StringComparison.Ordinal))
            {
                value = value.Replace("[masque] [masque]", "[masque]", StringComparison.Ordinal);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Un message pathologique ne doit pas bloquer le poste. En cas de depassement on
            // ECARTE le message entier plutot que de risquer d'en laisser passer une partie.
            return "(message non assaini, ecarte)";
        }

        return Truncate(value, MessageMaxLength);
    }

    /// <summary>
    /// Retire la chaine de requete d'une route : une URL peut porter des identifiants ou des codes
    /// clients en parametre, et la route seule suffit au diagnostic.
    /// </summary>
    public static string StripQuery(string? path, int maxLength = 256)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var trimmed = path.Trim();
        var mark = trimmed.IndexOf('?');

        if (mark >= 0)
        {
            trimmed = trimmed[..mark];
        }

        return trimmed.Length == 0 ? "/" : Truncate(trimmed, maxLength);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
