using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Raqmi.Server.Tests;

public class ModulesIntegrationTests : IClassFixture<RaqmiServerFactory>
{
    private readonly HttpClient _client;

    public ModulesIntegrationTests(RaqmiServerFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_ReturnsOkDemoMode()
    {
        var body = await _client.GetFromJsonAsync<JsonElement>("/health");
        Assert.Equal("ok", body.GetProperty("status").GetString());
        Assert.Equal("demo", body.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Modules_WithoutAuth_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/modules");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Modules_WithDemoToken_Returns14EnabledModules()
    {
        var token = await LoginAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/modules");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var modules = body.GetProperty("modules").EnumerateArray().ToList();

        Assert.Equal(23, modules.Count);
        Assert.Equal(14, modules.Count(m => m.GetProperty("enabled").GetBoolean()));
    }

    [Fact]
    public async Task Catalog_ReturnsAllModulesEnabled()
    {
        var body = await _client.GetFromJsonAsync<JsonElement>("/api/v1/modules/catalog");
        var modules = body.GetProperty("modules").EnumerateArray().ToList();

        Assert.Equal(23, modules.Count);
        Assert.All(modules, m => Assert.True(m.GetProperty("enabled").GetBoolean()));
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@demo.raqmi.local",
            password = "demo1234",
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }
}
