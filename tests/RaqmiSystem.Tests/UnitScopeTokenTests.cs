using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Lot 2.2 - le perimetre dans le JETON, en HTTP complet : scope=global sans affectation, un
/// claim unit par code avec affectations, scope=none quand aucune n'est en validite ; la
/// reponse de connexion et /me le rendent lisible ; et le refresh RELIT la base au lieu de
/// recopier l'ancien jeton.
/// </summary>
public sealed class UnitScopeTokenTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private readonly RaqmiApiFactory _factory;

    public UnitScopeTokenTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task A_user_without_assignment_gets_a_global_scope_in_the_token_the_login_response_and_me()
    {
        await _factory.CreateUserAsync("scope.global", "scope.global@example.com", "Scope Global", Password, RoleCatalog.Reader);

        using var client = _factory.CreateClient();
        var login = await LoginAsync(client, "scope.global");

        var token = new JsonWebToken(login.AccessToken);
        Assert.Contains(token.Claims, claim => claim.Type == SecurityClaimTypes.Scope && claim.Value == SecurityClaimTypes.GlobalScope);
        Assert.DoesNotContain(token.Claims, claim => claim.Type == SecurityClaimTypes.Unit);

        Assert.True(login.User.UnitScope.IsGlobal);
        Assert.Empty(login.User.UnitScope.Units);

        var me = await ReadMeAsync(client, login.AccessToken);
        Assert.True(me.UnitScope.IsGlobal);
        Assert.Empty(me.UnitScope.Units);
    }

    [Fact]
    public async Task A_user_with_assignments_gets_one_unit_claim_per_unit_and_no_global_scope()
    {
        await _factory.CreateHotelUnitAsync("TOKA", "Token Hotel A");
        await _factory.CreateHotelUnitAsync("TOKB", "Token Hotel B");

        var userId = await _factory.CreateUserAsync("scope.units", "scope.units@example.com", "Scope Units", Password, RoleCatalog.Reader);
        await _factory.SetUserUnitsAsync(userId, null, "TOKB", "toka");

        using var client = _factory.CreateClient();
        var login = await LoginAsync(client, "scope.units");

        var token = new JsonWebToken(login.AccessToken);

        var units = token.Claims
            .Where(claim => claim.Type == SecurityClaimTypes.Unit)
            .Select(claim => claim.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "TOKA", "TOKB" }, units);
        Assert.DoesNotContain(token.Claims, claim => claim.Type == SecurityClaimTypes.Scope);

        Assert.False(login.User.UnitScope.IsGlobal);
        Assert.Equal(new[] { "TOKA", "TOKB" }, login.User.UnitScope.Units);

        var me = await ReadMeAsync(client, login.AccessToken);
        Assert.False(me.UnitScope.IsGlobal);
        Assert.Equal(new[] { "TOKA", "TOKB" }, me.UnitScope.Units);
    }

    [Fact]
    public async Task A_user_whose_assignments_are_all_expired_is_restricted_to_no_unit()
    {
        await _factory.CreateHotelUnitAsync("TOKX", "Token Hotel Expired");

        var userId = await _factory.CreateUserAsync("scope.expired", "scope.expired@example.com", "Scope Expired", Password, RoleCatalog.Reader);
        await _factory.SetUserUnitsAsync(userId, DateTimeOffset.UtcNow.AddMinutes(-1), "TOKX");

        using var client = _factory.CreateClient();
        var login = await LoginAsync(client, "scope.expired");

        var token = new JsonWebToken(login.AccessToken);
        Assert.Contains(token.Claims, claim => claim.Type == SecurityClaimTypes.Scope && claim.Value == SecurityClaimTypes.NoUnitScope);
        Assert.DoesNotContain(token.Claims, claim => claim.Type == SecurityClaimTypes.Unit);

        var me = await ReadMeAsync(client, login.AccessToken);
        Assert.False(me.UnitScope.IsGlobal);
        Assert.Empty(me.UnitScope.Units);
    }

    [Fact]
    public async Task Refresh_rereads_the_scope_from_the_database_instead_of_copying_the_old_token()
    {
        await _factory.CreateHotelUnitAsync("TOKR", "Token Hotel Refresh");

        var userId = await _factory.CreateUserAsync("scope.refresh", "scope.refresh@example.com", "Scope Refresh", Password, RoleCatalog.Reader);

        using var client = _factory.CreateClient();
        var login = await LoginAsync(client, "scope.refresh");
        Assert.True(login.User.UnitScope.IsGlobal);

        // Une affectation posee APRES la connexion : le refresh doit la voir.
        await _factory.SetUserUnitsAsync(userId, null, "TOKR");

        var restricted = await RefreshAsync(client, login.RefreshToken);
        Assert.False(restricted.User.UnitScope.IsGlobal);
        Assert.Equal(new[] { "TOKR" }, restricted.User.UnitScope.Units);
        Assert.Contains(new JsonWebToken(restricted.AccessToken).Claims, claim => claim.Type == SecurityClaimTypes.Unit && claim.Value == "TOKR");

        // Et une affectation RETIREE entre deux refresh disparait du jeton suivant.
        await _factory.SetUserUnitsAsync(userId, null);

        var global = await RefreshAsync(client, restricted.RefreshToken);
        Assert.True(global.User.UnitScope.IsGlobal);
        Assert.Contains(new JsonWebToken(global.AccessToken).Claims, claim => claim.Type == SecurityClaimTypes.Scope && claim.Value == SecurityClaimTypes.GlobalScope);
        Assert.DoesNotContain(new JsonWebToken(global.AccessToken).Claims, claim => claim.Type == SecurityClaimTypes.Unit);
    }

    [Fact]
    public async Task The_reception_role_signs_in_with_the_front_desk_keys_only()
    {
        await _factory.CreateUserAsync("scope.reception", "scope.reception@example.com", "Scope Reception", Password, RoleCatalog.Reception);

        using var client = _factory.CreateClient();
        var login = await LoginAsync(client, "scope.reception");

        Assert.Contains(RoleCatalog.Reception, login.User.Roles);
        Assert.Contains(PermissionCatalog.LodgingCheckin, login.User.Permissions);
        Assert.Contains(PermissionCatalog.LodgingReserve, login.User.Permissions);
        Assert.Contains(PermissionCatalog.CustomersWrite, login.User.Permissions);

        // Ni caisse, ni night audit, ni decision de validation, ni emission de facture.
        Assert.DoesNotContain(PermissionCatalog.TreasuryWrite, login.User.Permissions);
        Assert.DoesNotContain(PermissionCatalog.RevenueWrite, login.User.Permissions);
        Assert.DoesNotContain(PermissionCatalog.LodgingNightAudit, login.User.Permissions);
        Assert.DoesNotContain(PermissionCatalog.ApprovalsDecide, login.User.Permissions);
        Assert.DoesNotContain(PermissionCatalog.InvoicesIssue, login.User.Permissions);
    }

    private static async Task<LoginResponse> LoginAsync(HttpClient client, string userName)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(userName, Password), RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(login);
        return login!;
    }

    private static async Task<LoginResponse> RefreshAsync(HttpClient client, string refreshToken)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(refreshToken), RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(login);
        return login!;
    }

    private static async Task<MeResponse> ReadMeAsync(HttpClient client, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await response.Content.ReadFromJsonAsync<MeResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(me);
        return me!;
    }

    private sealed record MeResponse(
        string Id,
        string UserName,
        string? Email,
        string[] Roles,
        string[] Permissions,
        UnitScopeResponse UnitScope);
}
