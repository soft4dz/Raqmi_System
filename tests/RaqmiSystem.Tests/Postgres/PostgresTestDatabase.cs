using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests.Postgres;

/// <summary>
/// Une base PostgreSQL creee pour les tests et detruite avec elle. La fixture en cree une par
/// execution (migree jusqu'au bout) et les tests de migration en demandent d'autres, vierges,
/// pour rejouer les migrations depuis zero ou depuis N-1 sans toucher a la base commune.
/// </summary>
public sealed class PostgresTestDatabase : IAsyncDisposable
{
    private readonly PostgresDatabaseFixture _owner;

    private int _dropped;

    internal PostgresTestDatabase(PostgresDatabaseFixture owner, string name, string connectionString)
    {
        _owner = owner;
        Name = name;
        ConnectionString = connectionString;
    }

    public string Name { get; }

    /// <summary>
    /// Chaine construite par <see cref="ConnectionStringFactory"/> : la meme forme (pooling,
    /// delais, details d'erreur masques) que celle de l'API en production.
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// Un DbContext Npgsql sur cette base, avec les options par defaut. Chaque appel ouvre son
    /// propre suivi de changements et sa propre connexion (poolee) : c'est ce qui permet a deux
    /// contextes d'etre en vol au meme moment dans les tests de concurrence.
    /// </summary>
    public RaqmiDbContext CreateDbContext()
    {
        return new RaqmiDbContext(BuildOptions(ignorePendingModelChangesWarning: false));
    }

    /// <summary>
    /// Applique les migrations jusqu'a <paramref name="targetMigration"/> (la derniere quand
    /// null), via le meme <see cref="IMigrator"/> que <c>dotnet ef database update</c>.
    ///
    /// L'avertissement PendingModelChangesWarning est ignore ICI, et seulement ici : depuis
    /// EF Core 9, <c>Migrate()</c> vers la derniere migration leve une exception si le modele a
    /// derive du snapshot. Cette derive est bien une erreur a bloquer, mais c'est le role du test
    /// dedie (garde anti-derive de <c>PostgresMigrationTests</c>), qui la nomme et explique
    /// comment la corriger. Laisser la fixture lever a sa place noierait le message dans un echec
    /// d'initialisation de collection, sans test identifiable dans le rapport.
    /// </summary>
    public async Task MigrateAsync(string? targetMigration = null, CancellationToken cancellationToken = default)
    {
        await using var dbContext = new RaqmiDbContext(BuildOptions(ignorePendingModelChangesWarning: true));

        await dbContext.GetService<IMigrator>().MigrateAsync(targetMigration, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Supprime la base. Idempotent : la fixture rappelle cette methode en fin de collection pour
    /// toute base qu'un test n'aurait pas liberee (echec avant le <c>await using</c>, par exemple).
    /// </summary>
    public async Task DropAsync()
    {
        if (Interlocked.Exchange(ref _dropped, 1) == 1)
        {
            return;
        }

        await _owner.DropDatabaseAsync(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DropAsync();
    }

    private DbContextOptions<RaqmiDbContext> BuildOptions(bool ignorePendingModelChangesWarning)
    {
        var builder = new DbContextOptionsBuilder<RaqmiDbContext>().UseNpgsql(ConnectionString);

        if (ignorePendingModelChangesWarning)
        {
            builder.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        }

        return builder.Options;
    }
}
