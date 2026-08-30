using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Lodging;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Regression coverage for the race the anti-double-booking invariant is exposed to when it is
/// enforced by reading ("is the room free?") and committing afterwards.
///
/// <see cref="LodgingServiceTests"/> proves the invariant against one request at a time, which a
/// check-then-commit implementation passes just as well as an atomic one. The interesting
/// failure needs two requests in flight at the same moment: two front desks booking the SAME
/// room for the SAME dates, each reading "the room is free", both passing the check, both
/// committing - and the hotel left with two guests holding one key.
///
/// Same harness as <see cref="LastAdministratorConcurrencyTests"/>: a temporary file-backed
/// SQLite database (the shared ":memory:" connection cannot host two overlapping transactions),
/// two separate connections, and a <see cref="Rendezvous"/> planted in the audit writer - the
/// collaborator the service calls between its guard and its commit - to hold the first request
/// open until the second has passed its own guard. The rendezvous releases on its own after a
/// short delay so the winning request never hangs when the guard is atomic and the loser is
/// turned away before reaching the audit writer.
/// </summary>
public sealed class LodgingReservationConcurrencyTests
{
    private const string UnitCode = "HTL1";

    private const string CustomerCode = "CLI1";

    private static readonly DateOnly Arrival = new(2030, 5, 1);

    private static readonly DateOnly Departure = new(2030, 5, 4);

    private static readonly TimeSpan RendezvousTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Two_simultaneous_bookings_of_the_same_room_and_dates_cannot_both_succeed()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"raqmi-lodging-double-booking-{Guid.NewGuid():N}.sqlite");

        // Pooling is disabled so every connection really closes when its DbContext is disposed
        // and the database file can be deleted at the end of the test.
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();

        try
        {
            var roomId = await ArrangeRoomAsync(connectionString);

            var rendezvous = new Rendezvous(RendezvousTimeout);

            await using var firstDbContext = CreateDbContext(connectionString);
            await using var secondDbContext = CreateDbContext(connectionString);

            var firstService = CreateService(firstDbContext, rendezvous);
            var secondService = CreateService(secondDbContext, rendezvous);

            var request = new CreateReservationRequest(
                UnitCode,
                roomId,
                CustomerCode,
                Arrival,
                Departure,
                GuestCount: 1);

            var firstBooking = Task.Run(() => firstService.CreateReservationAsync(
                request,
                new OperationContext(null, "desk.one", "127.0.0.1"),
                CancellationToken.None));

            var secondBooking = Task.Run(() => secondService.CreateReservationAsync(
                request,
                new OperationContext(null, "desk.two", "127.0.0.1"),
                CancellationToken.None));

            var results = await Task.WhenAll(firstBooking, secondBooking);

            var succeeded = results.Where(result => result.Succeeded).ToArray();
            var refused = results.Where(result => !result.Succeeded).ToArray();

            Assert.True(
                succeeded.Length == 1,
                $"Exactly one of the two concurrent bookings may go through; {succeeded.Length} did.");

            // Refused for the right reason: either the invariant itself (the loser re-checked
            // after the winner committed) or the retryable conflict raised when the loser's
            // write ran into the winner's still-open transaction. Never an unexplained failure.
            Assert.True(
                refused[0].ErrorType == ApplicationErrorType.Conflict,
                $"Unexpected refusal ({refused[0].ErrorType}): {refused[0].Error}");

            await using var verificationDbContext = CreateDbContext(connectionString);

            var blockingReservations = await verificationDbContext.Set<Reservation>()
                .AsNoTracking()
                .CountAsync(reservation => reservation.RoomId == roomId
                    && reservation.Status != ReservationStatus.Cancelled
                    && reservation.Status != ReservationStatus.NoShow);

            Assert.True(
                blockingReservations == 1,
                "The room must end up with exactly one blocking reservation whatever the " +
                $"concurrency; {blockingReservations} were persisted.");

            // The refused request must not have left a trace of a booking that did not happen.
            var creationAuditEntries = await verificationDbContext.AuditLogs
                .CountAsync(auditLog => auditLog.Action == "lodging.reservation.created");

            Assert.Equal(1, creationAuditEntries);
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
    /// Builds the schema and seeds one unit, one room type, one room and one customer - the
    /// smallest population in which either booking alone would be perfectly legitimate.
    /// </summary>
    private static async Task<Guid> ArrangeRoomAsync(string connectionString)
    {
        await using var dbContext = CreateDbContext(connectionString);
        await dbContext.Database.EnsureCreatedAsync();

        var room = new Room(UnitCode, "101", "DBL");

        dbContext.Set<HotelUnit>().Add(new HotelUnit(UnitCode, "Hotel Test", HotelUnitType.Hotel));
        dbContext.Set<RoomType>().Add(new RoomType(UnitCode, "DBL", "Chambre double", 2));
        dbContext.Set<Room>().Add(room);
        dbContext.Set<Customer>().Add(new Customer(CustomerCode, "Client Un", CustomerType.Individual));

        await dbContext.SaveChangesAsync();

        return room.Id;
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

    private static LodgingService CreateService(RaqmiDbContext dbContext, Rendezvous rendezvous)
    {
        return new LodgingService(
            dbContext,
            new RendezvousAuditLogWriter(new AuditLogWriter(dbContext), rendezvous),
            new StubTariffResolutionService());
    }

    /// <summary>
    /// Holds a request at the point where it has passed its overlap check but has not committed
    /// yet - the exact window a check-then-commit guard leaves open - and lets it go once the
    /// other request has reached the same point, or once <see cref="RendezvousTimeout"/> has
    /// elapsed.
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
    /// The service writes its audit entry after the overlap check and before committing, which
    /// makes the audit writer the natural place to suspend a request inside that window without
    /// touching production code.
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
