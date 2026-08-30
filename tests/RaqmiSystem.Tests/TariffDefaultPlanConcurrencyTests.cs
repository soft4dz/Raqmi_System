using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Tariffs;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Tariffs;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Tariffs;

namespace RaqmiSystem.Tests;

/// <summary>
/// Regression coverage for the race the one-default-active-plan-per-unit invariant is exposed
/// to when two requests each try to make a DIFFERENT plan the default of the SAME unit at the
/// same moment. Each request reads "the other plan is the current default", clears it, flags
/// its own - run without real isolation, both could commit and leave the unit with two active
/// defaults (or none).
///
/// Like <see cref="LastAdministratorConcurrencyTests"/> (the model for this class), proving
/// that needs two SEPARATE database connections - the shared <see cref="RaqmiApiFactory"/>
/// harness hands every DbContext the same SQLite ":memory:" connection, on which two
/// transactions cannot overlap at all - hence a temporary file-backed SQLite database and the
/// same audit-writer <see cref="Rendezvous"/> to hold a request open between its default swap
/// and its commit.
///
/// The acceptable outcomes are asymmetric: with the Serializable transaction + filtered unique
/// index in place, either the two swaps serialize (both succeed, the LAST one holds the flag)
/// or the loser is turned away with a retryable conflict. What may never happen is what the
/// final assertion pins down: two active default plans for one unit.
/// </summary>
public sealed class TariffDefaultPlanConcurrencyTests
{
    private const string UnitCode = "HTL-RACE";

    private static readonly TimeSpan RendezvousTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Two_concurrent_default_swaps_on_the_same_unit_leave_exactly_one_active_default()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"raqmi-tariff-default-race-{Guid.NewGuid():N}.sqlite");

        // Pooling is disabled so that every connection is really closed when its DbContext is
        // disposed, and the database file can be deleted at the end of the test.
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();

        try
        {
            await ArrangeUnitWithTwoCandidatePlansAsync(connectionString);

            var rendezvous = new Rendezvous(RendezvousTimeout);

            await using var firstDbContext = CreateDbContext(connectionString);
            await using var secondDbContext = CreateDbContext(connectionString);

            var firstService = CreateService(firstDbContext, rendezvous);
            var secondService = CreateService(secondDbContext, rendezvous);

            var firstContext = new OperationContext(null, "manager.one", "127.0.0.1");
            var secondContext = new OperationContext(null, "manager.two", "127.0.0.1");

            // Two managers, each promoting a different plan of the same unit at the same moment.
            var firstSwap = Task.Run(() => firstService.SetPlanDefaultAsync(
                "PLAN-A", firstContext, CancellationToken.None));

            var secondSwap = Task.Run(() => secondService.SetPlanDefaultAsync(
                "PLAN-B", secondContext, CancellationToken.None));

            var results = await Task.WhenAll(firstSwap, secondSwap);

            var succeeded = results.Where(result => result.Succeeded).ToArray();
            var refused = results.Where(result => !result.Succeeded).ToArray();

            // At least one of the two swaps must go through - the guard may serialize them (both
            // succeed, one after the other) but must never refuse both.
            Assert.True(
                succeeded.Length >= 1,
                $"At least one of the two concurrent swaps must succeed; none did " +
                $"({string.Join(" / ", refused.Select(result => result.Error))}).");

            // A refused swap must be refused as a RETRYABLE conflict, never an unexplained error.
            Assert.All(refused, result =>
                Assert.Equal(ApplicationErrorType.Conflict, result.ErrorType));

            // THE invariant: whatever the interleaving, the unit ends with exactly one active
            // default plan - never two, never zero.
            await using var verificationDbContext = CreateDbContext(connectionString);

            var activeDefaults = await verificationDbContext.Set<RatePlan>()
                .AsNoTracking()
                .Where(plan => plan.HotelUnitCode == UnitCode && plan.IsDefault && plan.IsActive)
                .Select(plan => plan.Code)
                .ToArrayAsync();

            Assert.True(
                activeDefaults.Length == 1,
                "The unit must keep exactly one active default plan whatever the concurrency; " +
                $"it holds {activeDefaults.Length} ({string.Join(", ", activeDefaults)}).");
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
    /// Builds the schema and creates one unit with two active, NON-default plans: the starting
    /// population in which promoting either plan is legitimate and ending with two defaults is not.
    /// </summary>
    private static async Task ArrangeUnitWithTwoCandidatePlansAsync(string connectionString)
    {
        await using var dbContext = CreateDbContext(connectionString);
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Add(new HotelUnit(UnitCode, "Hotel Course", HotelUnitType.Hotel));

        var planA = new RatePlan("PLAN-A", "Plan A", UnitCode, isDefault: false);
        planA.MarkCreated("tests", DateTimeOffset.UtcNow);

        var planB = new RatePlan("PLAN-B", "Plan B", UnitCode, isDefault: false);
        planB.MarkCreated("tests", DateTimeOffset.UtcNow);

        dbContext.AddRange(planA, planB);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// One DbContext per connection STRING (not per shared connection instance), so each request
    /// opens a connection of its own and the two can really be in flight at the same time.
    /// </summary>
    private static RaqmiDbContext CreateDbContext(string connectionString)
    {
        return new RaqmiDbContext(
            new DbContextOptionsBuilder<RaqmiDbContext>()
                .UseSqlite(connectionString)
                .Options);
    }

    private static TariffService CreateService(RaqmiDbContext dbContext, Rendezvous rendezvous)
    {
        return new TariffService(
            dbContext,
            new RendezvousAuditLogWriter(new AuditLogWriter(dbContext), rendezvous));
    }

    /// <summary>
    /// Holds a request at the point where it has performed its default swap but has not
    /// committed yet - the exact window a check-then-commit implementation leaves open - and
    /// lets it go once the other request has reached the same point, or once
    /// <see cref="RendezvousTimeout"/> has elapsed (with the guard enforced correctly, the
    /// second request may never reach the audit writer at all, so the wait must be bounded).
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
    /// TariffService writes its audit entry after the swap is decided and before committing it,
    /// which makes the audit writer the natural place to suspend a request inside that window
    /// without touching production code.
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
