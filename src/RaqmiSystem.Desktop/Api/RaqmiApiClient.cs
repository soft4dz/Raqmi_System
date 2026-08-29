using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
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
public sealed partial class RaqmiApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private string? accessToken;

    static RaqmiApiClient()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(accessToken);

    /// <summary>
    /// Clears the current session token so <see cref="IsAuthenticated"/> reports false again.
    /// Does not call the API: the desktop client has no server-side session to invalidate,
    /// so signing out is purely a local state reset.
    /// </summary>
    public void Logout()
    {
        accessToken = null;
    }

    public async Task<LoginResponse> LoginAsync(
        string apiBaseUrl,
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, "/api/v1/auth/login", request, includeAuthorization: false, cancellationToken);
        var login = await ReadResponseAsync<LoginResponse>(response, cancellationToken);
        accessToken = login.AccessToken;
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
        using var request = new HttpRequestMessage(method, BuildUri(apiBaseUrl, relativePath));

        if (includeAuthorization)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (payload is not null)
        {
            var json = JsonSerializer.Serialize(payload, payload.GetType(), JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var message = await ReadErrorMessageAsync(response, cancellationToken);
        throw new ApiRequestFailedException(response.StatusCode, message);
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
