using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Mice;
using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Lot 2.2 - le filtre de perimetre pose sur le groupe /api/v1 (UnitScopeEndpointFilter), en HTTP
/// complet : une unite hors perimetre est refusee en 403 avec ErrorResponse qu'elle vienne de la
/// ROUTE, de la REQUETE ou du CORPS, sans casse ; une unite dans le perimetre passe ; un perimetre
/// global n'est jamais filtre ; un perimetre restreint a aucune unite (scope=none) refuse tout ;
/// les routes de session, d'administration et d'organisation sont exemptees ; et une requete
/// anonyme reste un 401, jamais un 403 du filtre.
///
/// Tous les comptes restreints sont des administrateurs systeme (toutes les permissions) : un 403
/// ne peut alors venir que du filtre de perimetre, jamais d'une politique de permission - et le
/// corps ErrorResponse, que l'autorisation ne renvoie pas, le confirme.
/// </summary>
public sealed class UnitScopeEndpointFilterTests : IClassFixture<RaqmiApiFactory>, IAsyncLifetime
{
    private const string Password = "Correct-Horse-Battery-42!";
    private const string AllowedUnit = "FLTA";
    private const string ForbiddenUnit = "FLTB";

    private readonly RaqmiApiFactory _factory;

    public UnitScopeEndpointFilterTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        foreach (var code in new[] { AllowedUnit, ForbiddenUnit })
        {
            if (!await dbContext.HotelUnits.AnyAsync(unit => unit.Code == code))
            {
                dbContext.HotelUnits.Add(new Domain.Organization.HotelUnit(code, $"Filter Hotel {code}", Domain.Organization.HotelUnitType.Hotel));
            }
        }

        await dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_unit_in_the_route_outside_the_scope_is_refused_whatever_its_case_and_one_inside_is_served()
    {
        using var client = await CreateAdminClientAsync("filter.route", null, AllowedUnit);
        var body = new SaveFunctionSpaceRequest("Salle du filtre", 40, null, null);

        var refused = await client.PostAsJsonAsync($"/api/v1/mice/spaces/{ForbiddenUnit}/ROUTE1", body, RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        var error = await refused.Content.ReadFromJsonAsync<ApiError>(RaqmiApiFactory.JsonOptions);
        Assert.Contains(ForbiddenUnit, error!.Message);
        Assert.Contains(AllowedUnit, error.Message);

        // La casse ne contourne pas le filtre : « fltb » est la meme unite que « FLTB ».
        var lowered = await client.PostAsJsonAsync($"/api/v1/mice/spaces/{ForbiddenUnit.ToLowerInvariant()}/ROUTE1", body, RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, lowered.StatusCode);

        // Rien n'a ete ecrit pour l'unite refusee : le filtre repond AVANT le service.
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
            Assert.False(await dbContext.FunctionSpaces.AnyAsync(space => space.HotelUnitCode == ForbiddenUnit));
        }

        var accepted = await client.PostAsJsonAsync($"/api/v1/mice/spaces/{AllowedUnit}/ROUTE1", body, RaqmiApiFactory.JsonOptions);
        Assert.True(accepted.IsSuccessStatusCode, await accepted.Content.ReadAsStringAsync());

        var space = await accepted.Content.ReadFromJsonAsync<FunctionSpaceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.Equal(AllowedUnit, space!.HotelUnitCode);
    }

    [Fact]
    public async Task A_unit_in_the_query_string_outside_the_scope_is_refused_whatever_the_case_of_key_or_value()
    {
        using var client = await CreateAdminClientAsync("filter.query", null, AllowedUnit);

        var refused = await client.GetAsync($"/api/v1/revenue/daily?hotelUnitCode={ForbiddenUnit}");
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        var error = await refused.Content.ReadFromJsonAsync<ApiError>(RaqmiApiFactory.JsonOptions);
        Assert.Contains(ForbiddenUnit, error!.Message);

        // Cle et valeur sans casse : « hotelunitcode=fltb » est refuse comme « hotelUnitCode=FLTB ».
        var lowered = await client.GetAsync($"/api/v1/revenue/daily?hotelunitcode={ForbiddenUnit.ToLowerInvariant()}");
        Assert.Equal(HttpStatusCode.Forbidden, lowered.StatusCode);

        // Le resume de la meme famille de routes est filtre de la meme facon.
        var summary = await client.GetAsync($"/api/v1/revenue/daily/summary?hotelUnitCode={ForbiddenUnit}");
        Assert.Equal(HttpStatusCode.Forbidden, summary.StatusCode);

        var accepted = await client.GetAsync($"/api/v1/revenue/daily?hotelUnitCode={AllowedUnit}");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        // LIMITE ASSUMEE (docs/security.md) : une liste appelee SANS parametre d'unite n'est pas
        // filtree, elle rend toutes les unites. Ce test fige l'etat courant pour que le lot A6b
        // (controle dans les services metier) le change sciemment, pas par accident.
        var unfiltered = await client.GetAsync("/api/v1/revenue/daily");
        Assert.Equal(HttpStatusCode.OK, unfiltered.StatusCode);
    }

