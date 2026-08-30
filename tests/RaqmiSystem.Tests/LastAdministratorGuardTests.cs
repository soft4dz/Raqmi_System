using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Guard (c) of the user administration module: the installation must always keep at least one
/// ACTIVE account holding users.write. Without it, an administration screen can walk the system
/// into a state no one can get it out of - nobody left able to create a user, re-activate an
/// account or hand out a role.
///
/// Unlike the two self-guards (see <see cref="UserAdministrationEndpointTests"/>), this one is
/// about a system-wide count, so it can only be proved against a database whose entire population
/// is known. Each test therefore builds its OWN <see cref="RaqmiApiFactory"/>, and with it its own
/// isolated SQLite database, instead of sharing a class fixture with the others.
///
/// The scenario is the realistic one rather than a contrived one: an access token is a permission
/// SNAPSHOT taken at sign-in and is not revoked when the account behind it is deactivated. So a
/// just-deactivated administrator keeps calling the API until the token expires, and is exactly
/// the caller able to close the door behind them.
/// </summary>
public sealed class LastAdministratorGuardTests
{
    private const string Password = "Correct-Horse-Battery-42!";

    [Fact]
    public async Task The_last_active_administrator_cannot_be_deactivated()
    {
        using var factory = new RaqmiApiFactory();
        using var scenario = await ArrangeSingleRemainingAdministratorAsync(factory);

        var refused = await scenario.DemotedAdministratorClient.PostAsync(
            $"/api/v1/security/users/{scenario.LastAdministratorId}/deactivate",
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains(PermissionCatalog.UsersWrite, await ReadErrorAsync(refused));

        var stillStanding = await scenario.LastAdministratorClient.GetFromJsonAsync<UserAccountDetailResponse>(
            $"/api/v1/security/users/{scenario.LastAdministratorId}",
            RaqmiApiFactory.JsonOptions);

        Assert.True(stillStanding!.IsActive);
        Assert.Contains(PermissionCatalog.UsersWrite, stillStanding.Permissions);
    }

    [Fact]
    public async Task The_last_active_administrator_cannot_be_stripped_of_the_role_carrying_users_write()
    {
        using var factory = new RaqmiApiFactory();
        using var scenario = await ArrangeSingleRemainingAdministratorAsync(factory);

        // Deactivating and demoting are two doors into the same dead end, so the invariant has to
        // hold on the role-assignment path as well.
        var refused = await scenario.DemotedAdministratorClient.PutAsJsonAsync(
            $"/api/v1/security/users/{scenario.LastAdministratorId}/roles",
            new SetUserRolesRequest([RoleCatalog.Reader]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains(PermissionCatalog.UsersWrite, await ReadErrorAsync(refused));

        var stillStanding = await scenario.LastAdministratorClient.GetFromJsonAsync<UserAccountDetailResponse>(
            $"/api/v1/security/users/{scenario.LastAdministratorId}",
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(new[] { RoleCatalog.SystemAdministrator }, stillStanding!.Roles);
        Assert.Contains(PermissionCatalog.UsersWrite, stillStanding.Permissions);
    }

    [Fact]
    public async Task An_administrator_can_be_deactivated_as_long_as_another_active_one_remains()
    {
        using var factory = new RaqmiApiFactory();
        await factory.InitializeAsync();

        var firstId = await CreateAdministratorAsync(factory, "admin.one");
        await CreateAdministratorAsync(factory, "admin.two");

        using var secondClient = await factory.CreateAuthenticatedClientAsync("admin.two", Password);

        // The positive control of the two tests above: the guard refuses the LAST administrator,
        // it does not make administrators undeactivatable.
        var accepted = await secondClient.PostAsync(
            $"/api/v1/security/users/{firstId}/deactivate",
            content: null);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        var deactivated = await accepted.Content.ReadFromJsonAsync<UserAccountDetailResponse>(
            RaqmiApiFactory.JsonOptions);

        Assert.False(deactivated!.IsActive);
    }

    /// <summary>
    /// Two administrators; one of them is deactivated by the other, leaving exactly one active
    /// holder of users.write - and a still-usable access token in the hands of the demoted one.
    /// </summary>
    private static async Task<Scenario> ArrangeSingleRemainingAdministratorAsync(RaqmiApiFactory factory)
    {
        await factory.InitializeAsync();

        var demotedId = await CreateAdministratorAsync(factory, "admin.demoted");
        var lastId = await CreateAdministratorAsync(factory, "admin.last");

        var demotedClient = await factory.CreateAuthenticatedClientAsync("admin.demoted", Password);
        var lastClient = await factory.CreateAuthenticatedClientAsync("admin.last", Password);

        var deactivated = await lastClient.PostAsync(
            $"/api/v1/security/users/{demotedId}/deactivate",
            content: null);

        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);

        await AssertExactlyOneActiveAdministratorAsync(factory);

        return new Scenario(demotedClient, lastClient, lastId);
    }

    /// <summary>
    /// States the precondition the two guard tests depend on, so a database that unexpectedly
    /// contains another administrator (a seeded RAQMI_INITIAL_ADMIN_* account, for instance) fails
    /// here with a clear reason instead of silently turning the guard assertions into no-ops.
    /// </summary>
    private static async Task AssertExactlyOneActiveAdministratorAsync(RaqmiApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        var activeAdministrators = await dbContext.Users
            .AsNoTracking()
            .CountAsync(user => user.IsActive
                && user.Roles.Any(userRole =>
                    userRole.Role.Permissions.Any(rolePermission =>
                        rolePermission.Permission.Key == PermissionCatalog.UsersWrite)));

        Assert.True(
            activeAdministrators == 1,
            "The guard is about the LAST active users.write holder, so the scenario must leave exactly " +
            $"one; the database holds {activeAdministrators}.");
    }

    private static async Task<Guid> CreateAdministratorAsync(RaqmiApiFactory factory, string userName)
    {
        return await factory.CreateUserAsync(
            userName,
            $"{userName}@example.com",
            userName,
            Password,
            RoleCatalog.SystemAdministrator);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var error = await response.Content.ReadFromJsonAsync<ApiError>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(error);

        return error!.Message;
    }

    private sealed record ApiError(string Message);

    private sealed record Scenario(
        HttpClient DemotedAdministratorClient,
        HttpClient LastAdministratorClient,
        Guid LastAdministratorId) : IDisposable
    {
        public void Dispose()
        {
            DemotedAdministratorClient.Dispose();
            LastAdministratorClient.Dispose();
        }
    }
}
