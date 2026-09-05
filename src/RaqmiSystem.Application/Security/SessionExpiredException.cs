namespace RaqmiSystem.Application.Security;

/// <summary>
/// La session du client est terminee : le serveur a refuse de renouveler le jeton d'acces (jeton
/// de rafraichissement expire, deja consomme par la rotation, revoque par un changement de mot de
/// passe, compte desactive). La seule issue est une nouvelle connexion.
///
/// POURQUOI UNE InvalidOperationException. Les ecrans du client lourd n'attrapent que quatre
/// familles d'exceptions autour d'un appel API : ApiRequestFailedException (code HTTP + message),
/// HttpRequestException (reseau), OperationCanceledException (delai) et InvalidOperationException
/// (etat du client, dont « Connexion requise avant d'appeler l'API »). Une session expiree est
/// exactement un etat du client - il n'est plus connecte - et son message doit s'afficher tel quel,
/// sans prefixe « API 401 » qui n'aiderait pas un receptionniste. Deriver d'InvalidOperationException
/// garantit que CHAQUE ecran existant l'affiche deja, sans qu'aucune vue n'ait a etre modifiee.
/// </summary>
public sealed class SessionExpiredException : InvalidOperationException
{
    public const string DefaultMessage =
        "Votre session a expiré. Reconnectez-vous pour continuer.";

    public SessionExpiredException()
        : base(DefaultMessage)
    {
    }

    public SessionExpiredException(string message)
        : base(message)
    {
    }

    public SessionExpiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
