using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Inventory;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Inventory;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Inventory;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Regression coverage for the race the never-negative invariant is exposed to when it is
/// enforced by reading ("is there enough left?") and committing afterwards.
///
/// <see cref="InventoryServiceTests"/> proves the invariant against one request at a time, which
/// a check-then-commit implementation passes just as well as an atomic one. The interesting
/// failure needs two requests in flight at the same moment: two storerooms taking 8 kg out of
/// the same 10 kg, each reading "there are 10 left", both passing the check, both committing -
/// and a registry that sums to -6, a physical impossibility the module exists to prevent.
///
/// Same harness as <see cref="LodgingReservationConcurrencyTests"/>: a temporary file-backed
/// SQLite database (the shared ":memory:" connection cannot host two overlapping transactions),
/// two separate connections, and a <see cref="Rendezvous"/> planted in the audit writer - the
/// collaborator the service calls between its guard and its commit - to hold the first request
/// open until the second has passed its own guard. The rendezvous releases on its own after a
/// short delay so the winning request never hangs when the guard is atomic and the loser is
/// turned away before reaching the audit writer.
/// </summary>
public sealed class InventoryStockConcurrencyTests
{
    private const string UnitCode = "HTL1";

    private const string SourceWarehouse = "MAG1";

    private const string TargetWarehouse = "MAG2";

    private const string ItemCode = "FAR-T55";

    private const decimal InitialStock = 10m;

    private const decimal Withdrawal = 8m;

    private static readonly DateOnly MovementDate = new(2030, 3, 15);

