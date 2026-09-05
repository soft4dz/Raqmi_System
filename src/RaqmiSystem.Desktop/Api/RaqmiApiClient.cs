using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Desktop.Api;

// Classe partielle : chaque module metier ajoute ses appels dans son propre
// fichier RaqmiApiClient.<Module>.cs, ce qui evite que plusieurs chantiers
// paralleles se disputent ce fichier. Les membres prives (SendAsync,
// ReadResponseAsync, BuildQuery, EnsureAuthenticated...) restent accessibles
// depuis ces fichiers puisqu'il s'agit de la meme classe.
public sealed partial class RaqmiApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient httpClient;

    // La session (jetons d'acces et de rafraichissement, expiration) et sa politique de
    // renouvellement vivent dans RaqmiSystem.Application.Security.TokenRenewalPolicy, testee sans
    // WPF. Avant ce lot, le client ne gardait que le jeton d'acces et jetait le jeton de
    // rafraichissement recu au login : chaque poste mourait a l'expiration du jeton (60 minutes),
    // en plein geste, sans autre issue que de relancer l'application.
    private readonly TokenRenewalPolicy session;

    /// <summary>
    /// Delai maximal d'un appel API, en l'absence de reglage explicite. Trente secondes : bien
    /// au-dela de tout appel normal sur un reseau local (les plus lourds, journal d'audit ou grand
    /// livre, repondent en moins de deux secondes), et assez court pour qu'une coupure pendant un
    /// check-in soit annoncee a la reception avant que le client ne s'impatiente. Le defaut de
    /// HttpClient, 100 secondes, faisait attendre presque deux minutes sur un cable debranche.
    /// </summary>
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Variable d'environnement qui surcharge <see cref="DefaultRequestTimeout"/>, en secondes
    /// entieres (liaison lente, serveur distant, diagnostic). Bornee a [5 ; 300] : en deca on ne
    /// laisse plus le temps a une requete honnete, au-dela on retombe dans l'attente que ce
    /// reglage existe pour supprimer. Valeur absente ou invalide = defaut, sans erreur.
    /// </summary>
    public const string RequestTimeoutEnvironmentVariable = "RAQMI_DESKTOP_HTTP_TIMEOUT_SECONDS";

    private const int MinRequestTimeoutSeconds = 5;

    private const int MaxRequestTimeoutSeconds = 300;

    static RaqmiApiClient()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    /// <param name="requestTimeout">
    /// Delai explicite ; null = variable d'environnement, puis <see cref="DefaultRequestTimeout"/>.
    /// Le delai est pose ICI et non par l'appelant parce que le client possede sa politique de
    /// transport : quiconque lui confie un HttpClient neuf obtient un delai raisonnable sans y penser.
    /// </param>
    public RaqmiApiClient(HttpClient httpClient, TimeSpan? requestTimeout = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        // HttpClient.Timeout ne se modifie plus apres la premiere requete : le constructeur est le
        // seul endroit ou ce reglage est garanti d'etre accepte.
        httpClient.Timeout = requestTimeout ?? ResolveRequestTimeout();

        session = new TokenRenewalPolicy(httpClient, JsonOptions);
    }

    private static TimeSpan ResolveRequestTimeout()
    {
        var configured = Environment.GetEnvironmentVariable(RequestTimeoutEnvironmentVariable);

        if (int.TryParse(configured, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            && seconds is >= MinRequestTimeoutSeconds and <= MaxRequestTimeoutSeconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return DefaultRequestTimeout;
    }

    public bool IsAuthenticated => session.IsAuthenticated;

    /// <summary>
    /// Expiration du jeton d'acces courant, telle qu'annoncee par le serveur, ou null hors session.
    /// Informatif : le renouvellement est automatique, l'appelant n'a rien a planifier.
    /// </summary>
    public DateTimeOffset? SessionExpiresAt => session.ExpiresAt;

    /// <summary>
    /// Oublie les jetons de la session, si bien que <see cref="IsAuthenticated"/> redevient faux.
    /// N'appelle pas l'API : elle n'expose pas de route de deconnexion, le jeton de
    /// rafraichissement reste donc valable cote serveur jusqu'a son expiration ou sa rotation.
    /// </summary>
    public void Logout()
    {
        session.Close();
    }

    public async Task<LoginResponse> LoginAsync(
        string apiBaseUrl,
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, "/api/v1/auth/login", request, includeAuthorization: false, cancellationToken);
        var login = await ReadResponseAsync<LoginResponse>(response, cancellationToken);
        session.Open(login);
        return login;
    }

    public async Task<IReadOnlyCollection<HotelUnitResponse>> GetHotelUnitsAsync(
        string apiBaseUrl,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = includeInactive ? "?includeInactive=true" : string.Empty;
        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"/api/v1/organization/hotel-units{query}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<HotelUnitResponse>>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<DailyRevenueResponse>> GetDailyRevenueAsync(
        string apiBaseUrl,
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildQuery("/api/v1/revenue/daily", from, to, hotelUnitCode),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<DailyRevenueResponse>>(response, cancellationToken);
    }

    public async Task<DailyRevenueResponse> CreateDailyRevenueAsync(
        string apiBaseUrl,
        CreateDailyRevenueRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, "/api/v1/revenue/daily", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<DailyRevenueResponse>(response, cancellationToken);
    }

    public async Task<DailyRevenueResponse> SubmitDailyRevenueAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/revenue/daily/{id}/submit", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<DailyRevenueResponse>(response, cancellationToken);
    }

    public async Task<DailyRevenueResponse> ValidateDailyRevenueAsync(
        string apiBaseUrl,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/revenue/daily/{id}/validate", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<DailyRevenueResponse>(response, cancellationToken);
    }

    public async Task<DailyRevenueResponse> RejectDailyRevenueAsync(
        string apiBaseUrl,
        Guid id,
        RejectDailyRevenueRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/revenue/daily/{id}/reject", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<DailyRevenueResponse>(response, cancellationToken);
    }

    public async Task<DailyRevenueSummaryResponse> GetDailyRevenueSummaryAsync(
        string apiBaseUrl,
        DateOnly? from,
        DateOnly? to,
        string? hotelUnitCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildQuery("/api/v1/revenue/daily/summary", from, to, hotelUnitCode),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<DailyRevenueSummaryResponse>(response, cancellationToken);
    }

    public async Task<UnitDashboardResponse> GetUnitDashboardAsync(
        string apiBaseUrl,
        DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = "?date=" + Uri.EscapeDataString(businessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"/api/v1/revenue/daily/dashboard{query}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<UnitDashboardResponse>(response, cancellationToken);
    }

    public async Task<HotelUnitResponse> CreateHotelUnitAsync(
        string apiBaseUrl,
        CreateHotelUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, "/api/v1/organization/hotel-units", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<HotelUnitResponse>(response, cancellationToken);
    }

    public async Task<HotelUnitResponse> UpdateHotelUnitAsync(
        string apiBaseUrl,
        string code,
        UpdateHotelUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"/api/v1/organization/hotel-units/{Uri.EscapeDataString(code)}", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<HotelUnitResponse>(response, cancellationToken);
    }

    public async Task<HotelUnitResponse> SetHotelUnitActiveAsync(
        string apiBaseUrl,
        string code,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var action = isActive ? "activate" : "deactivate";
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/organization/hotel-units/{Uri.EscapeDataString(code)}/{action}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<HotelUnitResponse>(response, cancellationToken);
    }

    public async Task<PagedResult<AuditLogSummary>> GetAuditLogAsync(
        string apiBaseUrl,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? action,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildAuditQuery(from, to, action, page, pageSize),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<PagedResult<AuditLogSummary>>(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        string apiBaseUrl,
        HttpMethod method,
        string relativePath,
        object? payload,
        bool includeAuthorization,
        CancellationToken cancellationToken)
    {
        var uri = BuildUri(apiBaseUrl, relativePath);

        // Serialise une seule fois : la fabrique ci-dessous peut etre rappelee pour rejouer la
        // requete apres un renouvellement de jeton, et un HttpRequestMessage ne s'envoie qu'une fois.
        var json = payload is null ? null : JsonSerializer.Serialize(payload, payload.GetType(), JsonOptions);

        HttpResponseMessage response;

        try
        {
            if (includeAuthorization)
            {
                // Renouvellement proactif, traitement du 401 (un renouvellement, un rejeu) et
                // serialisation des renouvellements concurrents : tout est dans la politique.
                response = await session.SendAuthenticatedAsync(
                    BuildUri(apiBaseUrl, TokenRenewalPolicy.RefreshPath),
                    () => CreateRequest(method, uri, json),
                    cancellationToken);
            }
            else
            {
                using var request = CreateRequest(method, uri, json);
                response = await httpClient.SendAsync(request, cancellationToken);
            }
        }
        catch (SessionExpiredException ex)
        {
            // Le serveur a refuse le renouvellement : la session est deja fermee par la politique,
            // l'ecran affiche le message (InvalidOperationException) et l'operateur se reconnecte.
            RecordFailure(method, relativePath, (int)HttpStatusCode.Unauthorized, "SessionExpired", ex.Message);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // Depuis .NET 5, un depassement de HttpClient.Timeout arrive ici sous la forme d'une
            // TaskCanceledException et NON d'une HttpRequestException.
            RecordFailure(method, relativePath, null, "Timeout", ex.Message);
            throw;
        }
        catch (HttpRequestException ex)
        {
            RecordFailure(method, relativePath, null, "Network", ex.Message);
            throw;
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var message = await ReadErrorMessageAsync(response, cancellationToken);
        RecordFailure(method, relativePath, (int)response.StatusCode, "HttpError", message);
        throw new ApiRequestFailedException(response.StatusCode, message);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, string? json)
    {
        var request = new HttpRequestMessage(method, uri);

        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return request;
    }

    /// <summary>
    /// Tampon des echecs constates par ce poste, en attente de signalement (module 29). Il est
    /// alimente ICI parce que SendAsync est le seul endroit qui connaisse a la fois le verbe et la
    /// route : ni ApiRequestFailedException ni HttpRequestException ne les portent, et un
    /// enregistrement fait plus haut ne saurait pas quel appel a echoue.
    /// </summary>
    public ClientFailureBuffer Failures { get; } = new();

    private void RecordFailure(HttpMethod method, string relativePath, int? statusCode, string kind, string message)
    {
        // On n'enregistre PAS les echecs des routes du module 29 lui-meme : quand le lien est
        // coupe, le battement et le signalement echouent aussi, et les journaliser remplirait le
        // tampon de bruit en chassant les vraies erreurs metier qu'il est cense conserver.
        if (relativePath.StartsWith(SyncPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Le diagnostic ne doit jamais aggraver l'incident qu'il observe.
        try
        {
            Failures.Record(method.Method, relativePath, statusCode, kind, message);
        }
        catch
        {
            // Rien a faire : perdre une ligne de journal est sans consequence pour l'operateur.
        }
    }

    private static Uri BuildUri(string apiBaseUrl, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            throw new InvalidOperationException("API URL is required.");
        }

        var baseUrl = apiBaseUrl.Trim().TrimEnd('/');
        var path = relativePath.StartsWith("/", StringComparison.Ordinal) ? relativePath : "/" + relativePath;

        if (!Uri.TryCreate(baseUrl + path, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("API URL is invalid.");
        }

        return uri;
    }

    private static string BuildQuery(string basePath, DateOnly? from, DateOnly? to, string? hotelUnitCode)
    {
        var query = new List<string>();

        if (from.HasValue)
        {
            query.Add("from=" + Uri.EscapeDataString(from.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        if (to.HasValue)
        {
            query.Add("to=" + Uri.EscapeDataString(to.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        if (!string.IsNullOrWhiteSpace(hotelUnitCode))
        {
            query.Add("hotelUnitCode=" + Uri.EscapeDataString(hotelUnitCode.Trim()));
        }

        return query.Count == 0
            ? basePath
            : basePath + "?" + string.Join("&", query);
    }

    private static string BuildAuditQuery(DateTimeOffset? from, DateTimeOffset? to, string? action, int page, int pageSize)
    {
        var query = new List<string>
        {
            "page=" + page.ToString(CultureInfo.InvariantCulture),
            "pageSize=" + pageSize.ToString(CultureInfo.InvariantCulture)
        };

        if (from.HasValue)
        {
            query.Add("from=" + Uri.EscapeDataString(from.Value.ToString("O", CultureInfo.InvariantCulture)));
        }

        if (to.HasValue)
        {
            query.Add("to=" + Uri.EscapeDataString(to.Value.ToString("O", CultureInfo.InvariantCulture)));
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query.Add("action=" + Uri.EscapeDataString(action.Trim()));
        }

        return "/api/v1/audit?" + string.Join("&", query);
    }

    private static async Task<T> ReadResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);

        return result ?? throw new InvalidOperationException("API returned an empty response.");
    }

    private static async Task<string> ReadErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(body))
        {
            return $"API request failed with status {(int)response.StatusCode}.";
        }

        try
        {
            var error = JsonSerializer.Deserialize<ApiErrorResponse>(body, JsonOptions);

            if (!string.IsNullOrWhiteSpace(error?.Message))
            {
                return error.Message;
            }
        }
        catch (JsonException)
        {
        }

        return body;
    }

    private void EnsureAuthenticated()
    {
        if (!IsAuthenticated)
        {
            throw new InvalidOperationException("Connexion requise avant d appeler l API.");
        }
    }

    private sealed record ApiErrorResponse(string? Message);
}
