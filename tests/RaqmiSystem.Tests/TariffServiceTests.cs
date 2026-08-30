using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Tariffs;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Tariffs;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Tariffs;

namespace RaqmiSystem.Tests;

/// <summary>
/// Covers TariffService against a real relational provider (SQLite ":memory:", the same
/// technique as ReceivablesServiceTests): the one-default-active-plan invariant is backed by a
/// filtered unique index and the overlap rules run real range queries, so neither can be
/// honestly exercised against hand-built in-memory lists.
///
/// The tests deliberately drive the service directly rather than over HTTP: the
/// "tariffs.read"/"tariffs.write" authorization policies are wired by the integration pass
/// (PermissionCatalog + Program.cs), which this module does not own.
/// </summary>
public sealed class TariffServiceTests
{
    private const string UnitCode = "HTL-TARIF";

    private const string OtherUnitCode = "HTL-AUTRE";

    private const string CustomerCode = "CLI-CONV";

    private static readonly OperationContext Context = new(null, "tests", "127.0.0.1");

    [Fact]
    public async Task Overlapping_periods_of_the_same_plan_and_room_type_are_refused_bounds_included()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = await CreateDbContextAsync(connection);
        var service = CreateService(dbContext);

        await SeedOrganizationAsync(dbContext);
        await CreatePlanAsync(service, "PLAN-A", UnitCode, isDefault: true);

