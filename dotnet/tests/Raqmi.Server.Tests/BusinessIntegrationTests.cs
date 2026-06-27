using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Raqmi.Server.Tests;

public class BusinessIntegrationTests : IClassFixture<BusinessDbServerFactory>
{
    private readonly HttpClient _client;

    public BusinessIntegrationTests(BusinessDbServerFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Finance_DailyRevenue_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/finance/daily-revenue");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Finance_DailyRevenue_WithToken_ReturnsList()
    {
        var token = await LoginAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/finance/daily-revenue");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.GetProperty("items").ValueKind);
    }

    [Fact]
    public async Task Finance_DailyRevenue_CreateAndValidate()
    {
        var token = await LoginAsync();
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/finance/daily-revenue");
        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        create.Content = JsonContent.Create(new { amount = 150.50m, category = "restaurant", notes = "test" });

        var createResponse = await _client.SendAsync(create);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(id));

        using var validate = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/finance/daily-revenue/{id}/validate");
        validate.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        validate.Content = JsonContent.Create(new { });
        var validateResponse = await _client.SendAsync(validate);
        validateResponse.EnsureSuccessStatusCode();
        var validated = await validateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validated", validated.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Hr_Employees_CreateAndList()
    {
        var token = await LoginAsync();
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/hr/employees");
        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        create.Content = JsonContent.Create(new { matricule = "EMP-TEST-01", firstName = "Jean", lastName = "Dupont", department = "FO" });

        var createResponse = await _client.SendAsync(create);
        createResponse.EnsureSuccessStatusCode();

        using var list = new HttpRequestMessage(HttpMethod.Get, "/api/v1/hr/employees");
        list.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var listResponse = await _client.SendAsync(list);
        listResponse.EnsureSuccessStatusCode();
        var body = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("items").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Stocks_Products_CreateAndList()
    {
        var token = await LoginAsync();
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/stocks/products");
        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        create.Content = JsonContent.Create(new { code = "PRD-TEST", name = "Produit test", unit = "kg" });

        var createResponse = await _client.SendAsync(create);
        createResponse.EnsureSuccessStatusCode();

        using var list = new HttpRequestMessage(HttpMethod.Get, "/api/v1/stocks/products");
        list.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var listResponse = await _client.SendAsync(list);
        listResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public void ModuleGuard_StarterPack_BlocksTreasuryModule()
    {
        var starterModules = Raqmi.Licensing.LicensePacks.All.First(p => p.Label == "Starter").Modules;
        var normalized = Raqmi.Licensing.ModuleEntitlement.NormalizeAllowedModules(starterModules);
        Assert.DoesNotContain("treasury", normalized);
        Assert.DoesNotContain("hr", normalized);
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
