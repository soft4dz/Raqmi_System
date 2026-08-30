using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP coverage for POST /api/v1/account/change-password.
///
/// The module exists to close a loop the user administration module could only ever open: account
/// creation and the administrative reset both hand out a temporary password and raise
/// <c>MustChangePassword</c>, and until this endpoint there was no way to lower it. So these tests
/// assert the loop end to end - the flag comes down, the OLD password stops working, the NEW one
/// starts working - rather than just the status code of the call itself.
///
/// Everything goes through real routing, JSON binding and JwtBearer authentication (see
/// <see cref="RaqmiApiFactory"/>); only the database is swapped for SQLite in-memory. The fixture is
/// shared by the whole class, so each test uses its own user names to stay independent.
/// </summary>
public sealed class AccountPasswordEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string OriginalPassword = "Original-Passphrase-42!";
    private const string ReplacementPassword = "Replacement-Passphrase-77!";

    private readonly RaqmiApiFactory _factory;

    public AccountPasswordEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Change_password_lowers_must_change_password_and_swaps_which_password_signs_in()
    {
        // Created the way an administrator creates one: on a temporary password, flagged. This is
        // the state the whole module exists for - before it, the flag could never come down.
        var userId = await CreateFlaggedUserAsync("account.rotate", "account.rotate@example.com");

        using var client = await _factory.CreateAuthenticatedClientAsync("account.rotate", OriginalPassword);

        var response = await client.PostAsJsonAsync(
            "/api/v1/account/change-password",
            new ChangePasswordRequest(OriginalPassword, ReplacementPassword),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ChangePasswordResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(body);
        Assert.Equal(userId, body!.UserId);
        Assert.False(body.MustChangePassword);

        // The new password signs in, and the login response confirms the flag is down for good and
        // not merely absent from the change response.
        var withNewPassword = await SignInAsync("account.rotate", ReplacementPassword);

        Assert.Equal(HttpStatusCode.OK, withNewPassword.StatusCode);

        var session = await withNewPassword.Content.ReadFromJsonAsync<LoginResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(session);
        Assert.False(session!.User.MustChangePassword);

        // And the temporary password the administrator knows is now worthless, which is the point.
        var withOldPassword = await SignInAsync("account.rotate", OriginalPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, withOldPassword.StatusCode);
    }

    [Fact]
    public async Task Change_password_with_a_wrong_current_password_is_refused_and_changes_nothing()
    {
        await CreateFlaggedUserAsync("account.wrongcurrent", "account.wrongcurrent@example.com");

        using var client = await _factory.CreateAuthenticatedClientAsync("account.wrongcurrent", OriginalPassword);

        var response = await client.PostAsJsonAsync(
            "/api/v1/account/change-password",
            new ChangePasswordRequest("Not-The-Current-Password-1!", ReplacementPassword),
            RaqmiApiFactory.JsonOptions);

        // Holding a valid session is not enough: a borrowed or stolen token must not be able to
        // lock the legitimate owner out of their own account.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync("account.wrongcurrent", ReplacementPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync("account.wrongcurrent", OriginalPassword)).StatusCode);
    }

    [Fact]
    public async Task Change_password_with_a_new_password_shorter_than_the_policy_is_refused()
    {
        await CreateFlaggedUserAsync("account.tooshort", "account.tooshort@example.com");

        using var client = await _factory.CreateAuthenticatedClientAsync("account.tooshort", OriginalPassword);

        // One character below PasswordPolicy.MinimumLength - the same threshold SecuritySeeder has
        // always imposed on the initial administrator, so a self-chosen password can never be
        // weaker than a seeded one.
        var tooShort = new string('a', PasswordPolicy.MinimumLength - 1);

        var response = await client.PostAsJsonAsync(
            "/api/v1/account/change-password",
            new ChangePasswordRequest(OriginalPassword, tooShort),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync("account.tooshort", tooShort)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync("account.tooshort", OriginalPassword)).StatusCode);
    }

    [Fact]
    public async Task Change_password_to_the_very_same_password_is_refused()
    {
        await CreateFlaggedUserAsync("account.samepassword", "account.samepassword@example.com");

        using var client = await _factory.CreateAuthenticatedClientAsync("account.samepassword", OriginalPassword);

        var response = await client.PostAsJsonAsync(
            "/api/v1/account/change-password",
            new ChangePasswordRequest(OriginalPassword, OriginalPassword),
            RaqmiApiFactory.JsonOptions);

        // Otherwise the flag would come down while the password an administrator handed over stays
        // in force - the exact situation the module was written to end.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.True(await ReadMustChangePasswordAsync("account.samepassword"));
    }

    [Fact]
    public async Task Change_password_revokes_the_refresh_tokens_issued_before_it()
    {
        await CreateFlaggedUserAsync("account.sessions", "account.sessions@example.com");

        // Two separate sign-ins = two live sessions, as if the account were open on two machines
        // (or as if someone else were holding one of them).
        var firstSession = await SignInAndReadAsync("account.sessions", OriginalPassword);
        var secondSession = await SignInAndReadAsync("account.sessions", OriginalPassword);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstSession.AccessToken);

        var response = await client.PostAsJsonAsync(
            "/api/v1/account/change-password",
            new ChangePasswordRequest(OriginalPassword, ReplacementPassword),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ChangePasswordResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(body);
        Assert.Equal(2, body!.RevokedSessionCount);

        // Neither refresh token survives - not the other machine's, and not the caller's own. A
        // password change that left the intruder's session quietly renewing itself for the rest of
        // the refresh-token lifetime would be no remedy to a compromise at all.
        using var anonymous = _factory.CreateClient();

        foreach (var refreshToken in new[] { firstSession.RefreshToken, secondSession.RefreshToken })
        {
            var refreshResponse = await anonymous.PostAsJsonAsync(
                "/api/v1/auth/refresh",
                new RefreshTokenRequest(refreshToken),
                RaqmiApiFactory.JsonOptions);

            Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
        }
    }

    [Fact]
    public async Task Change_password_ignores_a_user_id_smuggled_into_the_body()
    {
        var victimId = await CreateFlaggedUserAsync("account.victim", "account.victim@example.com");
        var attackerId = await CreateFlaggedUserAsync("account.attacker", "account.attacker@example.com");

        using var client = await _factory.CreateAuthenticatedClientAsync("account.attacker", OriginalPassword);

        // ChangePasswordRequest carries no identifier at all, so this extra field is simply not
        // bound. The test sends it anyway: the guarantee under test is that the account acted upon
        // comes from the token, and it would be worth little if it only held for well-formed
        // bodies. A reader - the least privileged role there is - must not be able to take over an
        // administrator's account by naming them here.
        var response = await client.PostAsJsonAsync(
            "/api/v1/account/change-password",
            new
            {
                userId = victimId,
                currentPassword = OriginalPassword,
                newPassword = ReplacementPassword
            },
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ChangePasswordResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(body);
        Assert.Equal(attackerId, body!.UserId);
        Assert.NotEqual(victimId, body.UserId);

        // The named account is untouched: original password still valid, replacement rejected.
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync("account.victim", OriginalPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync("account.victim", ReplacementPassword)).StatusCode);
    }

    [Fact]
    public async Task Change_password_cannot_be_driven_with_another_accounts_current_password()
    {
        await CreateFlaggedUserAsync("account.target", "account.target@example.com");
        await CreateFlaggedUserAsync(
            "account.impostor",
            "account.impostor@example.com",
            password: "Impostor-Passphrase-13!");

        using var client = await _factory.CreateAuthenticatedClientAsync(
            "account.impostor",
            "Impostor-Passphrase-13!");

        // Knowing the target's password does not help either: the current password is verified
        // against the CALLER's hash, so this is just a wrong password for the impostor's own
        // account.
        var response = await client.PostAsJsonAsync(
            "/api/v1/account/change-password",
            new
            {
                userId = "account.target",
                currentPassword = OriginalPassword,
                newPassword = ReplacementPassword
            },
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await SignInAsync("account.target", OriginalPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync("account.impostor", "Impostor-Passphrase-13!")).StatusCode);
    }

    [Fact]
    public async Task Change_password_without_a_token_returns_401()
    {
        await CreateFlaggedUserAsync("account.anonymous", "account.anonymous@example.com");

        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/account/change-password",
            new ChangePasswordRequest(OriginalPassword, ReplacementPassword),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await SignInAsync("account.anonymous", OriginalPassword)).StatusCode);
    }

    /// <summary>
    /// Creates a user in exactly the state <c>UserAdministrationService.CreateAsync</c> leaves one
    /// in - a known password plus <c>MustChangePassword = true</c> - which
    /// <see cref="RaqmiApiFactory.CreateUserAsync"/> cannot express (it always clears the flag).
    /// Written through the DbContext rather than the administration API so these tests do not
    /// depend on, or incidentally re-test, that module.
    /// </summary>
    private async Task<Guid> CreateFlaggedUserAsync(
        string userName,
        string email,
        string password = OriginalPassword,
        string roleName = RoleCatalog.Reader)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var role = await dbContext.Roles.SingleAsync(candidate => candidate.Name == roleName);

        var user = new User(userName, email, userName, passwordHasher.Hash(password), mustChangePassword: true);
        user.AssignRole(role, DateTimeOffset.UtcNow);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return user.Id;
    }

    private async Task<bool> ReadMustChangePasswordAsync(string userName)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        var user = await dbContext.Users
            .AsNoTracking()
            .SingleAsync(candidate => candidate.UserName == userName);

        return user.MustChangePassword;
    }

    private async Task<HttpResponseMessage> SignInAsync(string userNameOrEmail, string password)
    {
        using var client = _factory.CreateClient();

        return await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(userNameOrEmail, password),
            RaqmiApiFactory.JsonOptions);
    }

    private async Task<LoginResponse> SignInAndReadAsync(string userNameOrEmail, string password)
    {
        var response = await SignInAsync(userNameOrEmail, password);

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(body);

        return body!;
    }
}
