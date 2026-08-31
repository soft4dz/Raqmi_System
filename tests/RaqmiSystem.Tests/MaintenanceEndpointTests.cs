using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Maintenance;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Couverture HTTP du module Sauvegarde : authentification exigee, lecture reservee a
/// maintenance.read, declenchement reserve a maintenance.backup.
///
/// Les cles maintenance.read et maintenance.backup viennent de PermissionCatalog : le seeder
/// les seme au demarrage de la fabrique et Program.cs enregistre une policy par cle du
/// catalogue.
///
/// Le declenchement REUSSI n'est volontairement pas exerce ici : il dependrait de
/// l'environnement de la machine (RAQMI_BACKUP_DIR / RAQMI_PG_BIN reels) et pourrait
/// lancer un vrai pg_dump sur le poste d'un developpeur. Le comportement du service -
/// gardes de configuration comprises - est couvert par MaintenanceBackupServiceTests.
/// </summary>
public sealed class MaintenanceEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private readonly RaqmiApiFactory _factory;

    public MaintenanceEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Backups_endpoints_require_authentication()
    {
        using var client = _factory.CreateClient();

        var list = await client.GetAsync("/api/v1/maintenance/backups");
        var status = await client.GetAsync("/api/v1/maintenance/backups/status");
        var trigger = await client.PostAsync("/api/v1/maintenance/backups/trigger", null);

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, trigger.StatusCode);
    }

    [Fact]
    public async Task Reading_backups_requires_maintenance_read()
    {
        await CreateUserWithPermissionsAsync(
            "maintenance.norights",
            "maintenance.norights@example.com");

        using var client = await _factory.CreateAuthenticatedClientAsync("maintenance.norights", Password);

        var list = await client.GetAsync("/api/v1/maintenance/backups");
        var status = await client.GetAsync("/api/v1/maintenance/backups/status");

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, status.StatusCode);
    }

    [Fact]
    public async Task A_reader_can_consult_but_never_trigger()
    {
        await CreateUserWithPermissionsAsync(
            "maintenance.reader",
            "maintenance.reader@example.com",
            "maintenance.read");

        using var client = await _factory.CreateAuthenticatedClientAsync("maintenance.reader", Password);

        var listResponse = await client.GetAsync("/api/v1/maintenance/backups");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        // Quelle que soit la machine qui execute les tests (RAQMI_BACKUP_DIR defini ou
        // non), la reponse est un 200 exploitable - jamais un 500.
        var list = await listResponse.Content.ReadFromJsonAsync<BackupListResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(list);
        Assert.NotNull(list!.Backups);

        var statusResponse = await client.GetAsync("/api/v1/maintenance/backups/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var status = await statusResponse.Content.ReadFromJsonAsync<BackupStatusResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(status);
        Assert.True(status!.OverdueThresholdHours > 0);

        // Declencher une sauvegarde est un acte d'administration : maintenance.read ne suffit pas.
        var trigger = await client.PostAsync("/api/v1/maintenance/backups/trigger", null);
        Assert.Equal(HttpStatusCode.Forbidden, trigger.StatusCode);
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
            $"test.maintenance.{Guid.NewGuid():N}",
            "Maintenance test role",
            "Role dedicated to maintenance endpoint tests.");

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