    [Fact]
    public async Task A_unit_in_the_request_body_outside_the_scope_is_refused_and_one_inside_is_served()
    {
        using var client = await CreateAdminClientAsync("filter.body", null, AllowedUnit);
        var businessDate = new DateOnly(2026, 3, 1);

        var refused = await client.PostAsJsonAsync(
            "/api/v1/revenue/daily",
            new CreateDailyRevenueRequest(businessDate, ForbiddenUnit, 1000m, 200m, 100m, 0m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        var error = await refused.Content.ReadFromJsonAsync<ApiError>(RaqmiApiFactory.JsonOptions);
        Assert.Contains(ForbiddenUnit, error!.Message);

        // Le corps est lu tel que le serveur le lie : la casse du code n'y change rien non plus.
        var lowered = await client.PostAsJsonAsync(
            "/api/v1/revenue/daily",
            new CreateDailyRevenueRequest(businessDate, ForbiddenUnit.ToLowerInvariant(), 1000m, 200m, 100m, 0m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, lowered.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
            Assert.False(await dbContext.DailyRevenues.AnyAsync(revenue => revenue.HotelUnitCode == ForbiddenUnit));
        }

        var accepted = await client.PostAsJsonAsync(
            "/api/v1/revenue/daily",
            new CreateDailyRevenueRequest(businessDate, AllowedUnit, 1000m, 200m, 100m, 0m),
            RaqmiApiFactory.JsonOptions);

        Assert.True(accepted.IsSuccessStatusCode, await accepted.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_global_scope_is_never_filtered_and_a_restricted_scope_without_unit_is_refused_everywhere()
    {
        // Aucune affectation = global : l'unite « interdite » des autres tests passe.
        using var global = await CreateAdminClientAsync("filter.global");

        var served = await global.GetAsync($"/api/v1/revenue/daily?hotelUnitCode={ForbiddenUnit}");
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);

        // Une affectation expiree = restreint a AUCUNE unite (scope=none) : meme la sienne est refusee.
        using var none = await CreateAdminClientAsync("filter.none", DateTimeOffset.UtcNow.AddMinutes(-1), AllowedUnit);

        var refused = await none.GetAsync($"/api/v1/revenue/daily?hotelUnitCode={AllowedUnit}");
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        var error = await refused.Content.ReadFromJsonAsync<ApiError>(RaqmiApiFactory.JsonOptions);
        Assert.Contains("aucune", error!.Message);
    }

    [Fact]
    public async Task Session_administration_and_organization_routes_are_exempt_from_the_scope()
    {
        using var client = await CreateAdminClientAsync("filter.exempt", null, AllowedUnit);
        var targetId = await _factory.CreateUserAsync("filter.exempt.target", "filter.exempt.target@example.com", "Exempt Target", Password, RoleCatalog.Reader);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/settings")).StatusCode);

        // Les unites elles-memes se listent et s'administrent hors perimetre : la liste rend
        // aussi l'unite que cet administrateur ne peut pas exploiter.
        var units = await client.GetFromJsonAsync<Application.Organization.HotelUnitResponse[]>("/api/v1/organization/hotel-units", RaqmiApiFactory.JsonOptions);
        Assert.Contains(units!, unit => unit.Code == ForbiddenUnit);

        // Un administrateur restreint a FLTA affecte FLTB a un autre compte : administrer le
        // perimetre n'exige pas d'y etre soi-meme affecte (route sous /security, exemptee).
        var assigned = await client.PutAsJsonAsync(
            $"/api/v1/security/users/{targetId}/units",
            new SetUserUnitsRequest([ForbiddenUnit]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);

        var read = await client.GetFromJsonAsync<UserUnitScopeResponse>($"/api/v1/security/users/{targetId}/units", RaqmiApiFactory.JsonOptions);
        Assert.Equal(new[] { ForbiddenUnit }, read!.Units.Select(unit => unit.HotelUnitCode));

        // Les routes de session (/auth/login, /auth/refresh) sont publiques et couvertes par
        // UnitScopeTokenTests : un compte restreint s'y connecte et s'y rafraichit.
    }

    [Fact]
    public async Task An_anonymous_request_is_still_a_401_the_filter_never_answers_before_authentication()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/revenue/daily?hotelUnitCode={ForbiddenUnit}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Un administrateur systeme (toutes les permissions) au perimetre donne : aucune unite =
    /// global ; <paramref name="validTo"/> passe permet une affectation deja expiree (scope=none).
    /// </summary>
    private async Task<HttpClient> CreateAdminClientAsync(string userName, DateTimeOffset? validTo = null, params string[] hotelUnitCodes)
    {
        var userId = await _factory.CreateUserAsync(userName, $"{userName}@example.com", userName, Password, RoleCatalog.SystemAdministrator);
        await _factory.SetUserUnitsAsync(userId, validTo, hotelUnitCodes);

        return await _factory.CreateAuthenticatedClientAsync(userName, Password);
    }

    private sealed record ApiError(string Message);
}