    private static readonly TimeSpan RendezvousTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Two_simultaneous_outflows_cannot_both_empty_the_same_stock()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"raqmi-inventory-negative-stock-{Guid.NewGuid():N}.sqlite");

        // Pooling is disabled so every connection really closes when its DbContext is disposed
        // and the database file can be deleted at the end of the test.
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();

        try
        {
            await ArrangeStockAsync(connectionString);

            var rendezvous = new Rendezvous(RendezvousTimeout);

            await using var firstDbContext = CreateDbContext(connectionString);
            await using var secondDbContext = CreateDbContext(connectionString);

            var firstService = CreateService(firstDbContext, rendezvous);
            var secondService = CreateService(secondDbContext, rendezvous);

            var request = new CreateStockMovementRequest(
                SourceWarehouse,
                ItemCode,
                MovementDate,
                StockMovementKind.Consumption,
                Withdrawal,
                UnitCost: null,
                "BS-CONCURRENT",
                LotNumber: null,
                ExpiryDate: null,
                Notes: null,
                AdjustmentIsIncrease: null);

            var firstWithdrawal = Task.Run(() => firstService.CreateMovementAsync(
                request,
                new OperationContext(null, "cuisine", "127.0.0.1"),
                CancellationToken.None));

            var secondWithdrawal = Task.Run(() => secondService.CreateMovementAsync(
                request,
                new OperationContext(null, "etage", "127.0.0.1"),
                CancellationToken.None));

            var results = await Task.WhenAll(firstWithdrawal, secondWithdrawal);

            AssertExactlyOneWinner(results.Select(result => (result.Succeeded, result.ErrorType, result.Error)).ToArray());

            await AssertStockNeverWentNegativeAsync(connectionString, SourceWarehouse, InitialStock - Withdrawal);
        }
        finally
        {
            Delete(databasePath);
        }
    }

    [Fact]
    public async Task Two_simultaneous_transfers_cannot_both_take_the_same_goods_out_of_a_warehouse()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"raqmi-inventory-transfer-race-{Guid.NewGuid():N}.sqlite");

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();

        try
        {
            await ArrangeStockAsync(connectionString);

            var rendezvous = new Rendezvous(RendezvousTimeout);

            await using var firstDbContext = CreateDbContext(connectionString);
            await using var secondDbContext = CreateDbContext(connectionString);

            var firstService = CreateService(firstDbContext, rendezvous);
            var secondService = CreateService(secondDbContext, rendezvous);

            var request = new CreateStockTransferRequest(
                SourceWarehouse,
                TargetWarehouse,
                ItemCode,
                MovementDate,
                Withdrawal,
                "TR-CONCURRENT",
                LotNumber: null,
                ExpiryDate: null,
                Notes: null);

            var firstTransfer = Task.Run(() => firstService.TransferAsync(
                request,
                new OperationContext(null, "magasin.un", "127.0.0.1"),
                CancellationToken.None));

            var secondTransfer = Task.Run(() => secondService.TransferAsync(
                request,
                new OperationContext(null, "magasin.deux", "127.0.0.1"),
                CancellationToken.None));

            var results = await Task.WhenAll(firstTransfer, secondTransfer);

            AssertExactlyOneWinner(results.Select(result => (result.Succeeded, result.ErrorType, result.Error)).ToArray());

            await AssertStockNeverWentNegativeAsync(connectionString, SourceWarehouse, InitialStock - Withdrawal);

            await using var verificationDbContext = CreateDbContext(connectionString);

            // The winner moved the goods as a WHOLE: the destination holds exactly one
            // transfer's worth, and the two halves that exist share one group id.
            var transferHalves = await verificationDbContext.Set<StockMovement>()
                .AsNoTracking()
                .Where(movement => movement.TransferGroupId != null)
                .ToArrayAsync();

            Assert.Equal(2, transferHalves.Length);
            Assert.Single(transferHalves.Select(movement => movement.TransferGroupId).Distinct());

            var received = transferHalves
                .Where(movement => movement.WarehouseCode == TargetWarehouse)
                .Sum(movement => movement.SignedQuantity);

            Assert.Equal(Withdrawal, received);
        }
        finally
        {
            Delete(databasePath);
        }
    }

    /// <summary>
    /// Exactly one request may go through, and the loser must be refused for a REASON the module
    /// owns: either the invariant itself (it re-checked after the winner committed) or the
    /// retryable conflict raised when its write ran into the winner's still-open transaction.
    /// Never an unexplained failure.
    /// </summary>
    private static void AssertExactlyOneWinner((bool Succeeded, ApplicationErrorType ErrorType, string? Error)[] results)
    {
        var succeeded = results.Where(result => result.Succeeded).ToArray();
        var refused = results.Where(result => !result.Succeeded).ToArray();

        Assert.True(
            succeeded.Length == 1,
            $"Exactly one of the two concurrent outflows may go through; {succeeded.Length} did.");

        Assert.True(
            refused[0].ErrorType == ApplicationErrorType.Conflict,
            $"Unexpected refusal ({refused[0].ErrorType}): {refused[0].Error}");
    }

    /// <summary>
    /// The assertion that matters is not "one request failed" but "the registry still sums to a
    /// possible quantity": stock is the sum of the movements, so a corrupted outcome shows up
    /// here as a negative or short balance whatever the services reported.
    /// </summary>
    private static async Task AssertStockNeverWentNegativeAsync(
        string connectionString,
        string warehouseCode,
        decimal expectedStock)
    {
        await using var dbContext = CreateDbContext(connectionString);

        var movements = await dbContext.Set<StockMovement>()
            .AsNoTracking()
            .Where(movement => movement.WarehouseCode == warehouseCode && movement.ItemCode == ItemCode)
            .ToArrayAsync();

        var stock = movements.Sum(movement => movement.SignedQuantity);

        Assert.True(stock >= 0m, $"The registry summed to {stock}: stock can never be negative.");
        Assert.Equal(expectedStock, stock);
    }

    /// <summary>
    /// Builds the schema and seeds one unit, two warehouses, one item and a single entry of
    /// <see cref="InitialStock"/> - the smallest population in which either withdrawal alone
    /// would be perfectly legitimate, and both together impossible.
    /// </summary>
    private static async Task ArrangeStockAsync(string connectionString)
    {
        await using var dbContext = CreateDbContext(connectionString);
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Set<HotelUnit>().Add(new HotelUnit(UnitCode, "Hotel Test", HotelUnitType.Hotel));
        dbContext.Set<Warehouse>().Add(new Warehouse(SourceWarehouse, "Magasin central", UnitCode));
        dbContext.Set<Warehouse>().Add(new Warehouse(TargetWarehouse, "Magasin annexe", UnitCode));
        dbContext.Set<StockItem>().Add(new StockItem(ItemCode, "Farine T55", "kg", StockItemCategory.Alimentaire));

        dbContext.Set<StockMovement>().Add(StockMovement.PurchaseEntry(
            SourceWarehouse,
            ItemCode,
            MovementDate,
            InitialStock,
            250.00m,
            "BL-SEED"));

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// One DbContext per connection STRING (not a shared connection instance), so each request
    /// opens a connection of its own and the two can really be in flight at the same time.
    /// </summary>
    private static RaqmiDbContext CreateDbContext(string connectionString)
    {
        return new RaqmiDbContext(
            new DbContextOptionsBuilder<RaqmiDbContext>()
                .UseSqlite(connectionString)
                .Options);
    }

    private static InventoryService CreateService(RaqmiDbContext dbContext, Rendezvous rendezvous)
    {
        return new InventoryService(
            dbContext,
            new RendezvousAuditLogWriter(new AuditLogWriter(dbContext), rendezvous));
    }

    private static void Delete(string databasePath)
    {
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// Holds a request at the point where it has re-derived the stock but has not committed yet
    /// - the exact window a check-then-commit guard leaves open - and lets it go once the other
    /// request has reached the same point, or once <see cref="RendezvousTimeout"/> has elapsed.
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
    /// The service writes its audit entry after the never-negative check and before committing,
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
