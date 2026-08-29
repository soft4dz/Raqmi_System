using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Settings;

namespace RaqmiSystem.Desktop.Api;

// Module Parametrage global : le parametrage serveur (/api/v1/settings),
// l'identite de la session courante (/api/v1/me), l'entretien du journal
// d'audit (/api/v1/audit/purge) et les deux sondes de sante publiques
// (/health et /health/database).
// Fichier de classe partielle, pour que ce chantier n'entre pas en conflit avec
// les autres modules qui alimentent le meme client API.
public sealed partial class RaqmiApiClient
{
    private const string SettingsPath = "/api/v1/settings";

    /// <summary>
    /// Lit le parametrage global. L'API ne renvoie jamais 404 : tant qu'aucun
    /// administrateur n'a rien enregistre, la reponse porte les valeurs par defaut
    /// de l'installation avec <c>IsConfigured</c> a false.
    /// </summary>
    public async Task<ApplicationSettingsResponse> GetApplicationSettingsAsync(
        string apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, SettingsPath, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<ApplicationSettingsResponse>(response, cancellationToken);
    }

    public async Task<ApplicationSettingsResponse> UpdateApplicationSettingsAsync(
        string apiBaseUrl,
        UpdateApplicationSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, SettingsPath, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<ApplicationSettingsResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Identite de la session telle que le SERVEUR la voit : utilisateur, roles et
    /// permissions portes par le jeton en cours. Source plus fiable que la reponse
    /// de connexion pour afficher un etat de session, car elle reflete le jeton
    /// reellement presente a chaque appel.
    /// </summary>
    public async Task<CurrentSessionResponse> GetCurrentSessionAsync(
        string apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, "/api/v1/me", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<CurrentSessionResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Purge les entrees du journal d'audit anterieures a <paramref name="olderThanDays"/>
    /// jours. Operation destructrice, exigee par l'API sous la permission
    /// <c>security.seed</c> (et non <c>audit.read</c>).
    /// </summary>
    public async Task<AuditPurgeResponse> PurgeAuditLogAsync(
        string apiBaseUrl,
        int olderThanDays,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var path = "/api/v1/audit/purge?olderThanDays=" + olderThanDays.ToString(CultureInfo.InvariantCulture);
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<AuditPurgeResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Sonde /health. Endpoint PUBLIC (voir Program.cs) : aucun jeton n'est presente,
    /// la sante du serveur doit rester consultable meme quand l'authentification est
    /// justement ce qui ne fonctionne pas.
    /// </summary>
    public async Task<ServerHealthResponse> GetServerHealthAsync(
        string apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        using var response = await ProbeAsync(apiBaseUrl, "/health", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new ServerHealthResponse(false, null, null, null, (int)response.StatusCode);
        }

        var payload = await ReadResponseAsync<HealthPayload>(response, cancellationToken);

        return new ServerHealthResponse(true, payload.Status, payload.Application, payload.Version, (int)response.StatusCode);
    }

    /// <summary>
    /// Sonde /health/database, publique elle aussi. Un 503 est une REPONSE (base
    /// injoignable), pas un echec d'appel : il est donc rendu comme un etat, et non
    /// leve en exception, pour que l'ecran puisse afficher un indicateur rouge
    /// plutot qu'un message d'erreur technique.
    /// </summary>
    public async Task<DatabaseHealthResponse> GetDatabaseHealthAsync(
        string apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        using var response = await ProbeAsync(apiBaseUrl, "/health/database", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new DatabaseHealthResponse(false, null, null, (int)response.StatusCode);
        }

        var payload = await ReadResponseAsync<HealthPayload>(response, cancellationToken);

        return new DatabaseHealthResponse(true, payload.Status, payload.Database, (int)response.StatusCode);
    }

    // Envoi sans jeton et SANS controle du code de statut : les sondes de sante
    // interpretent elles-memes le code renvoye. Une panne reseau (serveur eteint,
    // URL erronee) leve toujours, comme partout ailleurs dans ce client.
    private async Task<HttpResponseMessage> ProbeAsync(
        string apiBaseUrl,
        string relativePath,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(apiBaseUrl, relativePath));

        return await httpClient.SendAsync(request, cancellationToken);
    }

    // Corps commun des deux sondes : /health porte application et version,
    // /health/database porte le nom du moteur. Les champs absents restent nuls.
    private sealed record HealthPayload(string? Status, string? Application, string? Version, string? Database);
}

/// <summary>Etat du serveur applicatif tel que renvoye par /health.</summary>
public sealed record ServerHealthResponse(
    bool IsReachable,
    string? Status,
    string? Application,
    string? Version,
    int StatusCode);

/// <summary>Etat de la base tel que renvoye par /health/database.</summary>
public sealed record DatabaseHealthResponse(
    bool IsReachable,
    string? Status,
    string? Database,
    int StatusCode);

/// <summary>
/// Identite de la session courante renvoyee par /api/v1/me. Les collections sont
/// nullables : une reponse tronquee ne doit pas faire echouer la deserialisation.
/// </summary>
public sealed record CurrentSessionResponse(
    string? UserName,
    string? Email,
    IReadOnlyCollection<string>? Roles,
    IReadOnlyCollection<string>? Permissions);
