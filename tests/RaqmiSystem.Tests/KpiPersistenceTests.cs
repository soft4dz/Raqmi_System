using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Domain.Kpi;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// La persistance des trois tables du module KPI, contre le modele reel.
///
/// Ce que ces tests protegent vraiment, ce sont les deux INDEX UNIQUES. Sans eux, deux poses
/// concurrentes de la meme periode creeraient deux instantanes et deux regles de seuils
/// pourraient coexister sur le meme indicateur : dans les deux cas, la valeur retenue
/// dependrait de l'ordre de lecture, ce qu'aucun utilisateur ne pourrait comprendre ni corriger.
/// Le cas du perimetre GROUPE est teste explicitement, parce que c'est celui qu'un index pose
/// sur un code d'unite nullable laisserait passer - PostgreSQL comme SQLite considerent deux
/// NULL comme distincts.
/// </summary>
public sealed class KpiPersistenceTests : IDisposable
{
    private static readonly DateOnly PeriodStart = new(2026, 1, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 1, 31);
    private static readonly DateTimeOffset CalculatedAt = new(2026, 2, 1, 6, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection connection = new("DataSource=:memory:");

    public KpiPersistenceTests()
    {
        connection.Open();
    }

    /// <summary>
    /// Une base SQLite en memoire batie sur le MODELE REEL. Les entites du module KPI y entrent
    /// par ApplyConfigurationsFromAssembly, comme en production : ces tests exercent donc les
    /// configurations livrees, pas une copie de circonstance.
    /// </summary>
    private RaqmiDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RaqmiDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new RaqmiDbContext(options);
        context.Database.EnsureCreated();

        return context;
    }

    private static KpiSnapshot Snapshot(string? hotelUnitCode = null, decimal? value = 1_000m)
    {
        return new KpiSnapshot(
            KpiCodes.RevenueTotal,
            hotelUnitCode,
            PeriodStart,
            PeriodEnd,
            KpiPeriodGranularity.Month,
            value,
            numerator: value,
            denominator: null,
            KpiQuality.Valid,
            formulaVersion: 1,
            calculatedAt: CalculatedAt);
    }

    [Fact]
    public async Task A_threshold_round_trips()
    {
        await using var context = CreateContext();
        context.Set<HotelUnit>().Add(new HotelUnit("HOTEL-A", "Hotel A", HotelUnitType.Hotel));
        await context.SaveChangesAsync();

        context.Set<KpiThreshold>().Add(
            new KpiThreshold(KpiCodes.OccupancyRate, "HOTEL-A", 65m, 40m, 70m, "Direction", "note"));

        await context.SaveChangesAsync();

        await using var reader = CreateContext();
        var stored = await reader.Set<KpiThreshold>().SingleAsync();

        Assert.Equal("HOTEL-A", stored.HotelUnitCode);
        Assert.Equal("HOTEL-A", stored.ScopeKey);
        Assert.Equal(65m, stored.FavorableThreshold);
        Assert.Equal(70m, stored.TargetValue);
        Assert.Equal("Direction", stored.OwnerRole);
    }

    [Fact]
    public async Task A_group_threshold_stores_the_group_scope_key()
    {
        await using var context = CreateContext();
        context.Set<KpiThreshold>().Add(new KpiThreshold(KpiCodes.OccupancyRate, null, 65m, 40m, null, null));
        await context.SaveChangesAsync();

        var stored = await CreateContext().Set<KpiThreshold>().SingleAsync();

        Assert.Null(stored.HotelUnitCode);
        Assert.Equal(KpiScopeKey.Group, stored.ScopeKey);
    }

    [Fact]
    public async Task Two_group_rules_for_the_same_indicator_collide()
    {
        await using var context = CreateContext();
        context.Set<KpiThreshold>().Add(new KpiThreshold(KpiCodes.OccupancyRate, null, 65m, 40m, null, null));
        await context.SaveChangesAsync();

        context.Set<KpiThreshold>().Add(new KpiThreshold(KpiCodes.OccupancyRate, null, 55m, 30m, null, null));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task An_account_prefix_can_only_belong_to_one_group()
    {
        await using var context = CreateContext();
        context.Set<KpiAccountMapping>().Add(
            new KpiAccountMapping("60", KpiAccountGroup.DepartmentalExpense, "Achats consommes"));

        await context.SaveChangesAsync();

        context.Set<KpiAccountMapping>().Add(
            new KpiAccountMapping("60", KpiAccountGroup.UndistributedExpense, "Doublon"));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task A_snapshot_keeps_its_missing_value_as_a_null()
    {
        // Toute la difference entre "pas de valeur" et "valeur nulle" doit survivre au voyage.
        await using var context = CreateContext();

        context.Set<KpiSnapshot>().Add(new KpiSnapshot(
            KpiCodes.Adr, null, PeriodStart, PeriodEnd, KpiPeriodGranularity.Month,
            value: null, numerator: 0m, denominator: 0m,
            KpiQuality.MissingData, formulaVersion: 1, calculatedAt: CalculatedAt));

        await context.SaveChangesAsync();

        var stored = await CreateContext().Set<KpiSnapshot>().SingleAsync();

        Assert.Null(stored.Value);
        Assert.Equal(KpiQuality.MissingData, stored.Quality);
        Assert.Equal(KpiSnapshotStatus.Provisional, stored.Status);
    }

    [Fact]
    public async Task Re_posting_the_same_group_period_collides_instead_of_duplicating()
    {
        await using var context = CreateContext();
        context.Set<KpiSnapshot>().Add(Snapshot());
        await context.SaveChangesAsync();

        context.Set<KpiSnapshot>().Add(Snapshot());

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task The_same_period_coexists_for_the_group_and_for_a_unit()
    {
        await using var context = CreateContext();
        context.Set<HotelUnit>().Add(new HotelUnit("HOTEL-A", "Hotel A", HotelUnitType.Hotel));
        await context.SaveChangesAsync();

        context.Set<KpiSnapshot>().Add(Snapshot());
        context.Set<KpiSnapshot>().Add(Snapshot("HOTEL-A", 400m));

        await context.SaveChangesAsync();

        Assert.Equal(2, await CreateContext().Set<KpiSnapshot>().CountAsync());
    }

    [Fact]
    public async Task A_closed_snapshot_keeps_its_closing_trace_and_still_refuses_a_recalculation()
    {
        await using var context = CreateContext();

        var snapshot = Snapshot();
        snapshot.Close("controleur", CalculatedAt);

        context.Set<KpiSnapshot>().Add(snapshot);
        await context.SaveChangesAsync();

        var stored = await CreateContext().Set<KpiSnapshot>().SingleAsync();

        Assert.True(stored.IsClosed);
        Assert.Equal("controleur", stored.ClosedBy);
        Assert.Equal(CalculatedAt, stored.ClosedAt);

        Assert.Throws<InvalidOperationException>(() =>
            stored.Refresh(2_000m, 2_000m, null, KpiQuality.Valid, 1, CalculatedAt));
    }

    public void Dispose()
    {
        connection.Dispose();
    }
}
