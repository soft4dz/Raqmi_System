using System.Net;
using System.Net.Http.Json;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP integration coverage for the login and "current user" endpoints: real routing, real
/// JSON model binding, and the real JwtBearer authentication middleware end to end (as opposed to
/// AuthenticationServiceTests.cs, which exercises AuthenticationService directly without going
/// through HTTP).
///
/// All tests in this class share one RaqmiApiFactory (and therefore one in-memory database), so
/// each test creates its own uniquely-named user to avoid interfering with the others - the
/// lockout test in particular must not share an account with any other test.
/// </summary>
public sealed class AuthenticationEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private readonly RaqmiApiFactory _factory;

    public AuthenticationEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_200_with_tokens_and_role_permissions()
    {
        await _factory.CreateUserAsync(
            "cashier.valid",
            "cashier.valid@example.com",
            "Cashier Valid",
            Password,
            RoleCatalog.Cashier);

        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("cashier.valid", Password),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.Equal("cashier.valid", body.User.UserName);
        Assert.Contains(RoleCatalog.Cashier, body.User.Roles);

        var expectedPermissions = new[]
        {
            PermissionCatalog.RevenueRead,
            PermissionCatalog.RevenueWrite,
            PermissionCatalog.TreasuryRead,
            PermissionCatalog.TreasuryWrite,
            PermissionCatalog.SettingsRead
        };

        Assert.Equal(expectedPermissions.Order(), body.User.Permissions.Order());
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        await _factory.CreateUserAsync(
            "wrongpass.user",
            "wrongpass.user@example.com",
            "Wrong Pass User",
            Password,
            RoleCatalog.Reader);

        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("wrongpass.user", "definitely-not-the-password"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_locks_out_after_five_failed_attempts_and_then_rejects_the_correct_password_too()
    {
        await _factory.CreateUserAsync(
            "lockout.user",
            "lockout.user@example.com",
            "Lockout User",
            Password,
            RoleCatalog.Reader);

        using var client = _factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failedResponse = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new LoginRequest("lockout.user", "still-not-the-password"),
                RaqmiApiFactory.JsonOptions);

            Assert.Equal(HttpStatusCode.Unauthorized, failedResponse.StatusCode);
        }

        // The account is now locked out - even the correct password must be rejected. This
        // proves the lockout is enforced by the real HTTP pipeline, not only inside the service.
        var responseWithCorrectPassword = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("lockout.user", Password),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, responseWithCorrectPassword.StatusCode);
    }

    [Fact]
    public async Task Me_without_a_token_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_with_a_valid_token_returns_200_with_the_expected_claims()
    {
        await _factory.CreateUserAsync(
            "me.user",
            "me.user@example.com",
            "Me User",
            Password,
            RoleCatalog.Direction);

        using var client = await _factory.CreateAuthenticatedClientAsync("me.user", Password);

        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MeResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(body);
        Assert.Equal("me.user", body!.UserName);
        Assert.Equal("me.user@example.com", body.Email);
        Assert.Contains(RoleCatalog.Direction, body.Roles);

        var expectedPermissions = new[]
        {
            PermissionCatalog.UnitsRead,
            PermissionCatalog.RevenueRead,
            PermissionCatalog.DashboardRead,
            PermissionCatalog.TreasuryRead,
            PermissionCatalog.AuditRead,
            PermissionCatalog.ReportsExport,
            PermissionCatalog.ClosingRead,
            PermissionCatalog.TreasuryApprove,
            PermissionCatalog.CustomersRead,
            PermissionCatalog.InvoicesRead,
            PermissionCatalog.SettingsRead
        };

        Assert.Equal(expectedPermissions.Order(), body.Permissions.Order());
    }

    private sealed record MeResponse(
        string Id,
        string UserName,
        string? Email,
        string[] Roles,
        string[] Permissions);
}
