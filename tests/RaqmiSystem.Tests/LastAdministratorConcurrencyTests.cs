using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Identity;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Security;

namespace RaqmiSystem.Tests;

/// <summary>
/// Regression coverage for the race that guard (c) - "the installation must keep at least one
/// ACTIVE holder of users.write" - is exposed to when it is enforced by reading a count and
/// committing afterwards.
///
/// <see cref="LastAdministratorGuardTests"/> proves the guard against one request at a time, which
/// a check-then-commit implementation passes just as well as an atomic one. The interesting failure
/// is the one that needs two requests in flight at the same moment: two administrators, each
/// deactivating the other, each reading "the other one is still active", both passing the guard,
/// both committing - and the installation left with nobody able to administer users.
///
/// Proving that needs things the shared <see cref="RaqmiApiFactory"/> harness cannot provide, so
/// this class drives <see cref="UserAdministrationService"/> directly instead of going through HTTP:
///   - two SEPARATE database connections (the factory hands every DbContext the same SQLite
///     ":memory:" connection, on which two transactions cannot overlap at all), hence a temporary
///     file-backed SQLite database here;
///   - a way to hold one request open between its guard and its commit, which is what
///     <see cref="Rendezvous"/> does from inside the audit writer - the one collaborator the
///     service calls at exactly that point.
///
/// The rendezvous releases on its own after a short delay instead of waiting for its partner
/// forever, because the two outcomes are meant to be asymmetric: with the guard enforced
/// atomically, the second request never reaches the audit writer at all (its conditional claim runs
/// into the first request's write lock and comes back as a retryable conflict), so waiting for it
/// unconditionally would hang. With a check-then-commit guard, both requests DO meet at the
/// rendezvous, both go on to commit, and the assertions below fail - which is precisely what this
/// test is for.
/// </summary>
public sealed class LastAdministratorConcurrencyTests
{
    private const string Password = "Correct-Horse-Battery-42!";

