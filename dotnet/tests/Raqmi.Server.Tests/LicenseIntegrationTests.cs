using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Raqmi.Server.Tests;

public class LicenseIntegrationTests : IClassFixture<RaqmiServerFactory>
{
    private readonly HttpClient _client;

    public LicenseIntegrationTests(RaqmiServerFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Fingerprint_ReturnsStableValue()
    {
        var first = await _client.GetFromJsonAsync<JsonElement>("/api/v1/license/fingerprint");
        var second = await _client.GetFromJsonAsync<JsonElement>("/api/v1/license/fingerprint");

        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("fingerprint").GetString()));
        Assert.Equal(first.GetProperty("fingerprint").GetString(), second.GetProperty("fingerprint").GetString());
    }

    [Fact]
    public async Task Status_WithoutAuth_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/license/status");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Status_WithDemoToken_ReturnsValidProfessionalLicense()
    {
        var token = await LoginAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/license/status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("evaluation").GetProperty("valid").GetBoolean());
        Assert.Equal("Professional", body.GetProperty("pack").GetProperty("label").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("fingerprint").GetString()));
    }

    [Fact]
    public async Task Import_WithoutAdminToken_Returns403()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/license/import", new { content = "{}" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
