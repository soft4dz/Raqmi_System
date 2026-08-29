using System.Net.Http;
using RaqmiSystem.Application.Closing;

namespace RaqmiSystem.Desktop.Api;

// Cloture journaliere & night audit : appels du groupe /api/v1/closing/daily
// (ClosingEndpoints.cs). Fichier de classe partielle, voir RaqmiApiClient.cs.
public sealed partial class RaqmiApiClient
{
    /// <summary>
    /// Liste les clotures de la periode, eventuellement restreintes a une unite.
    /// Permission serveur : closing.read.
    /// </summary>
    public async Task<IReadOnlyCollection<DailyClosingResponse>> GetDailyClosingsAsync(
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
            BuildQuery("/api/v1/closing/daily", from, to, hotelUnitCode),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<DailyClosingResponse>>(response, cancellationToken);
    }

    /// <summary>
    /// Cloture officiellement une journee d'exploitation pour une unite.
    /// Le serveur refuse (400) tant qu'une recette de la journee est en brouillon
    /// ou soumise, et (409) si la journee est deja cloturee.
    /// Permission serveur : closing.close.
    /// </summary>
    public async Task<DailyClosingResponse> CloseBusinessDayAsync(
        string apiBaseUrl,
        CloseBusinessDayRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, "/api/v1/closing/daily/close", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<DailyClosingResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Rouvre une journee cloturee : acte de controle, le motif est obligatoire.
    /// Permission serveur : closing.reopen.
    /// </summary>
    public async Task<DailyClosingResponse> ReopenDailyClosingAsync(
        string apiBaseUrl,
        Guid id,
        ReopenDailyClosingRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"/api/v1/closing/daily/{id}/reopen", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<DailyClosingResponse>(response, cancellationToken);
    }
}
