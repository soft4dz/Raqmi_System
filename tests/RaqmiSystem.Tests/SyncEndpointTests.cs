using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Sync;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Couverture HTTP du module 29, centree sur son ASYMETRIE DE PERMISSION, qui est la decision de
/// conception la plus discutable du module et doit donc etre verrouillee par un test :
///
///   * les deux POST (battement, signalement) n'exigent qu'une authentification NUE, parce que les
///     postes a surveiller sont des machines de reception et de caisse tenues par des profils sans
///     droit d'administration - exiger sync.read pour se declarer produirait un registre vide de
///     precisement ce qu'il doit montrer ;
///   * les deux GET exigent sync.read, car lire le parc et le journal des erreurs est un acte de
///     supervision.
///
/// Si quelqu'un "harmonise" un jour ces quatre routes sur la meme permission, ce fichier tombe.
/// </summary>
public sealed class SyncEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private const string SyncRead = "sync.read";

    private readonly RaqmiApiFactory _factory;

    public SyncEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Un_profil_sans_sync_read_peut_battre_mais_ne_peut_pas_lire_le_registre()
    {
        await CreateSyncUserAsync("sync.reception", "sync.reception@example.com", "Reception");

        var client = await _factory.CreateAuthenticatedClientAsync("sync.reception", Password);
        var stationId = Guid.NewGuid();

        // Le poste se declare : autorise, sans aucune permission particuliere.
        var heartbeat = await client.PostAsJsonAsync(
            "/api/v1/sync/stations/heartbeat",
            new WorkstationHeartbeatRequest(stationId, "POSTE-RECEPTION", "1.4.0", null));

        Assert.Equal(HttpStatusCode.OK, heartbeat.StatusCode);

        // Il signale ses erreurs : egalement autorise.
        var failures = await client.PostAsJsonAsync(
            "/api/v1/sync/stations/failures",
            new ReportWorkstationFailuresRequest(
                stationId,
                [new WorkstationFailureItem(
                    Guid.NewGuid(),
                    "POST",
                    "/api/v1/revenue/daily",
                    500,
                    "HttpError",
                    "Erreur serveur",
                    DateTimeOffset.UtcNow)]));

        Assert.Equal(HttpStatusCode.OK, failures.StatusCode);

        // Mais il ne voit NI le parc NI le journal : ce sont des actes de supervision.
        var registry = await client.GetAsync("/api/v1/sync/stations");
        Assert.Equal(HttpStatusCode.Forbidden, registry.StatusCode);

        var journal = await client.GetAsync("/api/v1/sync/failures");
        Assert.Equal(HttpStatusCode.Forbidden, journal.StatusCode);
    }

    [Fact]
    public async Task Un_profil_porteur_de_sync_read_lit_le_registre_et_le_journal()
    {
        await CreateSyncUserAsync("sync.superviseur", "sync.superviseur@example.com", "Superviseur", SyncRead);

        var client = await _factory.CreateAuthenticatedClientAsync("sync.superviseur", Password);
        var stationId = Guid.NewGuid();

        await client.PostAsJsonAsync(
            "/api/v1/sync/stations/heartbeat",
            new WorkstationHeartbeatRequest(stationId, "POSTE-DIRECTION", "1.4.0", null));

        var response = await client.GetAsync("/api/v1/sync/stations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var registry = await response.Content.ReadFromJsonAsync<WorkstationRegistryResponse>();

        Assert.NotNull(registry);
        Assert.Contains(registry!.Workstations, station => station.Label == "POSTE-DIRECTION");

        // Les seuils sont renvoyes par le serveur : l'ecran ne les recopie pas.
        Assert.Equal(15, registry.StaleAfterMinutes);
        Assert.Equal(60, registry.OfflineAfterMinutes);

        var journal = await client.GetAsync("/api/v1/sync/failures");
        Assert.Equal(HttpStatusCode.OK, journal.StatusCode);
    }

    [Fact]
    public async Task Un_appel_non_authentifie_est_refuse_sur_les_quatre_routes()
    {
        // Les POST sont ouverts a tout utilisateur AUTHENTIFIE, ce qui n'est pas la meme chose
        // qu'anonyme : aucune des quatre routes n'est publique.
        var client = _factory.CreateClient();

        var heartbeat = await client.PostAsJsonAsync(
            "/api/v1/sync/stations/heartbeat",
            new WorkstationHeartbeatRequest(Guid.NewGuid(), "POSTE-PIRATE", "1.4.0", null));

        Assert.Equal(HttpStatusCode.Unauthorized, heartbeat.StatusCode);

        var failures = await client.PostAsJsonAsync(
            "/api/v1/sync/stations/failures",
            new ReportWorkstationFailuresRequest(Guid.NewGuid(), []));

        Assert.Equal(HttpStatusCode.Unauthorized, failures.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/sync/stations")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/sync/failures")).StatusCode);
    }

    private async Task CreateSyncUserAsync(
        string userName,
        string email,
        string displayName,
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
            "Cles de permission absentes du PermissionCatalog seme : " +
            string.Join(", ", permissionKeys.Except(permissions.Select(permission => permission.Key))));

        var role = new Role(
            $"test.sync.{Guid.NewGuid():N}",
            "Sync test role",
            "Role dedie aux tests d'endpoints du module 29.");

        foreach (var permission in permissions)
        {
            role.GrantPermission(permission, DateTimeOffset.UtcNow);
        }

        dbContext.Roles.Add(role);

        var user = new User(userName, email, displayName, passwordHasher.Hash(Password), mustChangePassword: false);
        user.AssignRole(role, DateTimeOffset.UtcNow);
        dbContext.Users.Add(user);

        await dbContext.SaveChangesAsync();
    }
}
