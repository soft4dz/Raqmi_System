using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Navigation;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Security;

namespace RaqmiSystem.Tests;

/// <summary>
/// Table de vérité de l'accueil par rôle seedé : les clés effectives de chaque rôle système
/// (SecuritySeeder sur SQLite en mémoire) composent exactement les files du tableau § 3.7 de la
/// spécification. Toute dérive du seeder ou du registre casse ce test : c'est voulu.
/// </summary>
public sealed class HomeComposerSeededRolesTests
{
    [Theory]
    [InlineData(
        RoleCatalog.Direction, false,
        "dec-backlog,dec-rejected,backup,aging-90",
        "approvals,dec-po,counts-draft,po-approve,dec-revenue,po-pay,receipts-draft,po-receive,haccp,absences,payroll,revenue-yesterday,receipts-today",
        "low-stock,workstations",
        16)]
    [InlineData(
        RoleCatalog.ExploitationControl, false,
        "dec-backlog,dec-rejected,aging-90",
        "approvals,dec-revenue,dec-po,counts-draft,po-approve,po-receive,haccp,po-pay,receipts-draft,revenue-yesterday,receipts-today",
        "low-stock",
        12)]
    [InlineData(
        RoleCatalog.UnitManager, true,
        "arrivals-late,departures-late,closing-unit,dec-backlog,dec-rejected",
        "arrivals,arrivals-unassigned,departures,departures-balance,hk-dirty,hk-inspect,approvals,revenue-draft,po-receive,haccp,dec-revenue,dec-po,counts-draft,po-approve,revenue-yesterday,events-today",
        "hk-ooo,low-stock",
        15)]
    [InlineData(
        RoleCatalog.Cashier, true,
        "arrivals-late,departures-late,closing-unit",
        "arrivals,arrivals-unassigned,departures,departures-balance,hk-dirty,revenue-draft,po-pay,receipts-draft,hk-inspect,receipts-today",
        "hk-ooo",
        9)]
    [InlineData(
        RoleCatalog.HrManager, false,
        "",
        "absences,payroll",
        "",
        2)]
    [InlineData(
        RoleCatalog.Reader, true,
        "arrivals-late,departures-late,closing-unit,dec-backlog,dec-rejected,aging-90",
        "arrivals,arrivals-unassigned,departures,departures-balance,hk-dirty,hk-inspect,dec-revenue,dec-po,revenue-draft,counts-draft,po-approve,po-receive,haccp,revenue-yesterday,events-today",
        "hk-ooo,low-stock",
        15)]
    [InlineData(
        RoleCatalog.SystemAdministrator, false,
        "dec-backlog,dec-rejected,backup,aging-90",
        "approvals,dec-revenue,dec-po,po-pay,receipts-draft,counts-draft,po-approve,po-receive,haccp,absences,payroll,revenue-yesterday,receipts-today",
        "low-stock,workstations",
        16)]
    [InlineData(
        RoleCatalog.SystemAdministrator, true,
        "arrivals-late,departures-late,closing-unit,dec-backlog,dec-rejected,backup,aging-90",
        "arrivals,arrivals-unassigned,departures,departures-balance,hk-dirty,hk-inspect,approvals,dec-revenue,dec-po,revenue-draft,po-pay,receipts-draft,counts-draft,po-approve,po-receive,haccp,absences,payroll,revenue-yesterday,receipts-today,events-today",
        "hk-ooo,low-stock,workstations",
        23)]
    public async Task Each_seeded_role_composes_exactly_the_queues_of_the_specification(
        string roleName,
        bool hasStationUnit,
        string expectedOverdue,
        string expectedToday,
        string expectedWatch,
        int expectedSourceCount)
    {
        await using var dbContext = await CreateSeededContextAsync();
        var keys = (await LoadRolesWithKeysAsync(dbContext))[roleName];

        var layout = HomeComposer.Compose(keys, hasStationUnit);

        Assert.Equal(Split(expectedOverdue), Ids(layout.Band(HomeBand.Overdue)));
        Assert.Equal(Split(expectedToday), Ids(layout.Band(HomeBand.Today)));
        Assert.Equal(Split(expectedWatch), Ids(layout.Band(HomeBand.Watch)));
        Assert.Equal(expectedSourceCount, layout.Sources.Count);
    }

