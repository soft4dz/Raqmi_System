using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Tariffs;

namespace RaqmiSystem.Desktop.Api;

// Module Tarifs et conventions : appels du groupe /api/v1/tariffs (plans
// tarifaires, periodes de tarif, conventions clients et resolution d'un tarif).
// Fichier de classe partielle : SendAsync, ReadResponseAsync et
// EnsureAuthenticated sont definis dans RaqmiApiClient.cs.
public sealed partial class RaqmiApiClient
{
    private const string TariffsPlansPath = "/api/v1/tariffs/plans";

    private const string TariffsConventionsPath = "/api/v1/tariffs/conventions";

    // ================================ Plans tarifaires =============================

    public async Task<IReadOnlyCollection<RatePlanResponse>> GetRatePlansAsync(
        string apiBaseUrl,
        string? hotelUnitCode,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();

        AppendTariffsText(query, "hotelUnitCode", hotelUnitCode);

        if (includeInactive)
        {
            query.Add("includeInactive=true");
        }

        var path = query.Count == 0
            ? TariffsPlansPath
            : TariffsPlansPath + "?" + string.Join("&", query);

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<RatePlanResponse>>(response, cancellationToken);
    }

    public async Task<RatePlanResponse> CreateRatePlanAsync(
        string apiBaseUrl,
        CreateRatePlanRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, TariffsPlansPath, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<RatePlanResponse>(response, cancellationToken);
    }

    public async Task<RatePlanResponse> UpdateRatePlanAsync(
        string apiBaseUrl,
        string code,
        UpdateRatePlanRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"{TariffsPlansPath}/{Uri.EscapeDataString(code)}", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<RatePlanResponse>(response, cancellationToken);
    }

    public async Task<RatePlanResponse> SetRatePlanDefaultAsync(
        string apiBaseUrl,
        string code,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{TariffsPlansPath}/{Uri.EscapeDataString(code)}/set-default", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<RatePlanResponse>(response, cancellationToken);
    }

    public async Task<RatePlanResponse> SetRatePlanActiveAsync(
        string apiBaseUrl,
        string code,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var action = isActive ? "activate" : "deactivate";
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{TariffsPlansPath}/{Uri.EscapeDataString(code)}/{action}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<RatePlanResponse>(response, cancellationToken);
    }

    // ================================ Periodes de tarif ============================

    public async Task<IReadOnlyCollection<RatePeriodResponse>> GetRatePeriodsAsync(
        string apiBaseUrl,
        string planCode,
        string? roomTypeCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var basePath = $"{TariffsPlansPath}/{Uri.EscapeDataString(planCode)}/periods";
        var query = new List<string>();

        AppendTariffsText(query, "roomTypeCode", roomTypeCode);

        var path = query.Count == 0
            ? basePath
            : basePath + "?" + string.Join("&", query);

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<RatePeriodResponse>>(response, cancellationToken);
    }

    public async Task<RatePeriodResponse> CreateRatePeriodAsync(
        string apiBaseUrl,
        string planCode,
        CreateRatePeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{TariffsPlansPath}/{Uri.EscapeDataString(planCode)}/periods", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<RatePeriodResponse>(response, cancellationToken);
    }

    public async Task<RatePeriodResponse> DeleteRatePeriodAsync(
        string apiBaseUrl,
        string planCode,
        Guid periodId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Delete, $"{TariffsPlansPath}/{Uri.EscapeDataString(planCode)}/periods/{periodId}", null, includeAuthorization: true, cancellationToken);

        // L'API renvoie la periode supprimee (Results.Ok du ToHttpResult).
        return await ReadResponseAsync<RatePeriodResponse>(response, cancellationToken);
    }

    // =============================== Conventions clients ===========================

    public async Task<IReadOnlyCollection<CustomerConventionResponse>> GetCustomerConventionsAsync(
        string apiBaseUrl,
        string? customerCode,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();

        AppendTariffsText(query, "customerCode", customerCode);

        if (includeInactive)
        {
            query.Add("includeInactive=true");
        }

        var path = query.Count == 0
            ? TariffsConventionsPath
            : TariffsConventionsPath + "?" + string.Join("&", query);

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<CustomerConventionResponse>>(response, cancellationToken);
    }

    public async Task<CustomerConventionResponse> CreateCustomerConventionAsync(
        string apiBaseUrl,
        CreateCustomerConventionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, TariffsConventionsPath, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<CustomerConventionResponse>(response, cancellationToken);
    }

    public async Task<CustomerConventionResponse> UpdateCustomerConventionAsync(
        string apiBaseUrl,
        Guid id,
        UpdateCustomerConventionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"{TariffsConventionsPath}/{id}", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<CustomerConventionResponse>(response, cancellationToken);
    }

    public async Task<CustomerConventionResponse> SetCustomerConventionActiveAsync(
        string apiBaseUrl,
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var action = isActive ? "activate" : "deactivate";
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{TariffsConventionsPath}/{id}/{action}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<CustomerConventionResponse>(response, cancellationToken);
    }

    // ================================== Resolution =================================

    /// <summary>
    /// Miroir de diagnostic de la resolution d'un tarif : ce que couterait une nuit
    /// pour une unite + type de chambre + date, avec application de la convention
    /// active du client quand un code client est fourni. Lecture seule.
    /// </summary>
    public async Task<ResolvedNightlyRate> ResolveTariffAsync(
        string apiBaseUrl,
        string hotelUnitCode,
        string roomTypeCode,
        DateOnly night,
        string? customerCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();

        AppendTariffsText(query, "hotelUnitCode", hotelUnitCode);
        AppendTariffsText(query, "roomTypeCode", roomTypeCode);
        query.Add("night=" + Uri.EscapeDataString(night.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        AppendTariffsText(query, "customerCode", customerCode);

        var path = "/api/v1/tariffs/resolve?" + string.Join("&", query);
        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<ResolvedNightlyRate>(response, cancellationToken);
    }

    // =================================== Requetes ==================================

    private static void AppendTariffsText(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add(name + "=" + Uri.EscapeDataString(value.Trim()));
        }
    }
}
