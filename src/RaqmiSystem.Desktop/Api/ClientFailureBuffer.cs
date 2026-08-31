using RaqmiSystem.Application.Sync;

namespace RaqmiSystem.Desktop.Api;

/// <summary>
/// Tampon borne des erreurs constatees par CE poste, en attente d'etre signalees au serveur.
///
/// Ce n'est PAS une file de rejeu et cela ne doit jamais le devenir : rien de ce qui est stocke ici
/// ne permet de refaire une action. On ne garde ni le corps de la requete, ni les valeurs saisies,
/// ni l'objet metier vise - uniquement de quoi dire "cet appel a echoue, voici quand et pourquoi".
/// Rejouer une ecriture serait dangereux : les routes qui creent un encaissement ou une facture ne
/// portent aucune cle d'idempotence, un rejeu y produirait un doublon.
///
/// Le tampon est BORNE et perd les entrees les plus anciennes en cas de debordement. Le nombre
/// d'entrees perdues est compte : un journal qui perd des lignes en silence donnerait une fausse
/// impression d'exhaustivite, ce que ce module refuse.
///
/// L'assainissement des messages est delegue a <see cref="FailureMessageSanitizer"/>, qui vit dans
/// la couche Application pour pouvoir etre couvert par des tests - le projet de tests ne peut pas
/// referencer ce client WPF.
/// </summary>
public sealed class ClientFailureBuffer
{
    /// <summary>Capacite du tampon. Au-dela, la plus ancienne entree est ecrasee.</summary>
    public const int Capacity = 50;

    private readonly object gate = new();

    private readonly Queue<ClientFailureEntry> entries = new();

    private int lostCount;

    /// <summary>Nombre d'entrees perdues par debordement depuis le dernier vidage.</summary>
    public int LostCount
    {
        get
        {
            lock (gate)
            {
                return lostCount;
            }
        }
    }

    public int Count
    {
        get
        {
            lock (gate)
            {
                return entries.Count;
            }
        }
    }

    /// <summary>
    /// Enregistre un echec. <paramref name="rawMessage"/> provient d'un corps d'erreur d'API ou
    /// d'un message d'exception : il est ASSAINI avant tout stockage, car il peut contenir un
    /// jeton, un mot de passe ou une donnee client.
    /// </summary>
    public void Record(string method, string path, int? statusCode, string kind, string rawMessage)
    {
        var entry = new ClientFailureEntry(
            Guid.NewGuid(),
            Normalize(method, 8),
            FailureMessageSanitizer.StripQuery(path),
            statusCode,
            kind,
            FailureMessageSanitizer.Sanitize(rawMessage),
            DateTimeOffset.UtcNow);

        lock (gate)
        {
            if (entries.Count >= Capacity)
            {
                entries.Dequeue();
                lostCount++;
            }

            entries.Enqueue(entry);
        }
    }

    /// <summary>
    /// Retire et rend jusqu'a <paramref name="maxItems"/> entrees. Elles sont RETIREES du tampon :
    /// si l'envoi echoue ensuite, elles sont perdues. C'est un choix - les reinjecter ferait
    /// grossir le tampon a chaque panne prolongee, c'est-a-dire exactement quand la memoire du
    /// poste doit rester disponible pour le travail de l'operateur.
    /// </summary>
    public IReadOnlyList<ClientFailureEntry> DrainUpTo(int maxItems)
    {
        var take = maxItems <= 0 ? 0 : Math.Min(maxItems, Capacity);
        var drained = new List<ClientFailureEntry>(take);

        lock (gate)
        {
            while (drained.Count < take && entries.Count > 0)
            {
                drained.Add(entries.Dequeue());
            }
        }

        return drained;
    }

    /// <summary>Remet a zero le compteur d'entrees perdues, une fois celui-ci signale.</summary>
    public void ResetLostCount()
    {
        lock (gate)
        {
            lostCount = 0;
        }
    }

    private static string Normalize(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

/// <summary>Une entree du tampon, prete a etre envoyee telle quelle.</summary>
public sealed record ClientFailureEntry(
    Guid EventId,
    string Method,
    string Path,
    int? StatusCode,
    string Kind,
    string Message,
    DateTimeOffset ClaimedAtUtc);
