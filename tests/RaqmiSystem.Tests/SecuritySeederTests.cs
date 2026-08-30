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
}
