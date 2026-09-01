using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Security;

namespace RaqmiSystem.Tests;

/// <summary>
/// Behavior of SecuritySeeder.SeedPermissionsAsync on an ALREADY-SEEDED database: missing
/// catalog keys are inserted, and the display fields (name, category, description) of existing
/// permissions are re-aligned on PermissionCatalog - a wording fixed in the catalog must reach
/// installations seeded before the fix, not only fresh databases. The permission KEY, being the
/// identity role grants and policies reference, is never touched, and permissions outside the
/// catalog are left alone.
/// </summary>
public sealed class SecuritySeederTests
{
    [Fact]
    public async Task Seeding_updates_the_display_fields_of_an_existing_permission_to_the_catalog()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var _ = connection;

        await using var dbContext = new RaqmiDbContext(
            new DbContextOptionsBuilder<RaqmiDbContext>()
                .UseSqlite(connection)
                .Options);

        await dbContext.Database.EnsureCreatedAsync();

        // A database seeded BEFORE a catalog wording fix: the key exists, the labels are stale.
        dbContext.Permissions.Add(new Permission(
            "lodging.read",
            "Ancien libelle errone",
            "ancienne-categorie",
            "Ancienne description erronee."));

        // A permission created outside the catalog (e.g. by hand): the seeder must not touch it.
        dbContext.Permissions.Add(new Permission(
            "custom.local",
            "Permission locale",
            "locale",
            "Permission hors catalogue, propriete de l'installation."));

        await dbContext.SaveChangesAsync();

        var seeder = new SecuritySeeder(dbContext, new Pbkdf2PasswordHasher());
        await seeder.SeedAsync(CancellationToken.None);

        var catalogDefinition = PermissionCatalog.All.Single(definition => definition.Key == "lodging.read");
        var updated = await dbContext.Permissions.SingleAsync(permission => permission.Key == "lodging.read");

        // The stale labels now match the catalog; the key was never rewritten.
        Assert.Equal(catalogDefinition.Name, updated.Name);
        Assert.Equal(catalogDefinition.Category, updated.Category);
        Assert.Equal(catalogDefinition.Description, updated.Description);
        Assert.NotNull(updated.UpdatedAt);

        var untouched = await dbContext.Permissions.SingleAsync(permission => permission.Key == "custom.local");
        Assert.Equal("Permission locale", untouched.Name);
        Assert.Null(untouched.UpdatedAt);

        // Every catalog key is present after seeding.
        var seededKeys = (await dbContext.Permissions.Select(permission => permission.Key).ToArrayAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.All(PermissionCatalog.All, definition => Assert.Contains(definition.Key, seededKeys));

        // Idempotence: a second run leaves an already-aligned permission strictly untouched.
        var firstUpdateStamp = updated.UpdatedAt;
        await seeder.SeedAsync(CancellationToken.None);

        var afterSecondRun = await dbContext.Permissions.SingleAsync(permission => permission.Key == "lodging.read");
        Assert.Equal(firstUpdateStamp, afterSecondRun.UpdatedAt);
    }

    /// <summary>
    /// RoleCatalog.ApprovalDeciderRoles is what the domain lets an approval step require. It is
    /// only meaningful if it names EXACTLY the roles the seeder grants approvals.decide to: a
    /// role listed here without the permission would make its steps undecidable (its holders are
    /// refused by the authorization policy, and every other decider fails the step's role check),
    /// and a role holding the permission without being listed would be a decider the circuit
    /// designer is never allowed to call upon. The equality is asserted in BOTH directions so the
    /// two lists cannot drift apart in either.
    /// </summary>
    [Fact]
    public async Task The_approval_decider_roles_are_exactly_the_seeded_roles_holding_approvals_decide()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var _ = connection;

        await using var dbContext = new RaqmiDbContext(
            new DbContextOptionsBuilder<RaqmiDbContext>()
                .UseSqlite(connection)
                .Options);

        await dbContext.Database.EnsureCreatedAsync();

        var seeder = new SecuritySeeder(dbContext, new Pbkdf2PasswordHasher());
        await seeder.SeedAsync(CancellationToken.None);

