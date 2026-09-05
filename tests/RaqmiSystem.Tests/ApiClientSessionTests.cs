using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Tests;

/// <summary>
/// La politique de renouvellement de session du client lourd (TokenRenewalPolicy), jouee contre
/// un HttpMessageHandler factice : ni serveur, ni WPF. Ce que ces tests fixent est le CONTRAT
/// que RaqmiApiClient applique a chaque appel authentifie - renouvellement avant expiration, un
/// seul renouvellement et un seul rejeu sur 401, session fermee quand le serveur refuse, un seul
/// renouvellement en vol pour N appels concurrents.
/// </summary>
public sealed class ApiClientSessionTests
{
    private static readonly Uri ApiBase = new("http://api.test");

    private static readonly Uri RefreshUri = new(ApiBase, TokenRenewalPolicy.RefreshPath);

    private static readonly Uri ResourceUri = new(ApiBase, "/api/v1/organization/hotel-units");

    private static readonly DateTimeOffset Now = new(2026, 9, 5, 10, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    [Fact]
    public async Task Un_jeton_qui_expire_dans_moins_de_deux_minutes_est_renouvele_avant_l_appel()
    {
        var handler = new ScriptedHandler(request => request.RequestUri == RefreshUri
            ? Ok(CreateLogin("access-2", "refresh-2", Now.AddMinutes(60)))
            : Ok(new { }));
        var policy = CreatePolicy(handler);
        policy.Open(CreateLogin("access-1", "refresh-1", expiresAt: Now.AddSeconds(90)));

        using var response = await policy.SendAuthenticatedAsync(RefreshUri, CreateRequest, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            [(RefreshUri.AbsolutePath, (string?)null), (ResourceUri.AbsolutePath, "access-2")],
            handler.Requests.Select(request => (request.Path, request.Bearer)).ToArray());
        Assert.Equal(Now.AddMinutes(60), policy.ExpiresAt);
    }

    [Fact]
    public async Task Un_jeton_encore_valide_n_est_pas_renouvele()
    {
        var handler = new ScriptedHandler(_ => Ok(new { }));
        var policy = CreatePolicy(handler);
        policy.Open(CreateLogin("access-1", "refresh-1", expiresAt: Now.AddMinutes(10)));

        using var response = await policy.SendAuthenticatedAsync(RefreshUri, CreateRequest, CancellationToken.None);

        var sent = Assert.Single(handler.Requests);
        Assert.Equal(ResourceUri.AbsolutePath, sent.Path);
        Assert.Equal("access-1", sent.Bearer);
    }

    [Fact]
    public async Task Un_401_declenche_un_seul_renouvellement_puis_le_rejeu_de_la_requete()
    {
        var handler = new ScriptedHandler(request =>
        {
            if (request.RequestUri == RefreshUri)
            {
                return Ok(CreateLogin("access-2", "refresh-2", Now.AddMinutes(60)));
            }

            return request.Headers.Authorization?.Parameter == "access-2"
                ? Ok(new { })
                : new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });
        var policy = CreatePolicy(handler);
        policy.Open(CreateLogin("access-1", "refresh-1", expiresAt: Now.AddMinutes(30)));

        using var response = await policy.SendAuthenticatedAsync(RefreshUri, CreateRequest, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            [
                (ResourceUri.AbsolutePath, "access-1"),
                (RefreshUri.AbsolutePath, (string?)null),
                (ResourceUri.AbsolutePath, "access-2")
            ],
            handler.Requests.Select(request => (request.Path, request.Bearer)).ToArray());
        Assert.True(policy.IsAuthenticated);
    }

    [Fact]
    public async Task Le_renouvellement_presente_le_jeton_de_rafraichissement_sur_la_route_du_contrat()
    {
        var handler = new ScriptedHandler(request => request.RequestUri == RefreshUri
            ? Ok(CreateLogin("access-2", "refresh-2", Now.AddMinutes(60)))
            : new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var policy = CreatePolicy(handler);
        policy.Open(CreateLogin("access-1", "refresh-1", expiresAt: Now.AddMinutes(30)));

        using var response = await policy.SendAuthenticatedAsync(RefreshUri, CreateRequest, CancellationToken.None);

        var refresh = Assert.Single(handler.Requests, request => request.Path == RefreshUri.AbsolutePath);
        Assert.Equal(HttpMethod.Post, refresh.Method);
        Assert.Null(refresh.Bearer);

        // Le corps est celui que POST /api/v1/auth/refresh lie : { "refreshToken": "..." }, en
        // camelCase comme le reste de l'API (RefreshTokenEndpointTests).
        var body = JsonSerializer.Deserialize<RefreshTokenRequest>(refresh.Body!, Json);
        Assert.Equal("refresh-1", body!.RefreshToken);
        Assert.Contains("\"refreshToken\"", refresh.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Le_rejeu_n_est_jamais_retente_quand_il_echoue_encore()
    {
        var refreshCount = 0;
        var handler = new ScriptedHandler(request =>
        {
            if (request.RequestUri == RefreshUri)
            {
                refreshCount++;
                return Ok(CreateLogin($"access-{refreshCount + 1}", $"refresh-{refreshCount + 1}", Now.AddMinutes(60)));
            }

            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });
        var policy = CreatePolicy(handler);
        policy.Open(CreateLogin("access-1", "refresh-1", expiresAt: Now.AddMinutes(30)));

        using var response = await policy.SendAuthenticatedAsync(RefreshUri, CreateRequest, CancellationToken.None);

        // Le second 401 est rendu tel quel : un renouvellement, un rejeu, et on s'arrete.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, refreshCount);
        Assert.Equal(3, handler.Requests.Count);
        Assert.True(policy.IsAuthenticated);
    }

    [Fact]
    public async Task Un_renouvellement_refuse_ferme_la_session_et_leve_session_expiree()
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var policy = CreatePolicy(handler);
        policy.Open(CreateLogin("access-1", "refresh-1", expiresAt: Now.AddMinutes(30)));

        var exception = await Assert.ThrowsAsync<SessionExpiredException>(
            () => policy.SendAuthenticatedAsync(RefreshUri, CreateRequest, CancellationToken.None));

        Assert.Equal(SessionExpiredException.DefaultMessage, exception.Message);
        Assert.False(policy.IsAuthenticated);
        Assert.Null(policy.ExpiresAt);

        // Aucun rejeu apres un refus : la requete d'origine, le renouvellement, et c'est tout.
        Assert.Equal(
            [ResourceUri.AbsolutePath, RefreshUri.AbsolutePath],
            handler.Requests.Select(request => request.Path).ToArray());

        // Sans session, l'appel suivant est refuse avant tout envoi.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => policy.SendAuthenticatedAsync(RefreshUri, CreateRequest, CancellationToken.None));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Une_panne_transitoire_du_renouvellement_rend_le_401_d_origine_sans_fermer_la_session()
    {
        var handler = new ScriptedHandler(request => request.RequestUri == RefreshUri
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var policy = CreatePolicy(handler);
        policy.Open(CreateLogin("access-1", "refresh-1", expiresAt: Now.AddMinutes(30)));

        using var response = await policy.SendAuthenticatedAsync(RefreshUri, CreateRequest, CancellationToken.None);

        // Le serveur n'a pas statue sur la session : l'appelant voit le 401 d'origine, la session
        // est conservee et le prochain appel retentera - c'est ce qui evite de deconnecter tout
        // un hotel pour un redemarrage de l'API.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(policy.IsAuthenticated);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Une_panne_transitoire_du_renouvellement_proactif_laisse_partir_l_appel_avec_le_jeton_courant()
    {
        var handler = new ScriptedHandler(request => request.RequestUri == RefreshUri
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : Ok(new { }));
        var policy = CreatePolicy(handler);
        policy.Open(CreateLogin("access-1", "refresh-1", expiresAt: Now.AddSeconds(30)));

        using var response = await policy.SendAuthenticatedAsync(RefreshUri, CreateRequest, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("access-1", handler.Requests.Last().Bearer);
        Assert.True(policy.IsAuthenticated);
    }

    [Fact]
    public async Task Des_appels_concurrents_partagent_un_seul_renouvellement()
    {
        // Le renouvellement est retenu par une porte jusqu'a ce que les cinq appels aient recu
        // leur 401 : c'est la situation d'un ecran d'accueil qui charge ses files en parallele
        // juste apres l'expiration du jeton. Sans serialisation, cinq renouvellements partiraient
        // avec le MEME jeton de rafraichissement, et la rotation a usage unique du serveur
        // rejetterait les quatre derniers - session perdue pour un simple rafraichissement.
        var refreshGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshCount = 0;
        var handler = new ScriptedHandler(async request =>
        {
            if (request.RequestUri == RefreshUri)
            {
                Interlocked.Increment(ref refreshCount);
                await refreshGate.Task;
                return Ok(CreateLogin("access-2", "refresh-2", Now.AddMinutes(60)));
            }

            return request.Headers.Authorization?.Parameter == "access-2"
                ? Ok(new { })
                : new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });
        var policy = CreatePolicy(handler);
        policy.Open(CreateLogin("access-1", "refresh-1", expiresAt: Now.AddMinutes(30)));

        var calls = Enumerable.Range(0, 5)
            .Select(_ => policy.SendAuthenticatedAsync(RefreshUri, CreateRequest, CancellationToken.None))
            .ToArray();

        Assert.Equal(5, handler.Requests.Count(request => request.Bearer == "access-1"));
        Assert.Equal(1, refreshCount);

        refreshGate.SetResult();
        var responses = await Task.WhenAll(calls);

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(1, refreshCount);
        Assert.Equal(5, handler.Requests.Count(request => request.Bearer == "access-2"));

        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public void Fermer_la_session_oublie_les_jetons()
    {
        var policy = CreatePolicy(new ScriptedHandler(_ => Ok(new { })));
        policy.Open(CreateLogin("access-1", "refresh-1", expiresAt: Now.AddMinutes(30)));
        Assert.True(policy.IsAuthenticated);

        policy.Close();

        Assert.False(policy.IsAuthenticated);
        Assert.Null(policy.ExpiresAt);
    }

    [Fact]
    public void Session_expiree_est_une_InvalidOperationException_pour_les_ecrans_existants()
    {
        // Les ecrans du client lourd n'attrapent qu'InvalidOperationException pour les etats du
        // client : c'est ce qui permet a chacun d'afficher le message sans etre modifie.
        Assert.IsAssignableFrom<InvalidOperationException>(new SessionExpiredException());
    }

    private static TokenRenewalPolicy CreatePolicy(ScriptedHandler handler)
    {
        return new TokenRenewalPolicy(new HttpClient(handler), Json, new FixedClock(Now));
    }

    private static HttpRequestMessage CreateRequest()
    {
        return new HttpRequestMessage(HttpMethod.Get, ResourceUri);
    }

    private static LoginResponse CreateLogin(string accessToken, string refreshToken, DateTimeOffset expiresAt)
    {
        return new LoginResponse(
            accessToken,
            expiresAt,
            refreshToken,
            new AuthenticatedUser(Guid.NewGuid(), "reception", "reception@example.test", "Reception", false, ["Reader"], [], new UnitScopeResponse(true, [])));
    }

    private static HttpResponseMessage Ok<T>(T payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(payload, options: Json)
        };
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record RecordedRequest(HttpMethod Method, string Path, string? Bearer, string? Body);

    /// <summary>
    /// Handler factice : journalise chaque requete (verbe, chemin, jeton Bearer, corps) puis
    /// repond selon le script. Le corps est lu ICI, avant que la politique ne libere la requete.
    /// </summary>
    private sealed class ScriptedHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        private readonly List<RecordedRequest> requests = [];

        public ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            : this(request => Task.FromResult(respond(request)))
        {
        }

        public IReadOnlyList<RecordedRequest> Requests
        {
            get
            {
                lock (requests)
                {
                    return [.. requests];
                }
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            lock (requests)
            {
                requests.Add(new RecordedRequest(
                    request.Method,
                    request.RequestUri!.AbsolutePath,
                    request.Headers.Authorization?.Parameter,
                    body));
            }

            return await respond(request);
        }
    }
}
