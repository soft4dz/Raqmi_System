using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using RaqmiSystem.Application.Identity;

namespace RaqmiSystem.Application.Security;

/// <summary>
/// Porte la session d'un client HTTP de l'API (jeton d'acces, jeton de rafraichissement,
/// expiration) et decide QUAND et COMMENT la renouveler. Le client lourd s'en sert pour chaque
/// appel authentifie ; la classe vit ici, et non dans le projet WPF, pour etre testable avec un
/// HttpMessageHandler factice - le projet de tests ne reference pas le Desktop.
///
/// Trois regles, dans cet ordre :
/// <list type="number">
/// <item>Renouvellement PROACTIF : si le jeton d'acces expire dans moins de
/// <see cref="DefaultRenewalLeadTime"/>, il est renouvele AVANT d'envoyer la requete. C'est ce
/// qui evite qu'un check-in commence avec un jeton valide et echoue a la deuxieme requete.</item>
/// <item>Sur un 401 : UN seul renouvellement, puis UN seul rejeu de la requete. Si le rejeu
/// echoue encore, sa reponse est rendue telle quelle - jamais de boucle.</item>
/// <item>Renouvellement refuse par le serveur (401/403/400 sur /auth/refresh) : la session est
/// fermee et <see cref="SessionExpiredException"/> est levee. Une panne transitoire (5xx,
/// reponse illisible) ne ferme PAS la session : le serveur peut revenir avant l'expiration.</item>
/// </list>
///
/// Les renouvellements concurrents sont serialises : le serveur applique une rotation a usage
/// unique (voir RefreshTokenEndpointTests), donc deux appels qui recevraient un 401 au meme moment
/// et presenteraient tous deux l'ancien jeton feraient tomber la session - le second serait
/// rejete comme une reutilisation. Un seul renouvellement est en vol ; les autres appels attendent
/// puis reprennent le jeton fraichement obtenu.
/// </summary>
public sealed class TokenRenewalPolicy
{
    /// <summary>Route du contrat de rafraichissement (voir Program.cs et RefreshTokenEndpointTests).</summary>
    public const string RefreshPath = "/api/v1/auth/refresh";

    /// <summary>
    /// Marge avant expiration en deca de laquelle le jeton est renouvele avant l'appel. Deux
    /// minutes couvrent la latence d'un reseau local et un leger decalage d'horloge entre le poste
    /// et le serveur ; le traitement du 401 reste le filet de securite si la marge ne suffit pas.
    /// </summary>
    public static readonly TimeSpan DefaultRenewalLeadTime = TimeSpan.FromMinutes(2);

    private const string NotAuthenticatedMessage = "Connexion requise avant d'appeler l'API.";

    private readonly HttpClient httpClient;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly TimeProvider clock;
    private readonly TimeSpan renewalLeadTime;
    private readonly SemaphoreSlim renewalGate = new(1, 1);

    // Immuable et remplace d'un bloc : deux appels concurrents comparent des REFERENCES pour
    // savoir si un autre a deja renouvele la session pendant qu'ils attendaient.
    private volatile SessionTokens? session;

    public TokenRenewalPolicy(
        HttpClient httpClient,
        JsonSerializerOptions jsonOptions,
        TimeProvider? clock = null,
        TimeSpan? renewalLeadTime = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
        this.clock = clock ?? TimeProvider.System;
        this.renewalLeadTime = renewalLeadTime ?? DefaultRenewalLeadTime;
    }

    public bool IsAuthenticated => session is not null;

    /// <summary>Expiration du jeton d'acces courant, telle que le serveur l'a annoncee, ou null hors session.</summary>
    public DateTimeOffset? ExpiresAt => session?.ExpiresAt;

    /// <summary>
    /// Jeton d'acces courant, ou null hors session. Lecture seule : sert au client a lire les
    /// claims de perimetre sans detenir lui-meme le jeton (dette : le login expose deja unitScope).
    /// </summary>
    public string? AccessToken => session?.AccessToken;

    /// <summary>Ouvre la session avec la reponse de /auth/login (ou d'un rafraichissement).</summary>
    public void Open(LoginResponse login)
    {
        ArgumentNullException.ThrowIfNull(login);

        if (string.IsNullOrWhiteSpace(login.AccessToken))
        {
            throw new ArgumentException("La reponse de connexion ne porte aucun jeton d'acces.", nameof(login));
        }

        session = new SessionTokens(login.AccessToken, login.RefreshToken, login.ExpiresAt);
    }