    [Fact]
    public async Task Modes_follow_the_seeded_action_keys()
    {
        await using var dbContext = await CreateSeededContextAsync();
        var roles = await LoadRolesWithKeysAsync(dbContext);

        // Direction : décide et approuve, mais ne saisit ni ne clôture.
        var direction = HomeComposer.Compose(roles[RoleCatalog.Direction], hasStationUnit: false);
        Assert.Equal(HomeMode.Act, Mode(direction, "approvals"));
        Assert.Equal(HomeMode.Act, Mode(direction, "dec-po"));
        Assert.Equal(HomeMode.Watch, Mode(direction, "dec-revenue"));
        Assert.Equal(HomeMode.Watch, Mode(direction, "dec-backlog"));
        Assert.Equal(HomeMode.Watch, Mode(direction, "backup"));
        Assert.Equal(HomeMode.Information, Mode(direction, "aging-90"));
        Assert.True(direction.ShowUnitMissingBanner);
        Assert.False(direction.ShowBusinessDate);

        // Directeur d'unité : la carte OP porte le chiffre DEC et ouvre le cockpit (pas de treasury.read).
        var manager = HomeComposer.Compose(roles[RoleCatalog.UnitManager], hasStationUnit: true);
        var po = manager.Slots.Single(slot => slot.Queue.Id == "dec-po");
        Assert.Equal(HomeMode.Watch, po.Mode);
        Assert.Equal(20, po.TargetTab);
        Assert.Equal(5, manager.Slots.Single(slot => slot.Queue.Id == "closing-unit").TargetTab);
        Assert.True(manager.ShowBusinessDate);
        Assert.False(manager.ShowUnitMissingBanner);

        // Caisse : la clôture est suivie, et l'onglet Clôture fermé se replie sur le PMS.
        var cashier = HomeComposer.Compose(roles[RoleCatalog.Cashier], hasStationUnit: true);
        var closing = cashier.Slots.Single(slot => slot.Queue.Id == "closing-unit");
        Assert.Equal(HomeMode.Watch, closing.Mode);
        Assert.Equal(30, closing.TargetTab);
        Assert.False(closing.TargetLocked);
        Assert.Equal(HomeScope.Unit, cashier.Slots.Single(slot => slot.Queue.Id == "receipts-draft").Scope);
        Assert.DoesNotContain(cashier.Slots, slot => slot.Queue.Id == "approvals");

        // RH : l'accueil le plus court, et utile — aucun encart, aucune ligne d'unité.
        var hr = HomeComposer.Compose(roles[RoleCatalog.HrManager], hasStationUnit: false);
        Assert.All(hr.Slots, slot => Assert.Equal(HomeMode.Act, slot.Mode));
        Assert.False(hr.ShowUnitLine);
        Assert.False(hr.ShowUnitMissingBanner);
        Assert.Equal(HomeEmptyReason.NoQueues, hr.Band(HomeBand.Overdue).EmptyReason);
        Assert.Equal(HomeEmptyReason.NoQueues, hr.Band(HomeBand.Watch).EmptyReason);

        // Lecture seule : aucun verbe nulle part.
        var reader = HomeComposer.Compose(roles[RoleCatalog.Reader], hasStationUnit: true);
        Assert.True(reader.WatchOnly);
        Assert.DoesNotContain(reader.Slots, slot => slot.Mode == HomeMode.Act);
        Assert.Equal(5, reader.Slots.Single(slot => slot.Queue.Id == "closing-unit").TargetTab);
        Assert.Equal(20, reader.Slots.Single(slot => slot.Queue.Id == "dec-po").TargetTab);

        // Aucun rôle seedé ne laisse une cible verrouillée : chaque carte ouvre un écran.
        foreach (var role in roles)
        {
            var layout = HomeComposer.Compose(role.Value, hasStationUnit: true);
            Assert.All(layout.Slots, slot => Assert.False(slot.TargetLocked, $"{role.Key} : {slot.Queue.Id} verrouillée."));
        }
    }

    private static HomeMode Mode(HomeLayout layout, string id) => layout.Slots.Single(slot => slot.Queue.Id == id).Mode;

    private static string[] Ids(HomeSection section) => section.Slots.Select(slot => slot.Queue.Id).ToArray();

    private static string[] Split(string ids) =>
        string.IsNullOrWhiteSpace(ids) ? [] : ids.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static async Task<RaqmiDbContext> CreateSeededContextAsync()
    {
        // La connexion vit aussi longtemps que le contexte : le disposer ferme la base ":memory:".
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var dbContext = new RaqmiDbContext(
            new DbContextOptionsBuilder<RaqmiDbContext>()
                .UseSqlite(connection)
                .Options);

        await dbContext.Database.EnsureCreatedAsync();

        var seeder = new SecuritySeeder(dbContext, new Pbkdf2PasswordHasher());
        await seeder.SeedAsync(CancellationToken.None);

        return dbContext;
    }

    private static async Task<Dictionary<string, IReadOnlySet<string>>> LoadRolesWithKeysAsync(RaqmiDbContext dbContext)
    {
        var roles = await dbContext.Roles
            .AsNoTracking()
            .Include(role => role.Permissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .ToArrayAsync();

        return roles.ToDictionary(
            role => role.Name,
            role => (IReadOnlySet<string>)role.Permissions
                .Select(rolePermission => rolePermission.Permission.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            StringComparer.Ordinal);
    }
}
