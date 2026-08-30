using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Tariffs;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Tariffs;

namespace RaqmiSystem.Tests;

/// <summary>
/// Covers TariffResolutionService (the contract the PMS module consumes) against a real
/// relational provider - SQLite ":memory:", the same technique as ReceivablesServiceTests. The
/// tests drive the service directly rather than over HTTP: the "tariffs.read" authorization
/// policy is wired by the integration pass (PermissionCatalog + Program.cs), which this module
/// does not own.
/// </summary>
public sealed class TariffResolutionServiceTests
{
    private const string UnitCode = "HTL-RESOL";

    private const string OtherUnitCode = "HTL-RESOL2";

    private const string CustomerCode = "CLI-RESOL";

    [Fact]
    public async Task Resolves_the_default_plan_period_including_both_boundary_nights()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = await CreateDbContextAsync(connection);
        var service = new TariffResolutionService(dbContext);

        var plan = await SeedAsync(dbContext, defaultPlan: true);
        await SeedPeriodAsync(dbContext, plan, "DBL", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), 12_500m);

        // Codes arrive un-normalized from callers; the night hits both inclusive bounds.
        foreach (var night in new[] { new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 15), new DateOnly(2026, 6, 30) })
        {
            var resolved = await service.ResolveAsync(
                " htl-resol ", " dbl ", night, customerCode: null, CancellationToken.None);

            Assert.True(resolved.Succeeded, resolved.Error);
            Assert.Equal(12_500m, resolved.Value!.Amount);
            Assert.Equal(plan.Code, resolved.Value.RatePlanCode);
            Assert.Null(resolved.Value.ConventionCustomerCode);
            Assert.Null(resolved.Value.DiscountPercent);
        }
    }

    [Fact]
    public async Task Applies_the_customer_convention_plan_and_rounds_the_discount_away_from_zero()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = await CreateDbContextAsync(connection);
        var service = new TariffResolutionService(dbContext);

        var defaultPlan = await SeedAsync(dbContext, defaultPlan: true);
        await SeedPeriodAsync(dbContext, defaultPlan, "DBL", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 20_000m);

        // The convention selects a different, non-default plan of the same unit.
        var conventionPlan = new RatePlan("PLAN-CONV", "Plan conventionne", UnitCode);
        dbContext.Add(conventionPlan);
        await dbContext.SaveChangesAsync();

        // 250.25 with a 10% discount is 225.225: away-from-zero money rounding charges 225.23
        // (banker's rounding would give 225.22, which is the wrong convention for money here).
        await SeedPeriodAsync(dbContext, conventionPlan, "DBL", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 250.25m);

        dbContext.Add(new CustomerConvention(
            CustomerCode, "PLAN-CONV", 10m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        await dbContext.SaveChangesAsync();

        var resolved = await service.ResolveAsync(
            UnitCode, "DBL", new DateOnly(2026, 7, 14), CustomerCode, CancellationToken.None);

        Assert.True(resolved.Succeeded, resolved.Error);
        Assert.Equal(225.23m, resolved.Value!.Amount);
        Assert.Equal("PLAN-CONV", resolved.Value.RatePlanCode);
        Assert.Equal(CustomerCode, resolved.Value.ConventionCustomerCode);
        Assert.Equal(10m, resolved.Value.DiscountPercent);

        // Outside the convention's validity window the default plan takes over, full price.
        var outsideValidity = await service.ResolveAsync(
            UnitCode, "DBL", new DateOnly(2027, 1, 1), CustomerCode, CancellationToken.None);

        Assert.Equal(ApplicationErrorType.NotFound, outsideValidity.ErrorType);

        // (2027 has no period at all -> NotFound; inside 2026 but for a customer without a
        // convention, the default plan resolves at its own price.)
        var noConvention = await service.ResolveAsync(
            UnitCode, "DBL", new DateOnly(2026, 7, 14), "SOMEBODY-ELSE", CancellationToken.None);

        Assert.True(noConvention.Succeeded, noConvention.Error);
        Assert.Equal(20_000m, noConvention.Value!.Amount);
        Assert.Equal(defaultPlan.Code, noConvention.Value.RatePlanCode);
        Assert.Null(noConvention.Value.ConventionCustomerCode);
    }

    [Fact]
    public async Task A_convention_without_discount_uses_its_plan_at_full_price()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = await CreateDbContextAsync(connection);
        var service = new TariffResolutionService(dbContext);

        var defaultPlan = await SeedAsync(dbContext, defaultPlan: true);
        await SeedPeriodAsync(dbContext, defaultPlan, "DBL", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 20_000m);

        var conventionPlan = new RatePlan("PLAN-CONV", "Plan conventionne", UnitCode);
        dbContext.Add(conventionPlan);
        await dbContext.SaveChangesAsync();
        await SeedPeriodAsync(dbContext, conventionPlan, "DBL", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 15_000m);

        dbContext.Add(new CustomerConvention(
            CustomerCode, "PLAN-CONV", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        await dbContext.SaveChangesAsync();

        var resolved = await service.ResolveAsync(
            UnitCode, "DBL", new DateOnly(2026, 3, 1), CustomerCode, CancellationToken.None);

        Assert.True(resolved.Succeeded, resolved.Error);
        Assert.Equal(15_000m, resolved.Value!.Amount);
        Assert.Equal(CustomerCode, resolved.Value.ConventionCustomerCode);
        Assert.Null(resolved.Value.DiscountPercent);
    }

    [Fact]
    public async Task A_convention_bound_to_another_units_plan_falls_back_to_the_default_plan()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = await CreateDbContextAsync(connection);
        var service = new TariffResolutionService(dbContext);

        var defaultPlan = await SeedAsync(dbContext, defaultPlan: true);
        await SeedPeriodAsync(dbContext, defaultPlan, "DBL", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 20_000m);

        // The customer's convention was negotiated on the OTHER unit's plan: it must not price
        // this unit's rooms, so resolution silently falls back to the requested unit's default.
        var otherUnitPlan = new RatePlan("PLAN-OTHER", "Plan autre unite", OtherUnitCode);
        dbContext.Add(otherUnitPlan);
        dbContext.Add(new CustomerConvention(
            CustomerCode, "PLAN-OTHER", 50m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        await dbContext.SaveChangesAsync();

        var resolved = await service.ResolveAsync(
            UnitCode, "DBL", new DateOnly(2026, 3, 1), CustomerCode, CancellationToken.None);

        Assert.True(resolved.Succeeded, resolved.Error);
        Assert.Equal(20_000m, resolved.Value!.Amount);
        Assert.Equal(defaultPlan.Code, resolved.Value.RatePlanCode);
        Assert.Null(resolved.Value.ConventionCustomerCode);
        Assert.Null(resolved.Value.DiscountPercent);
    }

    [Fact]
    public async Task Missing_default_plan_and_coverage_gaps_surface_as_explicit_not_found()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = await CreateDbContextAsync(connection);
        var service = new TariffResolutionService(dbContext);

        // Unit and a NON-default plan only: no default to fall back to.
        var plan = await SeedAsync(dbContext, defaultPlan: false);
        await SeedPeriodAsync(dbContext, plan, "DBL", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), 12_500m);

        var noDefault = await service.ResolveAsync(
            UnitCode, "DBL", new DateOnly(2026, 6, 15), customerCode: null, CancellationToken.None);

        Assert.Equal(ApplicationErrorType.NotFound, noDefault.ErrorType);
        Assert.Contains("default rate plan", noDefault.Error);

        // Promote the plan to default: the covered night resolves, the uncovered ones do not.
        var tracked = await dbContext.Set<RatePlan>().SingleAsync(current => current.Id == plan.Id);
        tracked.SetAsDefault();
        await dbContext.SaveChangesAsync();

        var covered = await service.ResolveAsync(
            UnitCode, "DBL", new DateOnly(2026, 6, 15), customerCode: null, CancellationToken.None);
        Assert.True(covered.Succeeded, covered.Error);

        // The night right after the period's last day is the most common operations error: the
        // message must name the plan, the night and the room type.
        var gap = await service.ResolveAsync(
            UnitCode, "DBL", new DateOnly(2026, 7, 1), customerCode: null, CancellationToken.None);

        Assert.Equal(ApplicationErrorType.NotFound, gap.ErrorType);
        Assert.Contains(plan.Code, gap.Error);
        Assert.Contains("2026-07-01", gap.Error);
        Assert.Contains("DBL", gap.Error);

        // A room type nobody priced yet is a gap too.
        var unknownRoomType = await service.ResolveAsync(
            UnitCode, "SUITE", new DateOnly(2026, 6, 15), customerCode: null, CancellationToken.None);
        Assert.Equal(ApplicationErrorType.NotFound, unknownRoomType.ErrorType);

        // An unknown unit is reported as such, not as a tariff gap.
        var unknownUnit = await service.ResolveAsync(
            "NO-UNIT", "DBL", new DateOnly(2026, 6, 15), customerCode: null, CancellationToken.None);
        Assert.Equal(ApplicationErrorType.NotFound, unknownUnit.ErrorType);
        Assert.Contains("Hotel unit", unknownUnit.Error);
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

    /// <summary>
    /// Seeds the two hotel units, the conventioned customer and one plan on the main unit
    /// (default or not, per the flag), directly through the DbContext - resolution is read-only
    /// and does not need the management service's workflows.
    /// </summary>
    private static async Task<RatePlan> SeedAsync(RaqmiDbContext dbContext, bool defaultPlan)
    {
        dbContext.Add(new HotelUnit(UnitCode, "Hotel Resolution", HotelUnitType.Hotel));
        dbContext.Add(new HotelUnit(OtherUnitCode, "Hotel Resolution Bis", HotelUnitType.Hotel));
        dbContext.Add(new Customer(CustomerCode, "Client Resolution", CustomerType.Company));

        var plan = new RatePlan("PLAN-STD", "Plan standard", UnitCode, defaultPlan);
        dbContext.Add(plan);

        await dbContext.SaveChangesAsync();

        return plan;
    }

    private static async Task SeedPeriodAsync(
        RaqmiDbContext dbContext,
        RatePlan plan,
        string roomTypeCode,
        DateOnly fromDate,
        DateOnly toDate,
        decimal nightlyAmount)
    {
        dbContext.Add(new RatePeriod(plan.Id, roomTypeCode, fromDate, toDate, nightlyAmount));
        await dbContext.SaveChangesAsync();
    }
}
