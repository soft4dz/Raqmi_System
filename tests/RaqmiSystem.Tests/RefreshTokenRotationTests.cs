using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Identity;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Security;

namespace RaqmiSystem.Tests;

/// <summary>
/// Covers AuthenticationService.RefreshAsync's single-use rotation guarantee end to end.
///
/// This exercises the real ExecuteUpdateAsync-based atomic revocation added to RefreshAsync, so
/// it needs a genuinely relational EF Core provider: the EF Core InMemory provider does not
/// translate ExecuteUpdate/ExecuteUpdateAsync at all and throws
/// "InvalidOperationException: The methods 'ExecuteUpdate' and 'ExecuteUpdateAsync' are not
/// supported by the current database provider" (verified empirically while writing this test).
/// A SQLite ":memory:" database is a real relational provider - it supports ExecuteUpdateAsync
/// like the production Npgsql provider does - while staying just as fast and self-contained as
/// the InMemory provider for a single test.
/// </summary>
public sealed class RefreshTokenRotationTests
{
    private const string Password = "Correct-Horse-Battery-42!";

    [Fact]
    public async Task RefreshAsync_rejects_reuse_of_a_rotated_refresh_token()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RaqmiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new RaqmiDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var passwordHasher = new Pbkdf2PasswordHasher();
        var jwtOptions = new JwtOptions
        {
            Issuer = "RaqmiSystem.Tests",
            Audience = "RaqmiSystem.Tests.Client",
            SigningKey = new string('k', 64),
            AccessTokenMinutes = 60
        };
        var tokenService = new JwtTokenService(Options.Create(jwtOptions));
        var auditLogWriter = new AuditLogWriter(dbContext);
        var authenticationService = new AuthenticationService(dbContext, passwordHasher, tokenService, auditLogWriter);

        var user = new User("jdoe", "jdoe@example.com", "John Doe", passwordHasher.Hash(Password), mustChangePassword: false);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var loginResponse = await authenticationService.SignInAsync(
            new LoginRequest("jdoe", Password),
            "127.0.0.1",
            CancellationToken.None);

        Assert.NotNull(loginResponse);
        var originalRefreshToken = loginResponse!.RefreshToken;

        var firstRefresh = await authenticationService.RefreshAsync(
            originalRefreshToken,
            "127.0.0.1",
            CancellationToken.None);

        Assert.NotNull(firstRefresh);
        Assert.NotEqual(originalRefreshToken, firstRefresh!.RefreshToken);

        // Replaying the same (now-rotated) raw token must be rejected, not honored again.
        var secondRefresh = await authenticationService.RefreshAsync(
            originalRefreshToken,
            "127.0.0.1",
            CancellationToken.None);

        Assert.Null(secondRefresh);

        var refreshFailedAuditEntries = await dbContext.AuditLogs
            .Where(auditLog => auditLog.Action == "auth.refresh.failed")
            .ToListAsync();

        Assert.Single(refreshFailedAuditEntries);
    }
}
