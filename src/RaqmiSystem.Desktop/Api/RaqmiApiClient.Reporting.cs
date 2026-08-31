using System.Net.Http;
using RaqmiSystem.Application.Reporting;

namespace RaqmiSystem.Desktop.Api;

// Module Rapports automatiques : appels du groupe /api/v1/reporting.
// Fichier de classe partielle, pour que ce chantier n'entre pas en conflit avec
// les autres modules qui alimentent le meme client API.
public sealed partial class RaqmiApiClient
{
    private const string ReportingPath = "/api/v1/reporting";

    /// <summary>
    /// Catalogue des rapports definis en code cote serveur : codes, titres,
    /// descriptions et parametres types. La vue construit ses champs de saisie
    /// a partir de cette reponse, jamais d'une liste recopiee localement.
    /// </summary>
    public async Task<IReadOnlyCollection<ReportDefinitionResponse>> GetReportCatalogAsync(
        string apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            $"{ReportingPath}/catalog",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<ReportDefinitionResponse>>(response, cancellationToken);
    }

    /// <summary>
    /// Execute un rapport du catalogue. Le resultat est structurel (colonnes,
    /// lignes, ligne de total) : la meme grille dynamique rend n'importe quel
    /// rapport, et l'execution est journalisee cote serveur.
    /// </summary>
    public async Task<ReportResultResponse> RunReportAsync(
        string apiBaseUrl,
        RunReportRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            $"{ReportingPath}/run",
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<ReportResultResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Journal des executions (les plus recentes d'abord), optionnellement filtre
    /// par code de rapport.
    /// </summary>
    public async Task<IReadOnlyCollection<ReportExecutionResponse>> GetReportExecutionsAsync(
        string apiBaseUrl,
        string? reportCode = null,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var path = string.IsNullOrWhiteSpace(reportCode)
            ? $"{ReportingPath}/executions"
            : $"{ReportingPath}/executions?reportCode=" + Uri.EscapeDataString(reportCode.Trim());

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            path,
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<ReportExecutionResponse>>(response, cancellationToken);
    }
}
