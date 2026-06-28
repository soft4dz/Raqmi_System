using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Raqmi.Client;

public sealed class ClientConfig
{
    public string ServerUrl { get; set; } = "http://localhost:3000";
    public string Locale { get; set; } = "fr";
    public string? ActiveSiteId { get; set; }
}

public static class ConfigStore
{
    private static string ConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Raqmi System Client", "config.json");

    public static async Task<ClientConfig> LoadAsync()
    {
        try
        {
            var json = await File.ReadAllTextAsync(ConfigPath);
            return JsonSerializer.Deserialize<ClientConfig>(json) ?? new ClientConfig();
        }
        catch
        {
            return new ClientConfig();
        }
    }

    public static async Task SaveAsync(ClientConfig config)
    {
        var dir = Path.GetDirectoryName(ConfigPath)!;
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(ConfigPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class RaqmiApiClient
{
    private readonly HttpClient _http = new();
    private string? _token;

    public HttpClient Http => _http;
    public string? Token => _token;

    public string ServerUrl { get; private set; } = "http://localhost:3000";

    public async Task ConfigureAsync(ClientConfig config)
    {
        ServerUrl = ServerUrlNormalizer.Normalize(config.ServerUrl).TrimEnd('/');
        _http.BaseAddress = new Uri(ServerUrl);
    }

    public async Task<bool> TestServerAsync()
    {
        try
        {
            var response = await _http.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>()
            ?? throw new InvalidOperationException("Réponse login invalide");
        _token = body.Token;
    }

    public async Task<DashboardData> LoadDashboardAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/modules");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        var modulesResponse = await _http.SendAsync(request);
        modulesResponse.EnsureSuccessStatusCode();
        var modules = (await modulesResponse.Content.ReadFromJsonAsync<ModulesResponse>())?.Modules ?? [];

        using var licenseRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/license/status");
        licenseRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        var licenseResponse = await _http.SendAsync(licenseRequest);
        licenseResponse.EnsureSuccessStatusCode();
        var license = await licenseResponse.Content.ReadFromJsonAsync<LicenseStatusResponse>()
            ?? throw new InvalidOperationException("Licence invalide");

        return new DashboardData(modules, license);
    }

    private sealed record LoginResponse(string Token);
}

public sealed record ModuleDto(string Code, string Label, string Family, bool Commercial, string Description, bool Enabled);
public sealed record ModulesResponse(List<ModuleDto> Modules);
public sealed record LicenseStatusResponse(object Tenant, object License, LicenseEvaluationDto Evaluation, PackDto? Pack);
public sealed record LicenseEvaluationDto(bool Valid, bool ReadonlyMode, string? Reason);
public sealed record PackDto(string Label, string Description);
public sealed record DashboardData(List<ModuleDto> Modules, LicenseStatusResponse License);
