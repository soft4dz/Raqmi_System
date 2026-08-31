using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Crm;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Crm;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Couverture HTTP du module CRM : authentification exigee, lecture reservee a crm.read,
/// ecriture a crm.write, et mouvements de points a crm.loyalty - la cle distincte qui separe
/// « qualifier un client » de « deplacer des points que ce client peut depenser ».
///
/// Les trois cles viennent de PermissionCatalog : le seeder les seme au demarrage de la
/// fabrique et Program.cs enregistre une policy par cle du catalogue.
/// </summary>
public sealed class CrmEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private readonly RaqmiApiFactory _factory;

    public CrmEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Crm_endpoints_require_authentication()
    {
        using var client = _factory.CreateClient();

        var segments = await client.GetAsync("/api/v1/crm/segments");
        var guests = await client.GetAsync("/api/v1/crm/guests");
        var campaigns = await client.GetAsync("/api/v1/crm/campaigns");
        var interactions = await client.GetAsync("/api/v1/crm/interactions");

        Assert.Equal(HttpStatusCode.Unauthorized, segments.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, guests.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, campaigns.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, interactions.StatusCode);
    }

    [Fact]
    public async Task Reading_the_crm_requires_crm_read()
    {
        await CreateUserWithPermissionsAsync("crm.norights", "crm.norights@example.com");

        using var client = await _factory.CreateAuthenticatedClientAsync("crm.norights", Password);

        var segments = await client.GetAsync("/api/v1/crm/segments");
        var nps = await client.GetAsync("/api/v1/crm/satisfaction/nps?from=2030-01-01&to=2030-12-31");

        Assert.Equal(HttpStatusCode.Forbidden, segments.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, nps.StatusCode);
    }

    [Fact]
    public async Task A_reader_can_consult_but_never_write()
    {
        await CreateUserWithPermissionsAsync(
            "crm.reader",
            "crm.reader@example.com",
            PermissionCatalog.CrmRead);

        using var client = await _factory.CreateAuthenticatedClientAsync("crm.reader", Password);

        var segments = await client.GetAsync("/api/v1/crm/segments");
        Assert.Equal(HttpStatusCode.OK, segments.StatusCode);

        var created = await client.PostAsJsonAsync(
            "/api/v1/crm/segments",
            new CreateCustomerSegmentRequest("REFUSE", "Refusé"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, created.StatusCode);
    }

    /// <summary>
    /// La separation qui compte : le comptoir qualifie le client (crm.write) sans pouvoir
    /// crediter ni debiter son compte de points (crm.loyalty).
    /// </summary>
    [Fact]
    public async Task Writing_the_crm_does_not_open_the_point_ledger()
    {
        await CreateCustomerAsync("CLI-DESK", "Client comptoir");

        await CreateUserWithPermissionsAsync(
            "crm.desk",
            "crm.desk@example.com",
            PermissionCatalog.CrmRead,
            PermissionCatalog.CrmWrite);

        using var client = await _factory.CreateAuthenticatedClientAsync("crm.desk", Password);

        var qualified = await client.PutAsJsonAsync(
            "/api/v1/crm/guests/CLI-DESK",
            new SaveGuestProfileRequest(Preferences: "Étage élevé"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, qualified.StatusCode);

        var earned = await client.PostAsJsonAsync(
            "/api/v1/crm/loyalty/accounts/CLI-DESK/earn",
            new LoyaltyMovementRequest(100, new DateOnly(2030, 5, 1), "Séjour"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, earned.StatusCode);
    }

    [Fact]
    public async Task The_four_ledger_routes_all_require_crm_loyalty()
    {
        await CreateCustomerAsync("CLI-LEDGER", "Client fidélité");

        await CreateUserWithPermissionsAsync(
            "crm.programme",
            "crm.programme@example.com",
            PermissionCatalog.CrmRead,
            PermissionCatalog.CrmWrite,
            PermissionCatalog.CrmLoyalty);

        using var client = await _factory.CreateAuthenticatedClientAsync("crm.programme", Password);

        var earned = await client.PostAsJsonAsync(
            "/api/v1/crm/loyalty/accounts/CLI-LEDGER/earn",
            new LoyaltyMovementRequest(500, new DateOnly(2030, 5, 1), "Séjour de mai"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, earned.StatusCode);

        var statement = await earned.Content.ReadFromJsonAsync<LoyaltyStatementResponse>(RaqmiApiFactory.JsonOptions);
        Assert.Equal(500, statement!.Balance);

        // Le sens vient de la route, pas du corps : la meme quantite positive debite ici.
        var redeemed = await client.PostAsJsonAsync(
            "/api/v1/crm/loyalty/accounts/CLI-LEDGER/redeem",
            new LoyaltyMovementRequest(200, new DateOnly(2030, 5, 2), "Nuit offerte"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, redeemed.StatusCode);

        var afterRedeem = await redeemed.Content.ReadFromJsonAsync<LoyaltyStatementResponse>(RaqmiApiFactory.JsonOptions);
        Assert.Equal(300, afterRedeem!.Balance);

        // Un debit superieur au solde est une reponse metier (400), pas une erreur serveur.
        var overdraft = await client.PostAsJsonAsync(
            "/api/v1/crm/loyalty/accounts/CLI-LEDGER/redeem",
            new LoyaltyMovementRequest(1_000, new DateOnly(2030, 5, 3), "Trop"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, overdraft.StatusCode);
    }

    [Fact]
    public async Task A_campaign_runs_its_lifecycle_over_http()
    {
        await CreateUserWithPermissionsAsync(
            "crm.marketing",
            "crm.marketing@example.com",
            PermissionCatalog.CrmRead,
            PermissionCatalog.CrmWrite);

        using var client = await _factory.CreateAuthenticatedClientAsync("crm.marketing", Password);

        var created = await client.PostAsJsonAsync(
            "/api/v1/crm/campaigns",
            new CreateCampaignRequest(
                "HTTP-ETE",
                "Offre été",
                CampaignChannel.OnSite,
                new DateOnly(2030, 6, 1),
                new DateOnly(2030, 6, 30)),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var draft = await created.Content.ReadFromJsonAsync<CampaignResponse>(RaqmiApiFactory.JsonOptions);
        Assert.Equal(CampaignStatus.Draft, draft!.Status);
        Assert.True(draft.CanEdit);

        // Un raccourci de cycle de vie est une reponse metier (400), pas une erreur serveur.
        var launchedTooEarly = await client.PostAsync("/api/v1/crm/campaigns/HTTP-ETE/launch", null);
        Assert.Equal(HttpStatusCode.BadRequest, launchedTooEarly.StatusCode);

        var scheduled = await client.PostAsync("/api/v1/crm/campaigns/HTTP-ETE/schedule", null);
        Assert.Equal(HttpStatusCode.OK, scheduled.StatusCode);

        var launched = await client.PostAsync("/api/v1/crm/campaigns/HTTP-ETE/launch", null);
        Assert.Equal(HttpStatusCode.OK, launched.StatusCode);

        var running = await launched.Content.ReadFromJsonAsync<CampaignResponse>(RaqmiApiFactory.JsonOptions);
        Assert.Equal(CampaignStatus.Running, running!.Status);
        Assert.False(running.CanEdit);

        var audience = await client.GetAsync("/api/v1/crm/campaigns/HTTP-ETE/audience");
        Assert.Equal(HttpStatusCode.OK, audience.StatusCode);
    }

    [Fact]
    public async Task An_unknown_filter_value_is_answered_with_the_accepted_ones()
    {
        await CreateUserWithPermissionsAsync(
            "crm.filters",
            "crm.filters@example.com",
            PermissionCatalog.CrmRead);

        using var client = await _factory.CreateAuthenticatedClientAsync("crm.filters", Password);

        var badStatus = await client.GetAsync("/api/v1/crm/campaigns?status=inexistant");
        Assert.Equal(HttpStatusCode.BadRequest, badStatus.StatusCode);

        var badCategory = await client.GetAsync("/api/v1/crm/satisfaction?category=inexistant");
        Assert.Equal(HttpStatusCode.BadRequest, badCategory.StatusCode);

        // Le filtre reste facultatif : sans valeur, la liste repond normalement.
        var noFilter = await client.GetAsync("/api/v1/crm/campaigns");
        Assert.Equal(HttpStatusCode.OK, noFilter.StatusCode);
    }

    [Fact]
    public async Task The_nps_endpoint_demands_a_period()
    {
        await CreateUserWithPermissionsAsync(
            "crm.nps",
            "crm.nps@example.com",
            PermissionCatalog.CrmRead);

        using var client = await _factory.CreateAuthenticatedClientAsync("crm.nps", Password);

        var missingPeriod = await client.GetAsync("/api/v1/crm/satisfaction/nps");
        Assert.Equal(HttpStatusCode.BadRequest, missingPeriod.StatusCode);

        var inverted = await client.GetAsync("/api/v1/crm/satisfaction/nps?from=2030-12-31&to=2030-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, inverted.StatusCode);

        var period = await client.GetAsync("/api/v1/crm/satisfaction/nps?from=2030-01-01&to=2030-12-31");
        Assert.Equal(HttpStatusCode.OK, period.StatusCode);

        var summary = await period.Content.ReadFromJsonAsync<NpsSummaryResponse>(RaqmiApiFactory.JsonOptions);

        // Aucune reponse sur la periode : le score est absent, pas nul.
        Assert.Equal(0, summary!.AnswerCount);
        Assert.Null(summary.Nps);
    }

    [Fact]
    public async Task The_360_view_of_an_unknown_customer_is_not_found()
    {
        await CreateUserWithPermissionsAsync(
            "crm.view360",
            "crm.view360@example.com",
            PermissionCatalog.CrmRead);

        using var client = await _factory.CreateAuthenticatedClientAsync("crm.view360", Password);

        var response = await client.GetAsync("/api/v1/crm/guests/INEXISTANT/360");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task CreateCustomerAsync(string code, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        if (await dbContext.Set<Customer>().AnyAsync(customer => customer.Code == code))
        {
            return;
        }

        dbContext.Set<Customer>().Add(new Customer(code, name, CustomerType.Individual));
        await dbContext.SaveChangesAsync();
    }

    private async Task CreateUserWithPermissionsAsync(
        string userName,
        string email,
        params string[] permissionKeys)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var permissions = await dbContext.Permissions
            .Where(permission => permissionKeys.Contains(permission.Key))
            .ToArrayAsync();

        Assert.True(
            permissions.Length == permissionKeys.Length,
            "Permission keys are missing from the seeded PermissionCatalog: " +
            string.Join(", ", permissionKeys.Except(permissions.Select(permission => permission.Key))));

        var role = new Role(
            $"test.crm.{Guid.NewGuid():N}",
            "CRM test role",
            "Role dedicated to CRM endpoint tests.");

        foreach (var permission in permissions)
        {
            role.GrantPermission(permission, DateTimeOffset.UtcNow);
        }

        dbContext.Roles.Add(role);

        var user = new User(userName, email, userName, passwordHasher.Hash(Password), mustChangePassword: false);
        user.AssignRole(role, DateTimeOffset.UtcNow);
        dbContext.Users.Add(user);

        await dbContext.SaveChangesAsync();
    }
}
