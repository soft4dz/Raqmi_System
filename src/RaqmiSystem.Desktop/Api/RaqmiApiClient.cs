using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Application.Revenue;

namespace RaqmiSystem.Desktop.Api;

public sealed class RaqmiApiClient(HttpClient httpClient)
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
            BuildDailyRevenueQuery(from, to, hotelUnitCode),
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

    private static string BuildDailyRevenueQuery(DateOnly? from, DateOnly? to, string? hotelUnitCode)
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
            ? "/api/v1/revenue/daily"
            : "/api/v1/revenue/daily?" + string.Join("&", query);
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