        var rolesHoldingDecide = await dbContext.Roles
            .Include(role => role.Permissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .Where(role => role.Permissions.Any(rolePermission =>
                rolePermission.Permission.Key == PermissionCatalog.ApprovalsDecide))
            .Select(role => role.Name)
            .ToArrayAsync();

        Assert.Equal(
            RoleCatalog.ApprovalDeciderRoles.OrderBy(role => role, StringComparer.Ordinal).ToArray(),
            rolesHoldingDecide.OrderBy(role => role, StringComparer.Ordinal).ToArray());

        // The two roles the finding is about: they are seeded, they can READ the approvals, and
        // they must never be proposable as the required role of a step.
        Assert.DoesNotContain(RoleCatalog.Cashier, RoleCatalog.ApprovalDeciderRoles);
        Assert.DoesNotContain(RoleCatalog.Reader, RoleCatalog.ApprovalDeciderRoles);
    }

    // ------------------------------------------------------------------------------------------
    // Lot 2.1 - registre domaine.ressource.action : migration des roles SYSTEME par le seeder.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Equivalence stricte, role par role : un role systeme detient une cle cible si et
    /// seulement si l'une de ses cles historiques la couvre. Ni extension (une cible dont
    /// aucune cle historique detenue n'est couverte) ni perte (une cible couverte mais absente).
    /// Et il garde toutes ses cles historiques - le client WPF les evalue encore.
    /// </summary>
    [Fact]
    public async Task System_roles_hold_a_target_key_exactly_when_they_hold_a_legacy_key_covering_it()
    {
        await using var dbContext = await CreateSeededContextAsync();

        var roles = await LoadRolesWithKeysAsync(dbContext);

        var systemRoles = new[]
        {
            RoleCatalog.Direction,
            RoleCatalog.ExploitationControl,
            RoleCatalog.UnitManager,
            RoleCatalog.Cashier,
            RoleCatalog.HrManager,
            RoleCatalog.Reader
        };

        foreach (var roleName in systemRoles)
        {
            var held = roles[roleName];

            foreach (var target in PermissionRegistry.All.Where(candidate => candidate.LegacyKeys.Count > 0))
            {
                var coveredByHeldLegacyKey = target.LegacyKeys.Any(held.Contains);

                Assert.True(
                    held.Contains(target.Key) == coveredByHeldLegacyKey,
                    $"{roleName} : {target.Key} devrait etre {(coveredByHeldLegacyKey ? "accordee" : "absente")} " +
                    $"(cles historiques couvrantes : {string.Join(", ", target.LegacyKeys)}).");
            }

            // Un role systeme migre detient AU MOINS une cle historique et une cle cible : la
            // migration a bien eu lieu, et n'a rien retire.
            Assert.Contains(held, PermissionRegistry.IsLegacyKey);
            Assert.Contains(held, PermissionRegistry.IsTargetKey);
        }

        // L'administrateur systeme detient tout le catalogue, cibles comprises.
        var administrator = roles[RoleCatalog.SystemAdministrator];
        Assert.All(PermissionCatalog.All, definition => Assert.Contains(definition.Key, administrator));
    }

    /// <summary>
    /// La regle ApprovalDeciderRoles tenue plus haut sur approvals.decide tient aussi sur sa cle
    /// cible workflow.request.decide : les deux listes de roles sont les memes, sinon un circuit
    /// serait decidable par l'une et pas par l'autre selon la cle que la route exige.
    /// </summary>
    [Fact]
    public async Task The_approval_decider_roles_are_exactly_the_seeded_roles_holding_workflow_request_decide()
    {
        await using var dbContext = await CreateSeededContextAsync();

        var roles = await LoadRolesWithKeysAsync(dbContext);

        var holdingTarget = roles
            .Where(pair => pair.Value.Contains(PermissionCatalog.WorkflowRequestDecide))
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var holdingLegacy = roles
            .Where(pair => pair.Value.Contains(PermissionCatalog.ApprovalsDecide))
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(RoleCatalog.ApprovalDeciderRoles.Order(StringComparer.Ordinal).ToArray(), holdingTarget);
        Assert.Equal(holdingLegacy, holdingTarget);
    }

    /// <summary>
    /// Une base deja seedee AVANT le registre : les permissions historiques existent, un role
    /// systeme n'a que ses cles historiques. Rejouer le seeder insere les cles cibles (lignes de
    /// security.permissions, aucune migration de schema) et complete le role - puis un second
    /// passage ne change plus rien.
    /// </summary>
    [Fact]
    public async Task Seeding_an_installation_seeded_before_the_registry_adds_the_target_keys_idempotently()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var _ = connection;

        await using var dbContext = new RaqmiDbContext(
            new DbContextOptionsBuilder<RaqmiDbContext>()
                .UseSqlite(connection)
                .Options);

        await dbContext.Database.EnsureCreatedAsync();

