using System.Net;
using System.Net.Http.Json;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP integration coverage for POST /api/v1/auth/refresh, complementing
/// RefreshTokenRotationTests.cs (which drives AuthenticationService.RefreshAsync directly). This
/// class instead goes through real routing and JSON model binding, confirming the single-use
/// rotation guarantee is actually enforced at the HTTP boundary.
/// </summary>
public sealed class RefreshTokenEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private readonly RaqmiApiFactory _factory;

    public RefreshTokenEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Refresh_with_a_valid_refresh_token_returns_200_with_a_new_token_pair()
    {
        await _factory.CreateUserAsync(
            "refresh.user",
            "refresh.user@example.com",
            "Refresh User",
            Password,
            RoleCatalog.Reader);

        using var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("refresh.user", Password),
            RaqmiApiFactory.JsonOptions);

        loginResponse.EnsureSuccessStatusCode();
        var originalTokens = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(originalTokens);

        var refreshResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshTokenRequest(originalTokens!.RefreshToken),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var rotatedTokens = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(rotatedTokens);
        Assert.False(string.IsNullOrWhiteSpace(rotatedTokens!.AccessToken));

        // The refresh token is CSPRNG-random on every mint, so it is guaranteed to differ - this
        // is the actual rotation guarantee this test targets. The access token is intentionally
        // NOT asserted to differ: JwtTokenService encodes "nbf"/"exp" as whole-second Unix
        // timestamps, and login+refresh here run back to back, almost always within the same
        // UTC second - with every other claim (user id, roles, permissions) also identical, that
        // makes the two JWTs byte-for-byte identical far more often than not. That is a real
        // characteristic of the production token minting, not a test bug, so asserting access
        // token inequality here would make this test flaky rather than more meaningful.
        Assert.NotEqual(originalTokens.RefreshToken, rotatedTokens.RefreshToken);
    }

    [Fact]
    public async Task Refresh_with_an_already_rotated_refresh_token_returns_401()
    {
        await _factory.CreateUserAsync(
            "reuse.user",
            "reuse.user@example.com",
            "Reuse User",
            Password,
            RoleCatalog.Reader);

        using var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("reuse.user", Password),
            RaqmiApiFactory.JsonOptions);

        loginResponse.EnsureSuccessStatusCode();
        var originalTokens = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(originalTokens);

        var firstRefreshResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshTokenRequest(originalTokens!.RefreshToken),
            RaqmiApiFactory.JsonOptions);

        firstRefreshResponse.EnsureSuccessStatusCode();

        // Replaying the original (now-rotated) raw refresh token must be rejected.
        var replayResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshTokenRequest(originalTokens.RefreshToken),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);
    }
}
