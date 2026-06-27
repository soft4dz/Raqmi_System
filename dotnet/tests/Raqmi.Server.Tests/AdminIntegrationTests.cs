using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Raqmi.Server.Tests;

public class AdminIntegrationTests : IClassFixture<RaqmiServerFactory>
{
    private readonly HttpClient _client;

    public AdminIntegrationTests(RaqmiServerFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Users_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/admin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Users_WithAdminToken_ReturnsDemoUsers()
    {
        var token = await LoginAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("items").GetArrayLength() >= 3);
    }

    [Fact]
    public async Task Users_Create_WithAdminToken_Succeeds()
    {
        var token = await LoginAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            email = $"test-{Guid.NewGuid():N}@demo.raqmi.local",
            fullName = "Test User",
            roleCode = "user",
            siteIds = new[] { "demo-site-001" },
        });

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("siteIds").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Sites_WithAdminToken_ReturnsDemoSites()
    {
        var token = await LoginAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/sites");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("items").GetArrayLength() >= 2);
    }

    [Fact]
    public async Task Users_List_IncludesSiteIds()
    {
        var token = await LoginAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var first = body.GetProperty("items")[0];
        Assert.True(first.TryGetProperty("siteIds", out var siteIds));
        Assert.True(siteIds.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Roles_WithToken_ReturnsStandardRoles()
    {
        var token = await LoginAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/roles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, body.GetProperty("items").GetArrayLength());
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