    /// <summary>
    /// Long enough for the losing request to reach its claim and be turned away, short enough not
    /// to weigh on the suite: the winning request only ever waits this out once.
    /// </summary>
    private static readonly TimeSpan RendezvousTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Two_administrators_deactivating_each_other_at_the_same_moment_cannot_both_succeed()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"raqmi-last-administrator-{Guid.NewGuid():N}.sqlite");

        // Pooling is disabled so that every connection is really closed when its DbContext is
        // disposed, and the database file can be deleted at the end of the test.
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();

        try
        {
            var (firstId, secondId) = await ArrangeTwoActiveAdministratorsAsync(connectionString);

            var rendezvous = new Rendezvous(RendezvousTimeout);

            await using var firstDbContext = CreateDbContext(connectionString);
            await using var secondDbContext = CreateDbContext(connectionString);

            var firstService = CreateService(firstDbContext, rendezvous);
            var secondService = CreateService(secondDbContext, rendezvous);

            // Each of the two administrators deactivates the other one. Neither request is a
            // self-deactivation, so guards (a) and (b) stay out of the way and guard (c) is the
            // only thing standing between these two calls and an installation with zero
            // administrators.
            var firstDeactivation = Task.Run(() => firstService.SetActiveAsync(
                secondId,
                isActive: false,
                new OperationContext(firstId, "admin.one", "127.0.0.1"),
                CancellationToken.None));

            var secondDeactivation = Task.Run(() => secondService.SetActiveAsync(
                firstId,
                isActive: false,
                new OperationContext(secondId, "admin.two", "127.0.0.1"),
                CancellationToken.None));

            var results = await Task.WhenAll(firstDeactivation, secondDeactivation);

            var succeeded = results.Where(result => result.Succeeded).ToArray();
            var refused = results.Where(result => !result.Succeeded).ToArray();

            Assert.True(
                succeeded.Length == 1,
                "Exactly one of the two concurrent deactivations may go through; " +
                $"{succeeded.Length} did.");

            // Refused for the right reason: either the invariant itself (the loser re-asserted it
            // after the winner had committed), or the retryable conflict raised when the loser's
            // claim ran into the winner's still-open transaction. Never an unexplained failure.
            Assert.True(
                refused[0].ErrorType is ApplicationErrorType.Validation or ApplicationErrorType.Conflict,
                $"Unexpected refusal ({refused[0].ErrorType}): {refused[0].Error}");

            await using var verificationDbContext = CreateDbContext(connectionString);

            var activeAdministrators = await CountActiveAdministratorsAsync(verificationDbContext);

            Assert.True(
                activeAdministrators == 1,
                "The installation must keep an active users.write holder whatever the concurrency; " +
                $"{activeAdministrators} remain.");

            // The refused request must not have left a trace of something that did not happen.
            var deactivationEntries = await verificationDbContext.AuditLogs
                .CountAsync(auditLog => auditLog.Action == "security.user.deactivated");

            Assert.Equal(1, deactivationEntries);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    /// <summary>
    /// Builds the schema, seeds permissions and roles, and creates exactly two active accounts
    /// holding users.write - the population in which deactivating either one is legitimate and
    /// deactivating both is not.
    /// </summary>
    private static async Task<(Guid FirstId, Guid SecondId)> ArrangeTwoActiveAdministratorsAsync(
        string connectionString)
    {
        await using var dbContext = CreateDbContext(connectionString);
        await dbContext.Database.EnsureCreatedAsync();

        var passwordHasher = new Pbkdf2PasswordHasher();
        await new SecuritySeeder(dbContext, passwordHasher).SeedAsync(CancellationToken.None);

        var administratorRole = await dbContext.Roles
            .SingleAsync(role => role.Name == RoleCatalog.SystemAdministrator);

        var first = CreateAdministrator("admin.one", passwordHasher, administratorRole);
        var second = CreateAdministrator("admin.two", passwordHasher, administratorRole);

        dbContext.Users.AddRange(first, second);
        await dbContext.SaveChangesAsync();

        var activeAdministrators = await CountActiveAdministratorsAsync(dbContext);

        Assert.True(
            activeAdministrators == 2,
            "The race is about the LAST administrator, so it must start from exactly two; " +
            $"the database holds {activeAdministrators}.");

        return (first.Id, second.Id);
    }

    private static User CreateAdministrator(string userName, IPasswordHasher passwordHasher, Role role)
    {
        var user = new User(
            userName,
            $"{userName}@example.com",
            userName,
            passwordHasher.Hash(Password),
            mustChangePassword: false);

        user.AssignRole(role, DateTimeOffset.UtcNow);

        return user;
    }

    private static Task<int> CountActiveAdministratorsAsync(RaqmiDbContext dbContext)
    {
        return dbContext.Users
            .AsNoTracking()
            .CountAsync(user => user.IsActive
                && user.Roles.Any(userRole =>
                    userRole.Role.Permissions.Any(rolePermission =>
                        rolePermission.Permission.Key == PermissionCatalog.UsersWrite)));
    }

    /// <summary>
    /// One DbContext per connection: the options carry a connection STRING rather than a shared
    /// connection instance, so each context opens a connection of its own and the two requests can
    /// really be in flight at the same time.
    /// </summary>
    private static RaqmiDbContext CreateDbContext(string connectionString)
    {
        return new RaqmiDbContext(
            new DbContextOptionsBuilder<RaqmiDbContext>()
                .UseSqlite(connectionString)
                .Options);
    }

    private static UserAdministrationService CreateService(RaqmiDbContext dbContext, Rendezvous rendezvous)
    {
        return new UserAdministrationService(
            dbContext,
            new Pbkdf2PasswordHasher(),
            new RendezvousAuditLogWriter(new AuditLogWriter(dbContext), rendezvous));
    }

    /// <summary>
    /// Holds a request at the point where it has passed its guard but has not committed yet - the
    /// exact window a check-then-commit guard leaves open - and lets it go once the other request
    /// has reached the same point, or once <see cref="RendezvousTimeout"/> has elapsed (see the
    /// class remarks for why the wait must be bounded).
    /// </summary>
    private sealed class Rendezvous(TimeSpan timeout)
    {
        private readonly TaskCompletionSource _bothArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public Task ArriveAsync()
        {
            if (Interlocked.Increment(ref _arrivals) >= 2)
            {
                _bothArrived.TrySetResult();
                return Task.CompletedTask;
            }

            return Task.WhenAny(_bothArrived.Task, Task.Delay(timeout));
        }
    }

    /// <summary>
    /// The service writes its audit entry after deciding the mutation is allowed and before
    /// committing it, which makes the audit writer the natural place to suspend a request inside
    /// that window without touching production code.
    /// </summary>
    private sealed class RendezvousAuditLogWriter(IAuditLogWriter inner, Rendezvous rendezvous) : IAuditLogWriter
    {
        public async Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken)
        {
            await rendezvous.ArriveAsync();
            await inner.WriteAsync(entry, cancellationToken);
        }
    }
}
