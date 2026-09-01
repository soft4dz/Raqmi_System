using Npgsql;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests.Postgres;

/// <summary>
/// Fixture de la collection « Postgres » : une base PostgreSQL REELLE, creee au demarrage de la
/// collection sous un nom unique (<c>raqmi_test_main_&lt;suffixe&gt;</c>), migree par les
/// migrations EF du depot, et supprimee a la fin - y compris quand un test a echoue.
///
/// POURQUOI UNE BASE PAR EXECUTION, ET NON LA BASE DE DEVELOPPEMENT. Les tests de contraintes
/// provoquent des violations, ceux de migration rejouent des Down/Up : rien de cela n'a sa place
/// dans <c>raqmi_system</c>. La chaine de <c>RAQMI_TEST_POSTGRES</c> ne sert qu'a ouvrir une
/// connexion administrative pour CREATE DATABASE / DROP DATABASE ; la base qu'elle nomme n'est
/// jamais modifiee.
///
/// POURQUOI PASSER PAR <see cref="ConnectionStringFactory"/>. Les tests doivent parler a
/// PostgreSQL comme l'API le fait : meme pooling, memes delais, memes details d'erreur masques.
/// La fixture ne lit donc dans la variable que l'hote, le port, le role et le mot de passe, et
/// laisse la fabrique de production composer la chaine finale - une divergence entre les deux
/// serait un test qui ne teste pas la configuration livree.
///
/// Sans <c>RAQMI_TEST_POSTGRES</c>, la fixture ne fait rien : les tests sont deja marques
/// Skipped par <see cref="PostgresFactAttribute"/> et aucune connexion n'est tentee.
/// </summary>
public sealed class PostgresDatabaseFixture : IAsyncLifetime
{
    private readonly List<PostgresTestDatabase> _databases = [];

    private readonly object _gate = new();

    private string? _adminConnectionString;

    private NpgsqlConnectionStringBuilder? _adminSettings;

    private PostgresTestDatabase? _main;

    /// <summary>
    /// La base commune de la collection, migree jusqu'a la derniere migration. Les tests de
    /// contraintes et de concurrence travaillent dessus ; ils isolent leurs donnees par des codes
    /// uniques plutot que par un nettoyage, pour rester lisibles et rejouables.
    /// </summary>
    public PostgresTestDatabase Main =>
        _main ?? throw new InvalidOperationException(PostgresTestEnvironment.SkipReason);

    public RaqmiDbContext CreateDbContext()
    {
        return Main.CreateDbContext();
    }

    public async Task InitializeAsync()
    {
        var adminConnectionString = PostgresTestEnvironment.AdminConnectionString;

        if (adminConnectionString is null)
        {
            return;
        }

        _adminConnectionString = adminConnectionString;
        _adminSettings = new NpgsqlConnectionStringBuilder(adminConnectionString);

        var main = await CreateDatabaseAsync("main");

        try
        {
            await main.MigrateAsync();
        }
        catch
        {
            // Une migration qui echoue ne doit pas laisser une base orpheline sur le serveur.
            await main.DropAsync();
            throw;
        }

        _main = main;
    }

    /// <summary>
    /// Cree une base vierge supplementaire (aucune migration appliquee). L'appelant la libere par
    /// <c>await using</c> ; la fixture la supprime de toute facon en fin de collection.
    /// </summary>
    /// <param name="purpose">Court libelle repris dans le nom de la base, pour qu'un DBA qui
    /// tombe sur une base orpheline sache d'ou elle vient.</param>
    public async Task<PostgresTestDatabase> CreateDatabaseAsync(string purpose)
    {
        if (_adminConnectionString is null || _adminSettings is null)
        {
            throw new InvalidOperationException(PostgresTestEnvironment.SkipReason);
        }

        var name = BuildDatabaseName(purpose);

        // Le nom est compose de [a-z0-9_] uniquement (voir BuildDatabaseName) : il peut etre
        // insere tel quel dans le DDL, que PostgreSQL n'accepte de toute facon pas parametre.
        await ExecuteAdminAsync($"CREATE DATABASE \"{name}\" ENCODING 'UTF8'");

        var connectionString = ConnectionStringFactory.Build(new PostgresOptions
        {
            Host = _adminSettings.Host ?? "localhost",
            Port = _adminSettings.Port,
            Database = name,
            User = _adminSettings.Username ?? "raqmi",
            Password = _adminSettings.Password ?? string.Empty
        });

        var database = new PostgresTestDatabase(this, name, connectionString);

        lock (_gate)
        {
            _databases.Add(database);
        }

        return database;
    }

    internal async Task DropDatabaseAsync(PostgresTestDatabase database)
    {
        // Les connexions que le pool Npgsql garde ouvertes vers cette base bloqueraient le DROP :
        // on vide d'abord le pool, puis WITH (FORCE) - PostgreSQL 13+ - termine celles qu'un
        // DbContext non libere (test en echec) aurait encore en main.
        using (var probe = new NpgsqlConnection(database.ConnectionString))
        {
            NpgsqlConnection.ClearPool(probe);
        }

        await ExecuteAdminAsync($"DROP DATABASE IF EXISTS \"{database.Name}\" WITH (FORCE)");

        lock (_gate)
        {
            _databases.Remove(database);
        }
    }

    public async Task DisposeAsync()
    {
        List<PostgresTestDatabase> remaining;

        lock (_gate)
        {
            remaining = [.. _databases];
        }

        var failures = new List<Exception>();

        foreach (var database in remaining)
        {
            try
            {
                await database.DropAsync();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (failures.Count > 0)
        {
            // Une base laissee sur le serveur doit se voir dans le rapport, pas seulement dans
            // pg_database trois semaines plus tard.
            throw new AggregateException(
                "Des bases de test PostgreSQL n'ont pas pu etre supprimees : "
                + string.Join(", ", remaining.Select(database => database.Name)),
                failures);
        }
    }

    private async Task ExecuteAdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// <c>raqmi_test_&lt;objet&gt;_&lt;12 hex&gt;</c>, en minuscules et sans caractere a quoter :
    /// unique par execution (deux CI en parallele sur le meme serveur ne se marchent pas dessus)
    /// et toujours sous les 63 octets qu'un identifiant PostgreSQL peut porter.
    /// </summary>
    private static string BuildDatabaseName(string purpose)
    {
        var safePurpose = new string(purpose
            .ToLowerInvariant()
            .Where(character => char.IsAsciiLetterOrDigit(character) || character == '_')
            .Take(20)
            .ToArray());

        if (safePurpose.Length == 0)
        {
            safePurpose = "db";
        }

        return $"raqmi_test_{safePurpose}_{Guid.NewGuid():N}"[..(11 + safePurpose.Length + 1 + 12)];
    }
}
