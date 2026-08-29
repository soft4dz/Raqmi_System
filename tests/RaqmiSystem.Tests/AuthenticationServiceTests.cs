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

public sealed class AuthenticationServiceTests
{
    private const string Password = "Correct-Horse-Battery-42!";

    [Fact]
    public async Task SignInAsync_returns_null_and_does_not_extend_lockout_when_account_is_locked_out()
    {
        using var dbContext = CreateInMemoryDbContext();
        var passwordHasher = new Pbkdf2PasswordHasher();
        var authenticationService = CreateAuthenticationService(dbContext, passwordHasher);

        var user = await CreateAndSaveUserAsync(dbContext, passwordHasher);

        // Drive the account into lockout using the real domain rule (5 failures locks it out).
        var lockoutSetupTime = DateTimeOffset.UtcNow;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            user.RegisterFailedLogin(lockoutSetupTime);
        }

        await dbContext.SaveChangesAsync();

        var failedAttemptsBeforeRetry = user.FailedLoginAttempts;
        var lockedOutUntilBeforeRetry = user.LockedOutUntil;
        Assert.NotNull(lockedOutUntilBeforeRetry);
        Assert.True(lockedOutUntilBeforeRetry > DateTimeOffset.UtcNow, "Test setup requires LockedOutUntil to be in the future.");

        // Retry with the CORRECT password while still locked out.
        var response = await authenticationService.SignInAsync(
            new LoginRequest("jdoe", Password),
            "127.0.0.1",
            CancellationToken.None);

        Assert.Null(response);

        // The lockout must not be extended or otherwise mutated by a retry during lockout.
        Assert.Equal(failedAttemptsBeforeRetry, user.FailedLoginAttempts);
        Assert.Equal(lockedOutUntilBeforeRetry, user.LockedOutUntil);

        var lockedOutAuditEntry = await dbContext.AuditLogs
            .SingleOrDefaultAsync(auditLog => auditLog.Action == "auth.login.locked_out");

        Assert.NotNull(lockedOutAuditEntry);
        Assert.Equal(user.Id, lockedOutAuditEntry!.UserId);
    }

    private static RaqmiDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<RaqmiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new RaqmiDbContext(options);
    }

    private static AuthenticationService CreateAuthenticationService(
        RaqmiDbContext dbContext,
        IPasswordHasher passwordHasher)
    {
        var jwtOptions = new JwtOptions
        {
            Issuer = "RaqmiSystem.Tests",
            Audience = "RaqmiSystem.Tests.Client",
            SigningKey = new string('k', 64),
            AccessTokenMinutes = 60
        };

        var tokenService = new JwtTokenService(Options.Create(jwtOptions));
        var auditLogWriter = new AuditLogWriter(dbContext);

        return new AuthenticationService(dbContext, passwordHasher, tokenService, auditLogWriter);
    }

    private static async Task<User> CreateAndSaveUserAsync(RaqmiDbContext dbContext, IPasswordHasher passwordHasher)
    {
        var user = new User("jdoe", "jdoe@example.com", "John Doe", passwordHasher.Hash(Password), mustChangePassword: false);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return user;
    }
}