    /// <summary>
    /// Oublie les jetons. Purement local : l'API n'expose pas de route de deconnexion, le jeton
    /// de rafraichissement reste donc valable cote serveur jusqu'a son expiration ou sa rotation.
    /// </summary>
    public void Close()
    {
        session = null;
    }

    /// <summary>
    /// Envoie une requete authentifiee en appliquant les trois regles de la classe.
    /// </summary>
    /// <param name="refreshUri">URI absolue de <see cref="RefreshPath"/> sur le serveur vise.</param>
    /// <param name="createRequest">
    /// Fabrique de la requete, rappelee pour le rejeu : un HttpRequestMessage ne s'envoie qu'une
    /// fois. L'en-tete Authorization est pose ici ; la fabrique n'a pas a s'en occuper.
    /// </param>
    /// <exception cref="InvalidOperationException">Aucune session ouverte.</exception>
    /// <exception cref="SessionExpiredException">Le serveur a refuse le renouvellement.</exception>
    public async Task<HttpResponseMessage> SendAuthenticatedAsync(
        Uri refreshUri,
        Func<HttpRequestMessage> createRequest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refreshUri);
        ArgumentNullException.ThrowIfNull(createRequest);

        var current = session ?? throw new InvalidOperationException(NotAuthenticatedMessage);

        if (IsExpiringSoon(current))
        {
            // Une panne transitoire du renouvellement (null) n'empeche pas l'appel : le jeton
            // courant est encore valide quelques minutes, et c'est la requete elle-meme qui dira
            // si le serveur repond.
            current = await RenewAsync(refreshUri, current, cancellationToken) ?? current;
        }

        var response = await SendWithBearerAsync(createRequest, current.AccessToken, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        var renewed = await RenewAsync(refreshUri, current, cancellationToken);

        if (renewed is null)
        {
            // Le serveur n'a pas pu dire si la session est morte : le 401 d'origine est rendu tel
            // quel, la session est conservee et le prochain appel retentera le renouvellement.
            return response;
        }

        response.Dispose();

        return await SendWithBearerAsync(createRequest, renewed.AccessToken, cancellationToken);
    }

    private bool IsExpiringSoon(SessionTokens tokens)
    {
        // Expiration inconnue (reponse sans ExpiresAt) : on ne renouvelle pas a l'aveugle a chaque
        // appel ; le traitement du 401 suffira.
        if (tokens.ExpiresAt == default)
        {
            return false;
        }

        return tokens.ExpiresAt - clock.GetUtcNow() <= renewalLeadTime;
    }

    /// <summary>
    /// Renouvelle la session a partir de <paramref name="observed"/>, sous verrou. Rend la session
    /// courante si un autre appel l'a deja renouvelee entre-temps, la nouvelle session apres un
    /// rafraichissement reussi, ou null sur une panne transitoire.
    /// </summary>
    private async Task<SessionTokens?> RenewAsync(
        Uri refreshUri,
        SessionTokens observed,
        CancellationToken cancellationToken)
    {
        await renewalGate.WaitAsync(cancellationToken);

        try
        {
            // Un autre appel a pu conclure a l'expiration pendant l'attente : ne pas re-presenter
            // un jeton que le serveur vient de refuser.
            var current = session ?? throw new SessionExpiredException();

            if (!ReferenceEquals(current, observed))
            {
                return current;
            }

            if (string.IsNullOrWhiteSpace(current.RefreshToken))
            {
                session = null;
                throw new SessionExpiredException();
            }

            var body = JsonSerializer.Serialize(new RefreshTokenRequest(current.RefreshToken), jsonOptions);

            using var request = new HttpRequestMessage(HttpMethod.Post, refreshUri)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode is HttpStatusCode.Unauthorized
                or HttpStatusCode.Forbidden
                or HttpStatusCode.BadRequest)
            {
                // Le serveur a statue : jeton inconnu, deja tourne, revoque, ou compte desactive.
                session = null;
                throw new SessionExpiredException();
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            LoginResponse? login;

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                login = await JsonSerializer.DeserializeAsync<LoginResponse>(stream, jsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                login = null;
            }

            if (login is null || string.IsNullOrWhiteSpace(login.AccessToken))
            {
                return null;
            }

            var renewed = new SessionTokens(login.AccessToken, login.RefreshToken, login.ExpiresAt);
            session = renewed;

            return renewed;
        }
        finally
        {
            renewalGate.Release();
        }
    }

    private async Task<HttpResponseMessage> SendWithBearerAsync(
        Func<HttpRequestMessage> createRequest,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = createRequest();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await httpClient.SendAsync(request, cancellationToken);
    }

    private sealed record SessionTokens(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt);
}
