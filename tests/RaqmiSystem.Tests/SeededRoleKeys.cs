using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Security;

namespace RaqmiSystem.Tests;

/// <summary>
/// Les clés effectives de chaque rôle système, telles que <see cref="SecuritySeeder"/> les
/// sème réellement (SQLite en mémoire). Les tests de projection par rôle (accueil, barre
/// latérale) lisent ici plutôt que de recopier les listes du seeder : toute dérive du seeder
/// ou du registre les casse, c'est voulu.
/// </summary>
internal static class SeededRoleKeys
{
    public static async Task<Dictionary<string, IReadOnlySet<string>>> LoadAsync()
    {
        // La connexion vit aussi longtemps que le contexte : la disposer ferme la base ":memory:".
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = new RaqmiDbContext(
            new DbContextOptionsBuilder<RaqmiDbContext>()
                .UseSqlite(connection)
                .Options);

        await dbContext.Database.EnsureCreatedAsync();

        var seeder = new SecuritySeeder(dbContext, new Pbkdf2PasswordHasher());
        await seeder.SeedAsync(CancellationToken.None);

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
