using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP coverage of the user administration module (routing, JSON binding, JwtBearer
/// authentication and the per-permission authorization policies of Program.cs all run for real).
///
/// Two of the three anti-lockout guards are proved here - they are self-checks, so they hold
/// whatever else the shared database contains. The third one (the last active administrator) is
/// about a global invariant and therefore needs a database of its very own: see
/// <see cref="LastAdministratorGuardTests"/>.
///
/// Every test creates its own uniquely-named accounts, since the whole class shares one in-memory
/// database.
/// </summary>
public sealed class UserAdministrationEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private readonly RaqmiApiFactory _factory;

    public UserAdministrationEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Reading_users_needs_users_read_and_changing_them_needs_users_write()
    {
        await CreateUserWithPermissionsAsync(
            "admin.readonly",
            "admin.readonly@example.com",
            PermissionCatalog.UsersRead);

        using var readerClient = await _factory.CreateAuthenticatedClientAsync("admin.readonly", Password);

        var listed = await readerClient.GetAsync("/api/v1/security/users");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);

        var roles = await readerClient.GetAsync("/api/v1/security/roles");
        Assert.Equal(HttpStatusCode.OK, roles.StatusCode);

        var forbidden = await readerClient.PostAsJsonAsync(
            "/api/v1/security/users",
            new CreateUserRequest("admin.readonly.attempt", "attempt@example.com", "Attempt"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Roles_are_listed_with_the_labels_the_screen_needs()
    {
        using var client = await CreateAdministratorClientAsync("admin.roles");

        var roles = await client.GetFromJsonAsync<RoleSummary[]>(
            "/api/v1/security/roles",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(roles);

        var cashier = Assert.Single(roles!, role => role.Name == RoleCatalog.Cashier);
        Assert.False(string.IsNullOrWhiteSpace(cashier.DisplayName));
        Assert.False(string.IsNullOrWhiteSpace(cashier.Description));
        Assert.True(cashier.IsSystem);
    }

    [Fact]
    public async Task Creating_a_user_returns_a_one_time_temporary_password_that_really_signs_in()
    {
        using var client = await CreateAdministratorClientAsync("admin.creator");

        var response = await client.PostAsJsonAsync(
            "/api/v1/security/users",
            new CreateUserRequest(
                "created.cashier",
                "created.cashier@example.com",
                "Created Cashier",
                [RoleCatalog.Cashier]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateUserResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(created);
        Assert.False(string.IsNullOrWhiteSpace(created!.TemporaryPassword));
        Assert.Equal("created.cashier", created.User.UserName);
        Assert.True(created.User.IsActive);
        Assert.True(created.User.MustChangePassword);
        Assert.Equal(new[] { RoleCatalog.Cashier }, created.User.Roles);
        Assert.Contains(PermissionCatalog.TreasuryWrite, created.User.Permissions);

        // The temporary password is not decoration: it is the account's actual password.
        using var loginClient = _factory.CreateClient();

        var login = await loginClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("created.cashier", created.TemporaryPassword),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var session = await login.Content.ReadFromJsonAsync<LoginResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(session);
        Assert.True(session!.User.MustChangePassword);

        // It is handed over exactly once, through the creation response - and nowhere else: the
        // audit trail records that the account was created, never the secret itself.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        var creationEntries = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(entry => entry.Action == "security.user.created")
            .Select(entry => entry.DetailsJson)
            .ToArrayAsync();

        Assert.Contains(creationEntries, details => details is not null && details.Contains("created.cashier"));
        Assert.DoesNotContain(
            creationEntries,
            details => details is not null && details.Contains(created.TemporaryPassword));
    }

    [Fact]
    public async Task Creating_a_user_refuses_a_taken_identifier_and_an_unknown_role()
    {
        using var client = await CreateAdministratorClientAsync("admin.conflicts");

        var first = await client.PostAsJsonAsync(
            "/api/v1/security/users",
            new CreateUserRequest("taken.identity", "taken.identity@example.com", "Taken Identity"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicateUserName = await client.PostAsJsonAsync(
            "/api/v1/security/users",
            new CreateUserRequest("TAKEN.IDENTITY", "another.address@example.com", "Another"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, duplicateUserName.StatusCode);

        var duplicateEmail = await client.PostAsJsonAsync(
            "/api/v1/security/users",
            new CreateUserRequest("another.identity", "Taken.Identity@Example.com", "Another"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, duplicateEmail.StatusCode);

        var unknownRole = await client.PostAsJsonAsync(
            "/api/v1/security/users",
            new CreateUserRequest(
                "third.identity",
                "third.identity@example.com",
                "Third",
                ["direction", "grand.chambellan"]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, unknownRole.StatusCode);
        Assert.Contains("grand.chambellan", await ReadErrorAsync(unknownRole));

        // A mistyped role is rejected as a whole: the account is not created with the roles that
        // happened to be spelled correctly.
        var listed = await client.GetFromJsonAsync<UserAccountResponse[]>(
            "/api/v1/security/users?search=third.identity",
            RaqmiApiFactory.JsonOptions);

        Assert.Empty(listed!);
    }

    [Fact]
    public async Task Changing_the_roles_of_a_user_changes_the_permissions_carried_by_the_next_token()
    {
        using var client = await CreateAdministratorClientAsync("admin.rolechanger");

        var created = await CreateUserThroughTheApiAsync(
            client,
            "role.switch",
            "role.switch@example.com",
            "Role Switch",
            [RoleCatalog.Reader]);

        using var beforeClient = await _factory.CreateAuthenticatedClientAsync(
            "role.switch",
            created.TemporaryPassword);

        var before = await beforeClient.GetFromJsonAsync<MeResponse>("/api/v1/me", RaqmiApiFactory.JsonOptions);

        Assert.Contains(RoleCatalog.Reader, before!.Roles);
        Assert.Contains(PermissionCatalog.RevenueRead, before.Permissions);
        Assert.DoesNotContain(PermissionCatalog.TreasuryWrite, before.Permissions);

        var changed = await client.PutAsJsonAsync(
            $"/api/v1/security/users/{created.User.Id}/roles",
            new SetUserRolesRequest([RoleCatalog.Cashier]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        var detail = await changed.Content.ReadFromJsonAsync<UserAccountDetailResponse>(RaqmiApiFactory.JsonOptions);
        Assert.Equal(new[] { RoleCatalog.Cashier }, detail!.Roles);
        Assert.Contains(PermissionCatalog.TreasuryWrite, detail.Permissions);

        // What matters is not the response body but the token minted afterwards: the permission
        // claims the authorization policies are evaluated against must have followed the change.
        using var afterClient = await _factory.CreateAuthenticatedClientAsync(
            "role.switch",
            created.TemporaryPassword);

        var after = await afterClient.GetFromJsonAsync<MeResponse>("/api/v1/me", RaqmiApiFactory.JsonOptions);

        Assert.Equal(new[] { RoleCatalog.Cashier }, after!.Roles);
        Assert.Contains(PermissionCatalog.TreasuryWrite, after.Permissions);
        Assert.DoesNotContain(PermissionCatalog.UnitsRead, after.Permissions);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        var roleChanges = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(entry => entry.Action == "security.user.roles_changed")
            .Select(entry => entry.DetailsJson)
            .ToArrayAsync();

        Assert.Contains(
            roleChanges,
            details => details is not null
                && details.Contains("role.switch")
                && details.Contains(RoleCatalog.Reader)
                && details.Contains(RoleCatalog.Cashier));
    }

    [Fact]
    public async Task Unlocking_an_account_locked_out_by_failed_logins_lets_it_sign_in_again()
    {
        using var client = await CreateAdministratorClientAsync("admin.unlocker");

        var created = await CreateUserThroughTheApiAsync(
            client,
            "locked.out",
            "locked.out@example.com",
            "Locked Out",
            [RoleCatalog.Reader]);

        using var victimClient = _factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await victimClient.PostAsJsonAsync(
                "/api/v1/auth/login",
                new LoginRequest("locked.out", "not-the-temporary-password"),
                RaqmiApiFactory.JsonOptions);

            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        var rejectedWithTheRightPassword = await victimClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("locked.out", created.TemporaryPassword),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, rejectedWithTheRightPassword.StatusCode);

        var lockedOut = await client.GetFromJsonAsync<UserAccountDetailResponse>(
            $"/api/v1/security/users/{created.User.Id}",
            RaqmiApiFactory.JsonOptions);

        Assert.True(lockedOut!.IsLockedOut);
        Assert.NotNull(lockedOut.LockedOutUntil);

        var unlocked = await client.PostAsync($"/api/v1/security/users/{created.User.Id}/unlock", content: null);
        Assert.Equal(HttpStatusCode.OK, unlocked.StatusCode);

        var afterUnlock = await unlocked.Content.ReadFromJsonAsync<UserAccountDetailResponse>(
            RaqmiApiFactory.JsonOptions);

        Assert.False(afterUnlock!.IsLockedOut);
        Assert.Null(afterUnlock.LockedOutUntil);

        // The administrator did not have to wait out the 15-minute policy window.
        var accepted = await victimClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("locked.out", created.TemporaryPassword),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task Updating_a_user_rewrites_the_profile_but_never_the_sign_in_identifier()
    {
        using var client = await CreateAdministratorClientAsync("admin.updater");

        var created = await CreateUserThroughTheApiAsync(
            client,
            "profile.owner",
            "profile.owner@example.com",
            "Profile Owner",
            [RoleCatalog.Reader]);

        var updated = await client.PutAsJsonAsync(
            $"/api/v1/security/users/{created.User.Id}",
            new UpdateUserRequest("profile.renamed@example.com", "Profile Renamed"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var detail = await updated.Content.ReadFromJsonAsync<UserAccountDetailResponse>(RaqmiApiFactory.JsonOptions);

        Assert.Equal("profile.renamed@example.com", detail!.Email);
        Assert.Equal("Profile Renamed", detail.DisplayName);
        Assert.Equal("profile.owner", detail.UserName);
        Assert.Equal("admin.updater", detail.UpdatedBy);

        // The new email is a real sign-in identifier, and the old one no longer is.
        using var loginClient = _factory.CreateClient();

        var withNewEmail = await loginClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("profile.renamed@example.com", created.TemporaryPassword),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, withNewEmail.StatusCode);

        var missing = await client.PutAsJsonAsync(
            $"/api/v1/security/users/{Guid.NewGuid()}",
            new UpdateUserRequest("nobody@example.com", "Nobody"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Listing_users_hides_deactivated_accounts_unless_asked_and_reports_their_roles()
    {
        using var client = await CreateAdministratorClientAsync("admin.lister");

        var kept = await CreateUserThroughTheApiAsync(
            client,
            "listing.kept",
            "listing.kept@example.com",
            "Listing Kept",
            [RoleCatalog.Direction]);

        var retired = await CreateUserThroughTheApiAsync(
            client,
            "listing.retired",
            "listing.retired@example.com",
            "Listing Retired",
            [RoleCatalog.Reader]);

        var deactivated = await client.PostAsync(
            $"/api/v1/security/users/{retired.User.Id}/deactivate",
            content: null);

        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);

        var active = await client.GetFromJsonAsync<UserAccountResponse[]>(
            "/api/v1/security/users?search=listing.",
            RaqmiApiFactory.JsonOptions);

        Assert.Contains(active!, user => user.UserName == "listing.kept");
        Assert.DoesNotContain(active!, user => user.UserName == "listing.retired");

        var all = await client.GetFromJsonAsync<UserAccountResponse[]>(
            "/api/v1/security/users?search=listing.&includeInactive=true",
            RaqmiApiFactory.JsonOptions);

        var retiredRow = Assert.Single(all!, user => user.UserName == "listing.retired");
        Assert.False(retiredRow.IsActive);

        var keptRow = Assert.Single(all!, user => user.UserName == "listing.kept");
        Assert.Equal(new[] { RoleCatalog.Direction }, keptRow.Roles);
        Assert.False(keptRow.IsLockedOut);
        Assert.Equal(kept.User.Id, keptRow.Id);

        // Reactivation goes through the same endpoint pair.
        var reactivated = await client.PostAsync(
            $"/api/v1/security/users/{retired.User.Id}/activate",
            content: null);

        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);

        var missing = await client.GetAsync($"/api/v1/security/users/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Replacing_the_roles_demands_the_field_and_only_an_explicit_empty_array_strips_them()
    {
        using var client = await CreateAdministratorClientAsync("admin.stripper");

        var created = await CreateUserThroughTheApiAsync(
            client,
            "roles.stripped",
            "roles.stripped@example.com",
            "Roles Stripped",
            [RoleCatalog.Reader]);

        // A body that simply forgot the field must not be read as "remove every role".
        var withoutTheField = await client.PutAsync(
            $"/api/v1/security/users/{created.User.Id}/roles",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, withoutTheField.StatusCode);

        var untouched = await client.GetFromJsonAsync<UserAccountDetailResponse>(
            $"/api/v1/security/users/{created.User.Id}",
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(new[] { RoleCatalog.Reader }, untouched!.Roles);

        var stripped = await client.PutAsJsonAsync(
            $"/api/v1/security/users/{created.User.Id}/roles",
            new SetUserRolesRequest([]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, stripped.StatusCode);

        var detail = await stripped.Content.ReadFromJsonAsync<UserAccountDetailResponse>(
            RaqmiApiFactory.JsonOptions);

        Assert.Empty(detail!.Roles);
        Assert.Empty(detail.Permissions);

        // The account still signs in - it simply cannot do anything any more.
        using var strippedClient = await _factory.CreateAuthenticatedClientAsync(
            "roles.stripped",
            created.TemporaryPassword);

        var me = await strippedClient.GetFromJsonAsync<MeResponse>("/api/v1/me", RaqmiApiFactory.JsonOptions);

        Assert.Empty(me!.Permissions);
        Assert.Equal(HttpStatusCode.Forbidden, (await strippedClient.GetAsync("/api/v1/security/users")).StatusCode);
    }

    /// <summary>
    /// Guard (a): an administrator who deactivates their own account walks out and locks the door
    /// from the outside. The rule lives in the service, so it holds for any HTTP caller - not only
    /// for a screen that greys the button out.
    /// </summary>
    [Fact]
    public async Task An_administrator_cannot_deactivate_their_own_account()
    {
        var administratorId = await _factory.CreateUserAsync(
            "guard.self.deactivation",
            "guard.self.deactivation@example.com",
            "Guard Self Deactivation",
            Password,
            RoleCatalog.SystemAdministrator);

        using var client = await _factory.CreateAuthenticatedClientAsync("guard.self.deactivation", Password);

        var refused = await client.PostAsync(
            $"/api/v1/security/users/{administratorId}/deactivate",
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("your own account", await ReadErrorAsync(refused));

        var stillActive = await client.GetFromJsonAsync<UserAccountDetailResponse>(
            $"/api/v1/security/users/{administratorId}",
            RaqmiApiFactory.JsonOptions);

        Assert.True(stillActive!.IsActive);

        // Deactivating SOMEONE ELSE stays perfectly legal: the guard is about self-lockout, it is
        // not a blanket ban on the operation.
        var victim = await CreateUserThroughTheApiAsync(
            client,
            "guard.self.victim",
            "guard.self.victim@example.com",
            "Guard Self Victim",
            [RoleCatalog.Reader]);

        var accepted = await client.PostAsync(
            $"/api/v1/security/users/{victim.User.Id}/deactivate",
            content: null);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    /// <summary>
    /// Guard (b): the symmetric hole. Keeping the account active but dropping the role that
    /// carries users.write locks the administrator out of user administration just as effectively.
    /// </summary>
    [Fact]
    public async Task An_administrator_cannot_strip_themselves_of_the_role_carrying_users_write()
    {
        var administratorId = await _factory.CreateUserAsync(
            "guard.self.demotion",
            "guard.self.demotion@example.com",
            "Guard Self Demotion",
            Password,
            RoleCatalog.SystemAdministrator);

        using var client = await _factory.CreateAuthenticatedClientAsync("guard.self.demotion", Password);

        var refused = await client.PutAsJsonAsync(
            $"/api/v1/security/users/{administratorId}/roles",
            new SetUserRolesRequest([RoleCatalog.Reader]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains(PermissionCatalog.UsersWrite, await ReadErrorAsync(refused));

        var unchanged = await client.GetFromJsonAsync<UserAccountDetailResponse>(
            $"/api/v1/security/users/{administratorId}",
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(new[] { RoleCatalog.SystemAdministrator }, unchanged!.Roles);
        Assert.Contains(PermissionCatalog.UsersWrite, unchanged.Permissions);

        // Adding roles to yourself, or rearranging them while keeping users.write, is untouched by
        // the guard: only LOSING the permission is refused.
        var accepted = await client.PutAsJsonAsync(
            $"/api/v1/security/users/{administratorId}/roles",
            new SetUserRolesRequest([RoleCatalog.SystemAdministrator, RoleCatalog.Cashier]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    private async Task<CreateUserResponse> CreateUserThroughTheApiAsync(
        HttpClient client,
        string userName,
        string email,
        string displayName,
        IReadOnlyCollection<string> roles)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/security/users",
            new CreateUserRequest(userName, email, displayName, roles),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateUserResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(created);

        return created!;
    }

    private async Task<HttpClient> CreateAdministratorClientAsync(string userName)
    {
        await _factory.CreateUserAsync(
            userName,
            $"{userName}@example.com",
            userName,
            Password,
            RoleCatalog.SystemAdministrator);

        return await _factory.CreateAuthenticatedClientAsync(userName, Password);
    }

    /// <summary>
    /// Creates a user attached to a fresh single-purpose role carrying exactly the given permission
    /// keys, mirroring SettingsEndpointTests: the per-permission authorization policies registered
    /// in Program.cs are then enforced for real against that user's token.
    /// </summary>
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
            $"test.security.{Guid.NewGuid():N}",
            "Security test role",
            "Role dedicated to user administration endpoint tests.");

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

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var error = await response.Content.ReadFromJsonAsync<ApiError>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(error);

        return error!.Message;
    }

    private sealed record ApiError(string Message);

    private sealed record MeResponse(
        string Id,
        string UserName,
        string? Email,
        string[] Roles,
        string[] Permissions);
}
