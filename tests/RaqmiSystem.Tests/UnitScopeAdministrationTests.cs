using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Lot 2.2 - administration du perimetre : GET /security/users/{id}/units (users.read) et
/// PUT /security/users/{id}/units (users.write), remplacement complet, codes normalises, code
/// inconnu refuse en bloc, audit, et le jeton suivant du compte qui suit le changement.
/// </summary>
public sealed class UnitScopeAdministrationTests : IClassFixture<RaqmiApiFactory>, IAsyncLifetime
{
    private const string Password = "Correct-Horse-Battery-42!";

    private readonly RaqmiApiFactory _factory;

    public UnitScopeAdministrationTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        foreach (var code in new[] { "ADMA", "ADMB" })
        {
            if (!await dbContext.HotelUnits.AnyAsync(unit => unit.Code == code))
            {
                dbContext.HotelUnits.Add(new Domain.Organization.HotelUnit(code, $"Admin Hotel {code}", Domain.Organization.HotelUnitType.Hotel));
            }
        }

        await dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Reading_units_needs_users_read_and_changing_them_needs_users_write()
    {
        var targetId = await _factory.CreateUserAsync("units.target.ro", "units.target.ro@example.com", "Target", Password, RoleCatalog.Reader);

        // Un compte dedie qui ne detient QUE users.read : le 403 du PUT ne peut venir que de
        // la politique users.write.
        using var readerClient = await CreateClientWithPermissionsAsync("units.readonly", PermissionCatalog.UsersRead);

        var read = await readerClient.GetAsync($"/api/v1/security/users/{targetId}/units");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var forbidden = await readerClient.PutAsJsonAsync(
            $"/api/v1/security/users/{targetId}/units",
            new SetUserUnitsRequest(["ADMA"]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Replacing_the_units_is_complete_normalized_audited_and_reversible()
    {
        var targetId = await _factory.CreateUserAsync("units.target", "units.target@example.com", "Units Target", Password, RoleCatalog.Reader);
        await _factory.CreateUserAsync("units.admin", "units.admin@example.com", "Units Admin", Password, RoleCatalog.SystemAdministrator);
        using var admin = await _factory.CreateAuthenticatedClientAsync("units.admin", Password);

        var initial = await admin.GetFromJsonAsync<UserUnitScopeResponse>($"/api/v1/security/users/{targetId}/units", RaqmiApiFactory.JsonOptions);
        Assert.True(initial!.IsGlobal);
        Assert.Empty(initial.Units);
        Assert.Equal("units.target", initial.UserName);

        // Codes en minuscules, avec espaces et doublons : normalises et fondus.
        var first = await admin.PutAsJsonAsync(
            $"/api/v1/security/users/{targetId}/units",
            new SetUserUnitsRequest(["admb", " ADMA ", "adma"]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var restricted = await first.Content.ReadFromJsonAsync<UserUnitScopeResponse>(RaqmiApiFactory.JsonOptions);
        Assert.False(restricted!.IsGlobal);
        Assert.Equal(new[] { "ADMA", "ADMB" }, restricted.Units.Select(unit => unit.HotelUnitCode));
        Assert.All(restricted.Units, unit => Assert.Equal("units.admin", unit.AssignedBy));

        var admbAssignedAt = restricted.Units.Single(unit => unit.HotelUnitCode == "ADMB").AssignedAt;

        // Remplacement : ADMA part, ADMB reste avec sa date d'affectation d'origine.
        var second = await admin.PutAsJsonAsync(
            $"/api/v1/security/users/{targetId}/units",
            new SetUserUnitsRequest(["ADMB"]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var narrowed = await second.Content.ReadFromJsonAsync<UserUnitScopeResponse>(RaqmiApiFactory.JsonOptions);
        var kept = Assert.Single(narrowed!.Units);
        Assert.Equal("ADMB", kept.HotelUnitCode);
        Assert.Equal(admbAssignedAt, kept.AssignedAt);

        // Le jeton suivant du compte porte le nouveau perimetre.
        using var targetClient = await _factory.CreateAuthenticatedClientAsync("units.target", Password);
        var me = await targetClient.GetFromJsonAsync<MeResponse>("/api/v1/me", RaqmiApiFactory.JsonOptions);
        Assert.False(me!.UnitScope.IsGlobal);
        Assert.Equal(new[] { "ADMB" }, me.UnitScope.Units);

        // Liste vide : retour au global, explicitement.
        var cleared = await admin.PutAsJsonAsync(
            $"/api/v1/security/users/{targetId}/units",
            new SetUserUnitsRequest([]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var global = await cleared.Content.ReadFromJsonAsync<UserUnitScopeResponse>(RaqmiApiFactory.JsonOptions);
        Assert.True(global!.IsGlobal);
        Assert.Empty(global.Units);

        // Chaque remplacement est audite, avec l'avant et l'apres.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        // Le tri se fait en memoire : SQLite (base des tests) ne sait pas trier un DateTimeOffset.
        var targetKey = targetId.ToString();
        var entries = (await dbContext.AuditLogs
                .AsNoTracking()
                .Where(entry => entry.Action == "security.user.units_changed" && entry.EntityId == targetKey)
                .ToArrayAsync())
            .OrderBy(entry => entry.OccurredAt)
            .ToArray();

        Assert.Equal(3, entries.Length);
        Assert.All(entries, entry => Assert.Equal("units.admin", entry.UserName));
        Assert.Contains("\"After\":[\"ADMA\",\"ADMB\"]", entries[0].DetailsJson);
        Assert.Contains("\"Before\":[\"ADMA\",\"ADMB\"]", entries[1].DetailsJson);
        Assert.Contains("\"IsGlobal\":true", entries[2].DetailsJson);
    }

    [Fact]
    public async Task An_unknown_unit_code_refuses_the_whole_replacement()
    {
        var targetId = await _factory.CreateUserAsync("units.unknown", "units.unknown@example.com", "Units Unknown", Password, RoleCatalog.Reader);
        await _factory.CreateUserAsync("units.admin2", "units.admin2@example.com", "Units Admin 2", Password, RoleCatalog.SystemAdministrator);
        using var admin = await _factory.CreateAuthenticatedClientAsync("units.admin2", Password);

        var response = await admin.PutAsJsonAsync(
            $"/api/v1/security/users/{targetId}/units",
            new SetUserUnitsRequest(["ADMA", "GHOST"]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiError>(RaqmiApiFactory.JsonOptions);
        Assert.Contains("GHOST", error!.Message);

        // Rien n'a ete enregistre, pas meme le code valide.
        var unchanged = await admin.GetFromJsonAsync<UserUnitScopeResponse>($"/api/v1/security/users/{targetId}/units", RaqmiApiFactory.JsonOptions);
        Assert.True(unchanged!.IsGlobal);

        // Un champ absent n'est pas une liste vide.
        var missingField = await admin.PutAsync(
            $"/api/v1/security/users/{targetId}/units",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, missingField.StatusCode);

        var unknownUser = await admin.GetAsync($"/api/v1/security/users/{Guid.NewGuid()}/units");
        Assert.Equal(HttpStatusCode.NotFound, unknownUser.StatusCode);
    }

    private async Task<HttpClient> CreateClientWithPermissionsAsync(string userName, params string[] permissionKeys)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<Application.Security.IPasswordHasher>();

            var permissions = await dbContext.Permissions
                .Where(permission => permissionKeys.Contains(permission.Key))
                .ToArrayAsync();

            Assert.Equal(permissionKeys.Length, permissions.Length);

            var role = new Role($"test.units.{Guid.NewGuid():N}", "Units test role", "Role dedie aux tests de perimetre.");

            foreach (var permission in permissions)
            {
                role.GrantPermission(permission, DateTimeOffset.UtcNow);
            }

            dbContext.Roles.Add(role);

            var user = new User(userName, $"{userName}@example.com", userName, passwordHasher.Hash(Password), mustChangePassword: false);
            user.AssignRole(role, DateTimeOffset.UtcNow);
            dbContext.Users.Add(user);

            await dbContext.SaveChangesAsync();
        }

        return await _factory.CreateAuthenticatedClientAsync(userName, Password);
    }

    private sealed record ApiError(string Message);

    private sealed record MeResponse(
        string Id,
        string UserName,
        string? Email,
        string[] Roles,
        string[] Permissions,
        UnitScopeResponse UnitScope);
}