        var seed = await service.AddPeriodAsync(
            "PLAN-A",
            new CreateRatePeriodRequest("DBL", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 10), 10_000m),
            Context,
            CancellationToken.None);

        Assert.True(seed.Succeeded, seed.Error);

        // BOUNDARY: a period starting on the very day the existing one ends overlaps - the night
        // of the 10th would carry two prices.
        var startsOnLastDay = await service.AddPeriodAsync(
            "PLAN-A",
            new CreateRatePeriodRequest("DBL", new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 20), 12_000m),
            Context,
            CancellationToken.None);

        Assert.Equal(ApplicationErrorType.Conflict, startsOnLastDay.ErrorType);

        // A period fully inside the existing one is refused too.
        var contained = await service.AddPeriodAsync(
            "PLAN-A",
            new CreateRatePeriodRequest("DBL", new DateOnly(2026, 6, 5), new DateOnly(2026, 6, 6), 9_000m),
            Context,
            CancellationToken.None);

        Assert.Equal(ApplicationErrorType.Conflict, contained.ErrorType);

        // Starting the day after is the first legal layout.
        var adjacent = await service.AddPeriodAsync(
            "PLAN-A",
            new CreateRatePeriodRequest("DBL", new DateOnly(2026, 6, 11), new DateOnly(2026, 6, 20), 12_000m),
            Context,
            CancellationToken.None);

        Assert.True(adjacent.Succeeded, adjacent.Error);

        // Same nights, other room type: no conflict.
        var otherRoomType = await service.AddPeriodAsync(
            "PLAN-A",
            new CreateRatePeriodRequest("SUITE", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 10), 30_000m),
            Context,
            CancellationToken.None);

        Assert.True(otherRoomType.Succeeded, otherRoomType.Error);

        // Rescheduling an existing period into its neighbour is refused as well...
        var updateIntoNeighbour = await service.UpdatePeriodAsync(
            "PLAN-A",
            adjacent.Value!.Id,
            new UpdateRatePeriodRequest(new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 20), 12_000m),
            Context,
            CancellationToken.None);

        Assert.Equal(ApplicationErrorType.Conflict, updateIntoNeighbour.ErrorType);

        // ...while rescheduling within its own slot (a period never conflicts with itself) works.
        var reschedule = await service.UpdatePeriodAsync(
            "PLAN-A",
            adjacent.Value.Id,
            new UpdateRatePeriodRequest(new DateOnly(2026, 6, 12), new DateOnly(2026, 6, 19), 12_500m),
            Context,
            CancellationToken.None);

        Assert.True(reschedule.Succeeded, reschedule.Error);
        Assert.Equal(12_500m, reschedule.Value!.NightlyAmount);
    }

    [Fact]
    public async Task A_unit_has_at_most_one_active_default_plan_and_set_default_swaps_it()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = await CreateDbContextAsync(connection);
        var service = CreateService(dbContext);

        await SeedOrganizationAsync(dbContext);

        await CreatePlanAsync(service, "PLAN-DEF", UnitCode, isDefault: true);
        await CreatePlanAsync(service, "PLAN-ALT", UnitCode, isDefault: false);

        // A second default for the same unit is refused outright.
        var secondDefault = await service.CreatePlanAsync(
            new CreateRatePlanRequest("PLAN-DEF2", "Deuxieme defaut", UnitCode, IsDefault: true),
            Context,
            CancellationToken.None);

        Assert.Equal(ApplicationErrorType.Conflict, secondDefault.ErrorType);

        // Another unit keeps its own, independent default.
        var otherUnitDefault = await service.CreatePlanAsync(
            new CreateRatePlanRequest("PLAN-B-DEF", "Defaut autre unite", OtherUnitCode, IsDefault: true),
            Context,
            CancellationToken.None);

        Assert.True(otherUnitDefault.Succeeded, otherUnitDefault.Error);

        // set-default atomically swaps the flag inside the unit.
        var swapped = await service.SetPlanDefaultAsync("PLAN-ALT", Context, CancellationToken.None);

        Assert.True(swapped.Succeeded, swapped.Error);
        Assert.True(swapped.Value!.IsDefault);

        var previous = await service.GetPlanAsync("PLAN-DEF", CancellationToken.None);
        Assert.False(previous.Value!.IsDefault);

        // An inactive plan cannot become the default.
        var deactivated = await service.SetPlanActiveAsync("PLAN-DEF", false, Context, CancellationToken.None);
        Assert.True(deactivated.Succeeded, deactivated.Error);

        var inactiveDefault = await service.SetPlanDefaultAsync("PLAN-DEF", Context, CancellationToken.None);
        Assert.Equal(ApplicationErrorType.Validation, inactiveDefault.ErrorType);
    }

    [Fact]
    public async Task Reactivating_a_dormant_default_while_another_default_exists_is_refused_by_the_filtered_index()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = await CreateDbContextAsync(connection);
        var service = CreateService(dbContext);

        await SeedOrganizationAsync(dbContext);

        await CreatePlanAsync(service, "PLAN-OLD", UnitCode, isDefault: true);

        // Deactivate the default (it keeps its dormant is_default flag)...
        var deactivated = await service.SetPlanActiveAsync("PLAN-OLD", false, Context, CancellationToken.None);
        Assert.True(deactivated.Succeeded, deactivated.Error);

        // ...which frees the slot for a new default...
        var newDefault = await service.CreatePlanAsync(
            new CreateRatePlanRequest("PLAN-NEW", "Nouveau defaut", UnitCode, IsDefault: true),
            Context,
            CancellationToken.None);

        Assert.True(newDefault.Succeeded, newDefault.Error);

        // ...and reactivating the old one now collides with ux_rate_plans_default_per_unit.
        var reactivated = await service.SetPlanActiveAsync("PLAN-OLD", true, Context, CancellationToken.None);

        Assert.Equal(ApplicationErrorType.Conflict, reactivated.ErrorType);
    }

    [Fact]
    public async Task A_second_active_default_plan_is_refused_by_the_database_itself()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = await CreateDbContextAsync(connection);

        await SeedOrganizationAsync(dbContext);

        // Both rows are written straight through the DbContext, bypassing the service and every
        // one of its pre-checks: the only thing that can refuse the second row is the filtered
        // unique index ux_rate_plans_default_per_unit. This is the proof the invariant lives in
        // the SCHEMA, not merely in TariffService's friendly checks.
        var firstDefault = new RatePlan("RAW-DEF1", "Premier defaut", UnitCode, isDefault: true);
        firstDefault.MarkCreated("tests", DateTimeOffset.UtcNow);
        dbContext.Add(firstDefault);
        await dbContext.SaveChangesAsync();

        var secondDefault = new RatePlan("RAW-DEF2", "Deuxieme defaut", UnitCode, isDefault: true);
        secondDefault.MarkCreated("tests", DateTimeOffset.UtcNow);
        dbContext.Add(secondDefault);

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        dbContext.ChangeTracker.Clear();

        // The filter really is a filter: a default-flagged but INACTIVE row does not trip the
        // index, and a default row on ANOTHER unit does not either.
        var dormantDefault = new RatePlan("RAW-DEF3", "Defaut dormant", UnitCode, isDefault: true);
        dormantDefault.Deactivate();
        dormantDefault.MarkCreated("tests", DateTimeOffset.UtcNow);

        var otherUnitDefault = new RatePlan("RAW-DEF4", "Defaut autre unite", OtherUnitCode, isDefault: true);
        otherUnitDefault.MarkCreated("tests", DateTimeOffset.UtcNow);

        dbContext.AddRange(dormantDefault, otherUnitDefault);
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task A_customer_cannot_have_two_active_conventions_valid_on_the_same_day()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = await CreateDbContextAsync(connection);
        var service = CreateService(dbContext);

        await SeedOrganizationAsync(dbContext);
        await CreatePlanAsync(service, "PLAN-A", UnitCode, isDefault: true);
        await CreatePlanAsync(service, "PLAN-B", UnitCode, isDefault: false);

        var firstHalf = await service.CreateConventionAsync(
            new CreateCustomerConventionRequest(
                CustomerCode, "PLAN-A", 10m, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30)),
            Context,
            CancellationToken.None);

        Assert.True(firstHalf.Succeeded, firstHalf.Error);

        // BOUNDARY: a convention starting the very day the existing one ends shares that day.
        var startsOnLastDay = await service.CreateConventionAsync(
            new CreateCustomerConventionRequest(
                CustomerCode, "PLAN-B", 5m, new DateOnly(2026, 6, 30), new DateOnly(2026, 12, 31)),
            Context,
            CancellationToken.None);

        Assert.Equal(ApplicationErrorType.Conflict, startsOnLastDay.ErrorType);

        // Starting the day after is legal.
        var secondHalf = await service.CreateConventionAsync(
            new CreateCustomerConventionRequest(
                CustomerCode, "PLAN-B", 5m, new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31)),
            Context,
            CancellationToken.None);

        Assert.True(secondHalf.Succeeded, secondHalf.Error);

        // Stretching the second one back over the first is refused on update too.
        var stretchedBack = await service.UpdateConventionAsync(
            secondHalf.Value!.Id,
            new UpdateCustomerConventionRequest("PLAN-B", 5m, new DateOnly(2026, 6, 15), new DateOnly(2026, 12, 31)),
            Context,
            CancellationToken.None);

        Assert.Equal(ApplicationErrorType.Conflict, stretchedBack.ErrorType);

        // Deactivated conventions no longer block the window - and reactivating one that would
        // recreate the overlap is refused.
        var deactivated = await service.SetConventionActiveAsync(
            firstHalf.Value!.Id, false, Context, CancellationToken.None);
        Assert.True(deactivated.Succeeded, deactivated.Error);

        var replacement = await service.CreateConventionAsync(
            new CreateCustomerConventionRequest(
                CustomerCode, "PLAN-A", 15m, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30)),
            Context,
            CancellationToken.None);

        Assert.True(replacement.Succeeded, replacement.Error);

        var reactivated = await service.SetConventionActiveAsync(
            firstHalf.Value.Id, true, Context, CancellationToken.None);

        Assert.Equal(ApplicationErrorType.Conflict, reactivated.ErrorType);
    }

    [Fact]
    public async Task Conventions_require_an_existing_active_customer_and_an_active_plan()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = await CreateDbContextAsync(connection);
        var service = CreateService(dbContext);

        await SeedOrganizationAsync(dbContext);
        await CreatePlanAsync(service, "PLAN-A", UnitCode, isDefault: true);

        var unknownCustomer = await service.CreateConventionAsync(
            new CreateCustomerConventionRequest(
                "NOBODY", "PLAN-A", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
            Context,
            CancellationToken.None);

        Assert.Equal(ApplicationErrorType.NotFound, unknownCustomer.ErrorType);

        var unknownPlan = await service.CreateConventionAsync(
            new CreateCustomerConventionRequest(
                CustomerCode, "NO-PLAN", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
            Context,
            CancellationToken.None);

        Assert.Equal(ApplicationErrorType.NotFound, unknownPlan.ErrorType);

        // Deactivate the customer directly (billing owns that workflow; this test only needs the state).
        var customer = await dbContext.Set<Customer>().SingleAsync(current => current.Code == CustomerCode);
        customer.Deactivate();
        await dbContext.SaveChangesAsync();

        var inactiveCustomer = await service.CreateConventionAsync(
            new CreateCustomerConventionRequest(
                CustomerCode, "PLAN-A", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
            Context,
            CancellationToken.None);

        Assert.Equal(ApplicationErrorType.Validation, inactiveCustomer.ErrorType);
    }

    private static async Task CreatePlanAsync(TariffService service, string code, string unitCode, bool isDefault)
    {
        var result = await service.CreatePlanAsync(
            new CreateRatePlanRequest(code, $"Plan {code}", unitCode, isDefault),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
    }

    private static async Task<RaqmiDbContext> CreateDbContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<RaqmiDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new RaqmiDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        return dbContext;
    }

    private static TariffService CreateService(RaqmiDbContext dbContext)
    {
        return new TariffService(dbContext, new AuditLogWriter(dbContext));
    }

    private static async Task SeedOrganizationAsync(RaqmiDbContext dbContext)
    {
        dbContext.Add(new HotelUnit(UnitCode, "Hotel Tarifs", HotelUnitType.Hotel));
        dbContext.Add(new HotelUnit(OtherUnitCode, "Hotel Autre", HotelUnitType.Hotel));
        dbContext.Add(new Customer(CustomerCode, "Client Conventionne", CustomerType.Company));

        await dbContext.SaveChangesAsync();
    }
}