        // L'etat "avant" : les 83 definitions historiques, et le caissier avec ses seules cles
        // historiques de comptoir.
        var legacyPermissions = PermissionCatalog.Legacy
            .Select(definition => new Permission(definition.Key, definition.Name, definition.Category, definition.Description))
            .ToDictionary(permission => permission.Key, StringComparer.Ordinal);

        dbContext.Permissions.AddRange(legacyPermissions.Values);

        var cashier = new Role(RoleCatalog.Cashier, "Caissier", "Etat d'avant le registre.", isSystem: true);
        cashier.GrantPermission(legacyPermissions[PermissionCatalog.LodgingRead], DateTimeOffset.UtcNow);
        cashier.GrantPermission(legacyPermissions[PermissionCatalog.LodgingCheckin], DateTimeOffset.UtcNow);
        dbContext.Roles.Add(cashier);

        await dbContext.SaveChangesAsync();

        Assert.False(await dbContext.Permissions.AnyAsync(permission => permission.Key == PermissionCatalog.LodgingCheckinExecute));

        var seeder = new SecuritySeeder(dbContext, new Pbkdf2PasswordHasher());
        await seeder.SeedAsync(CancellationToken.None);

        var permissionCount = await dbContext.Permissions.CountAsync();
        Assert.Equal(PermissionCatalog.All.Count, permissionCount);

        var cashierKeys = (await LoadRolesWithKeysAsync(dbContext))[RoleCatalog.Cashier];

        // Les cles historiques du caissier sont toujours la, et chaque cle cible couverte par
        // lodging.checkin est arrivee - y compris celles que la cle fine historique correspondante
        // couvre aussi (checkout, room_move).
        Assert.Contains(PermissionCatalog.LodgingRead, cashierKeys);
        Assert.Contains(PermissionCatalog.LodgingCheckin, cashierKeys);
        Assert.Contains(PermissionCatalog.LodgingFrontOfficeRead, cashierKeys);

        foreach (var targetKey in PermissionRegistry.TargetKeysCoveredBy(PermissionCatalog.LodgingCheckin))
        {
            Assert.Contains(targetKey, cashierKeys);
        }

        var grantCountAfterFirstRun = await dbContext.Set<RolePermission>().CountAsync();

        await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(permissionCount, await dbContext.Permissions.CountAsync());
        Assert.Equal(grantCountAfterFirstRun, await dbContext.Set<RolePermission>().CountAsync());
    }

    /// <summary>
    /// Le seeder ne touche jamais un role PERSONNALISE : il est signale par le rapport de
    /// migration et migre par un administrateur, jamais en silence au demarrage.
    /// </summary>
    [Fact]
    public async Task Custom_roles_are_left_untouched_by_the_seeder()
    {
        await using var dbContext = await CreateSeededContextAsync();

        var usersWrite = await dbContext.Permissions.SingleAsync(permission => permission.Key == PermissionCatalog.UsersWrite);

        var custom = new Role("custom.administration", "Administration locale", "Role personnalise de l'installation.");
        custom.GrantPermission(usersWrite, DateTimeOffset.UtcNow);
        dbContext.Roles.Add(custom);
        await dbContext.SaveChangesAsync();

        var seeder = new SecuritySeeder(dbContext, new Pbkdf2PasswordHasher());
        await seeder.SeedAsync(CancellationToken.None);

        var customKeys = (await LoadRolesWithKeysAsync(dbContext))["custom.administration"];

        Assert.Equal(new[] { PermissionCatalog.UsersWrite }, customKeys.Order(StringComparer.Ordinal).ToArray());

        var report = await new PermissionMigrationReportService(dbContext).BuildAsync(CancellationToken.None);

        var row = Assert.Single(report.Roles);
        Assert.Equal("custom.administration", row.Name);
        Assert.False(row.IsMigrated);
        Assert.Equal(
            PermissionRegistry.TargetKeysCoveredBy(PermissionCatalog.UsersWrite).Order(StringComparer.Ordinal).ToArray(),
            row.TargetKeysMissing);
    }

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

    private static async Task<Dictionary<string, HashSet<string>>> LoadRolesWithKeysAsync(RaqmiDbContext dbContext)
    {
        var roles = await dbContext.Roles
            .AsNoTracking()
            .Include(role => role.Permissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .ToArrayAsync();

        return roles.ToDictionary(
            role => role.Name,
            role => role.Permissions.Select(rolePermission => rolePermission.Permission.Key).ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }
}
