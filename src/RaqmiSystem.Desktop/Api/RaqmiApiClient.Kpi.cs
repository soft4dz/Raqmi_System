using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Kpi;
using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Desktop.Api;

// Partie "Bibliotheque KPI" du client API : la section /api/v1/kpis de KpiEndpoints.
// Fichier de classe partielle, pour que ce chantier n'entre pas en conflit avec les autres
// modules qui alimentent le meme client.
//
// Seuls les appels reellement utilises par l'ecran KPI figurent ici : le tableau de bord, le
// comparatif inter-unites, l'historique d'un indicateur, et le parametrage (seuils, mapping de
// comptes, instantanes). Le catalogue et l'indicateur unitaire ne sont pas exposes tant
// qu'aucune vue ne les declenche - le tableau de bord porte deja les fiches completes.
public sealed partial class RaqmiApiClient
{
    private const string KpiBasePath = "/api/v1/kpis";

    /// <summary>
    /// Charge le tableau de bord KPI sur [from, to] : indicateurs de tete, bibliotheque rangee
    /// par famille, unites du groupe et alertes, compares a la periode equivalente un an plus
    /// tot. Lecture pure - aucune ecriture derriere cet appel.
    /// </summary>
    public async Task<KpiDashboardResponse> GetKpiDashboardAsync(
        string apiBaseUrl,
        DateOnly from,
        DateOnly to,
        string? hotelUnitCode = null,
        KpiDsoMethod dsoMethod = KpiDsoMethod.Simple,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var path = KpiBasePath + "/dashboard" + BuildKpiPeriodQuery(from, to)
            + (string.IsNullOrWhiteSpace(hotelUnitCode)
                ? string.Empty
                : "&unitId=" + Uri.EscapeDataString(hotelUnitCode))
            + "&dsoMethod=" + dsoMethod;

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<KpiDashboardResponse>(response, cancellationToken);
    }

    /// <summary>Charge le comparatif inter-unites et ses classements sur [from, to].</summary>
    public async Task<KpiComparisonResponse> GetKpiComparisonAsync(
        string apiBaseUrl,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            KpiBasePath + "/compare" + BuildKpiPeriodQuery(from, to),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<KpiComparisonResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Charge l'historique conserve d'un indicateur sur un perimetre. Le serveur REND les
    /// instantanes deja poses, il ne recalcule jamais le passe.
    /// </summary>
    public async Task<KpiHistoryResponse> GetKpiHistoryAsync(
        string apiBaseUrl,
        string code,
        DateOnly from,
        DateOnly to,
        string? hotelUnitCode = null,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var path = KpiBasePath + "/" + Uri.EscapeDataString(code) + "/history"
            + BuildKpiPeriodQuery(from, to)
            + (string.IsNullOrWhiteSpace(hotelUnitCode)
                ? string.Empty
                : "&unitId=" + Uri.EscapeDataString(hotelUnitCode));

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<KpiHistoryResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<KpiThresholdResponse>> GetKpiThresholdsAsync(
        string apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            KpiBasePath + "/thresholds",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<KpiThresholdResponse>>(response, cancellationToken);
    }

    /// <summary>
    /// Cree ou remplace la regle de seuils d'un couple (indicateur, perimetre). Le serveur
    /// verifie la coherence des bornes avec le sens de lecture de l'indicateur.
    /// </summary>
    public async Task<KpiThresholdResponse> SaveKpiThresholdAsync(
        string apiBaseUrl,
        SaveKpiThresholdRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, KpiBasePath + "/thresholds", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<KpiThresholdResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<KpiAccountMappingResponse>> GetKpiAccountMappingsAsync(
        string apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            KpiBasePath + "/account-mappings",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<KpiAccountMappingResponse>>(response, cancellationToken);
    }

    /// <summary>
    /// Cree ou remplace le rattachement d'un prefixe de compte a un groupe de gestion - la piece
    /// sans laquelle GOP, EBE et marges repondent "donnee manquante".
    /// </summary>
    public async Task<KpiAccountMappingResponse> SaveKpiAccountMappingAsync(
        string apiBaseUrl,
        SaveKpiAccountMappingRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, KpiBasePath + "/account-mappings", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<KpiAccountMappingResponse>(response, cancellationToken);
    }

    /// <summary>Pose les instantanes d'une periode (rafraichit les provisoires, ne touche jamais un cloture).</summary>
    public async Task<KpiSnapshotBatchResponse> CaptureKpiSnapshotsAsync(
        string apiBaseUrl,
        CaptureKpiSnapshotsRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, KpiBasePath + "/snapshots", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<KpiSnapshotBatchResponse>(response, cancellationToken);
    }

    /// <summary>Fige les instantanes d'une periode. Irreversible : le serveur refusera tout recalcul ensuite.</summary>
    public async Task<KpiSnapshotBatchResponse> CloseKpiSnapshotsAsync(
        string apiBaseUrl,
        CloseKpiSnapshotsRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, KpiBasePath + "/snapshots/close", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<KpiSnapshotBatchResponse>(response, cancellationToken);
    }

    private static string BuildKpiPeriodQuery(DateOnly from, DateOnly to)
    {
        return "?from=" + from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            + "&to=" + to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
