using System.Net.Http;
using RaqmiSystem.Application.Sync;

namespace RaqmiSystem.Desktop.Api;

// Module 29 - Registre des postes & erreurs clients : appels du groupe /api/v1/sync.
// Fichier de classe partielle, pour que ce chantier n'entre pas en conflit avec les autres
// modules qui alimentent le meme client API.
public sealed partial class RaqmiApiClient
{
    private const string SyncPath = "/api/v1/sync";

    /// <summary>
    /// Declare ce poste au serveur. Diagnostic pur : l'appelant DOIT envelopper cet appel dans un
    /// try/catch total, car un battement qui echoue ne doit jamais interrompre le travail de
    /// l'operateur ni s'afficher comme une erreur metier.
    /// </summary>
    public async Task<WorkstationResponse> SendHeartbeatAsync(
        string apiBaseUrl,
        WorkstationHeartbeatRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{SyncPath}/stations/heartbeat", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<WorkstationResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Remonte un lot d'erreurs constatees par ce poste. Meme regle que le battement : l'appelant
    /// avale toute exception. Le serveur ignore les entrees deja connues, un lot renvoye ne cree
    /// donc pas de doublon.
    /// </summary>
    public async Task<int> ReportWorkstationFailuresAsync(
        string apiBaseUrl,
        ReportWorkstationFailuresRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{SyncPath}/stations/failures", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<int>(response, cancellationToken);
    }

    /// <summary>
    /// Lit le registre des postes (permission sync.read). Les seuils de fraicheur voyagent avec la
    /// reponse : l'ecran n'en recopie aucun.
    /// </summary>
    public async Task<WorkstationRegistryResponse> GetWorkstationsAsync(
        string apiBaseUrl,
        bool includeAllKnown = false,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = includeAllKnown ? "?includeAllKnown=true" : string.Empty;

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"{SyncPath}/stations{query}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<WorkstationRegistryResponse>(response, cancellationToken);
    }

    /// <summary>Lit le journal des erreurs remontees, du plus recent au plus ancien.</summary>
    public async Task<IReadOnlyCollection<WorkstationFailureResponse>> GetWorkstationFailuresAsync(
        string apiBaseUrl,
        int maxItems = 100,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"{SyncPath}/failures?maxItems={maxItems}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<WorkstationFailureResponse>>(response, cancellationToken);
    }
}
