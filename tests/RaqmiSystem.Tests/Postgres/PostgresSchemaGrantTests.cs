using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace RaqmiSystem.Tests.Postgres;

/// <summary>
/// Le role applicatif <c>raqmi_app</c> (deploy/postgres/create-app-role.sql) doit couvrir CHAQUE
/// schema que les migrations creent. C'est lui qui fait tourner l'API et, sur site, pg_dump : un
/// schema cree par une migration et oublie du script est un schema dont les tables repondent
/// « permission denied » a l'API et qui manque dans chaque sauvegarde - ce fut le cas de crm,
/// housekeeping, hr et kpi, soit 25 tables sur 104, et /health/database repondait « healthy ».
///
/// Deux tests, deux niveaux de preuve :
/// <list type="bullet">
/// <item>l'ensemble des schemas du script est EGAL a l'ensemble des schemas migres, dans chacun
/// des cinq blocs de GRANT. Un schema migre absent du script casse la sauvegarde ; un schema du
/// script absent de la base fait echouer le script lui-meme (ON_ERROR_STOP) et donc
/// l'installation ;</item>
/// <item>les GRANT du script, rejoues tels quels sur un role jetable, donnent bien USAGE sur
/// chaque schema et SELECT sur chaque table de la base - ce que pg_dump exige reellement.</item>
/// </list>
///
/// Toute migration qui ajoute un schema rend ces tests rouges tant que le script n'est pas
/// complete ; c'est le but.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait(PostgresCollection.CategoryTraitName, PostgresCollection.CategoryTraitValue)]
public sealed partial class PostgresSchemaGrantTests(PostgresDatabaseFixture fixture)
{
    private const string ScriptRelativePath = "deploy/postgres/create-app-role.sql";

    private const string HistoryTableName = "__EFMigrationsHistory";

    /// <summary>
    /// Schemas de PostgreSQL lui-meme, jamais crees par une migration et jamais a accorder.
    /// <c>public</c> est traite a part : seule la table d'historique des migrations y vit
    /// (HasDefaultSchema("raqmi") ne la deplace pas), et le script lui accorde un SELECT
    /// nominatif, verifie ci-dessous a l'endroit ou elle se trouve reellement.
    /// </summary>
    private static readonly string[] SystemSchemas = ["pg_catalog", "information_schema", "public"];

    [PostgresFact]
    public async Task Le_script_du_role_applicatif_cite_exactement_les_schemas_que_les_migrations_creent()
    {
        var script = StripComments(await File.ReadAllTextAsync(LocateScript()));
        var granted = ExtractSchemas(script, UsageGrantPattern());
        var migrated = await ListMigratedSchemasAsync();

        Assert.NotEmpty(granted);
        Assert.NotEmpty(migrated);

        var missingFromScript = migrated.Except(granted).Order().ToArray();
        var unknownToDatabase = granted.Except(migrated).Order().ToArray();

        Assert.True(
            missingFromScript.Length == 0,
            "Des schemas crees par les migrations ne sont pas accordes a raqmi_app dans "
            + ScriptRelativePath + " : " + string.Join(", ", missingFromScript)
            + ". Sans GRANT, leurs tables sont inaccessibles a l'API et absentes de pg_dump. "
            + "Ajoutez chaque schema aux cinq blocs du script (USAGE, TABLES, SEQUENCES et les deux "
            + "ALTER DEFAULT PRIVILEGES).");

        Assert.True(
            unknownToDatabase.Length == 0,
            "Des schemas accordes dans " + ScriptRelativePath + " n'existent pas apres migration : "
            + string.Join(", ", unknownToDatabase)
            + ". Avec ON_ERROR_STOP, le premier GRANT sur un schema inconnu interrompt tout le script "
            + "et l'installation livre un role sans aucun droit.");

        // Un schema cite dans le bloc USAGE mais oublie d'un des quatre autres blocs laisserait
        // raqmi_app entrer dans le schema sans pouvoir en lire les tables : chaque bloc doit citer
        // le meme ensemble.
        foreach (var (label, pattern) in GrantBlocks())
        {
            var missing = granted.Except(ExtractSchemas(script, pattern)).Order().ToArray();

            Assert.True(
                missing.Length == 0,
                $"Bloc « {label} » de {ScriptRelativePath} incomplet, schemas manquants : {string.Join(", ", missing)}.");
        }

        // La table d'historique n'est pas dans un schema metier : pg_dump doit pourtant la lire,
        // sinon la sauvegarde restauree ne peut plus etre migree. Le SELECT nominatif du script
        // doit viser le schema ou EF l'a reellement creee.
        var historySchema = await FindHistoryTableSchemaAsync();

        Assert.True(
            historySchema is not null,
            $"La table {HistoryTableName} est introuvable dans la base migree.");

        Assert.True(
            HistoryGrantPattern(historySchema!).IsMatch(script),
            $"{ScriptRelativePath} doit accorder SELECT sur {historySchema}.\"{HistoryTableName}\" a raqmi_app "
            + "(c'est la que la table d'historique des migrations se trouve).");
    }

    [PostgresFact]
    public async Task Les_GRANT_du_script_donnent_au_role_la_lecture_de_chaque_schema_et_de_chaque_table()
    {
        var script = StripComments(await File.ReadAllTextAsync(LocateScript()));

        // Un role est global au serveur : nom unique par execution, et suppression garantie en
        // sortie, meme quand une assertion echoue. Necessite CREATEROLE sur la connexion de test
        // (le compte administrateur de la fixture, superutilisateur en CI comme dans le compose).
        var role = $"raqmi_test_app_{Guid.NewGuid():N}"[..40];

        await using var dbContext = fixture.CreateDbContext();

        await using var connection = new NpgsqlConnection(dbContext.Database.GetConnectionString());
        await connection.OpenAsync();

        await ExecuteAsync(connection, $"CREATE ROLE \"{role}\" NOLOGIN");

        try
        {
            var statements = ExtractGrantStatements(script, role);

            // Garde-fou du test lui-meme : si l'extraction ne trouvait rien, chaque table serait
            // « illisible » pour une raison qui n'a rien a voir avec le script.
            Assert.True(statements.Count >= 5, "Aucun GRANT extrait de " + ScriptRelativePath + ".");

            foreach (var statement in statements)
            {
                await ExecuteAsync(connection, statement);
            }

            var unreadable = new List<string>();

            foreach (var (schema, table) in await ListMigratedTablesAsync(connection))
            {
                // pg_dump a besoin des deux : entrer dans le schema (USAGE) ET lire la table (SELECT).
                await using var command = new NpgsqlCommand(
                    "SELECT has_schema_privilege(@role, @schema, 'USAGE') AND has_table_privilege(@role, @table, 'SELECT')",
                    connection);
                command.Parameters.AddWithValue("role", role);
                command.Parameters.AddWithValue("schema", schema);
                command.Parameters.AddWithValue("table", table);

                if (await command.ExecuteScalarAsync() is not true)
                {
                    unreadable.Add(table);
                }
            }

            Assert.True(
                unreadable.Count == 0,
                "Apres application de " + ScriptRelativePath + ", le role applicatif ne peut pas lire : "
                + string.Join(", ", unreadable)
                + ". pg_dump sous ce role s'arreterait sur la premiere de ces tables.");
        }
        finally
        {
            // DROP OWNED retire les privileges (y compris les privileges par defaut) accordes au
            // role dans cette base ; sans cela DROP ROLE refuse.
            await ExecuteAsync(connection, $"DROP OWNED BY \"{role}\"");
            await ExecuteAsync(connection, $"DROP ROLE \"{role}\"");
        }
    }

    private async Task<HashSet<string>> ListMigratedSchemasAsync()
    {
        await using var dbContext = fixture.CreateDbContext();

        var schemas = await dbContext.Database
            .SqlQueryRaw<string>("SELECT nspname AS \"Value\" FROM pg_namespace")
            .ToListAsync();

        return schemas
            .Where(schema => !schema.StartsWith("pg_", StringComparison.Ordinal))
            .Except(SystemSchemas)
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task<string?> FindHistoryTableSchemaAsync()
    {
        await using var dbContext = fixture.CreateDbContext();

        var schemas = await dbContext.Database
            .SqlQueryRaw<string>(
                "SELECT table_schema AS \"Value\" FROM information_schema.tables WHERE table_name = {0}",
                HistoryTableName)
            .ToListAsync();

        return schemas.SingleOrDefault();
    }

    private static async Task<IReadOnlyList<(string Schema, string QualifiedName)>> ListMigratedTablesAsync(NpgsqlConnection connection)
    {
        // Tables de base seulement, y compris la table d'historique des migrations : pg_dump la
        // lit aussi, et une sauvegarde sans elle ne pourrait pas etre migree apres restauration.
        const string sql = """
            SELECT table_schema, format('%I.%I', table_schema, table_name)
            FROM information_schema.tables
            WHERE table_type = 'BASE TABLE'
              AND table_schema NOT IN ('pg_catalog', 'information_schema')
            ORDER BY 1, 2
            """;

        var tables = new List<(string, string)>();

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            tables.Add((reader.GetString(0), reader.GetString(1)));
        }

        return tables;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static HashSet<string> ExtractSchemas(string script, Regex pattern)
    {
        return pattern.Matches(script)
            .Select(match => match.Groups["schema"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Les ordres GRANT / ALTER DEFAULT PRIVILEGES du script, tels quels, le nom du role remplace
    /// par le role jetable. Chaque ordre commence en debut de ligne et se termine au premier
    /// point-virgule (les ALTER DEFAULT PRIVILEGES tiennent sur deux lignes). Les meta-commandes
    /// psql (\set, \if, \gexec) et le CREATE ROLE ne sont pas rejoues : ce test ne prouve pas
    /// psql, il prouve les droits accordes.
    /// </summary>
    private static IReadOnlyList<string> ExtractGrantStatements(string script, string role)
    {
        return GrantStatementPattern().Matches(script)
            .Select(match => RoleNamePattern().Replace(match.Value, $"\"{role}\""))
            .ToArray();
    }

    private static string StripComments(string script)
    {
        return CommentPattern().Replace(script, string.Empty);
    }

    private static IEnumerable<(string Label, Regex Pattern)> GrantBlocks()
    {
        yield return ("GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA", TableGrantPattern());
        yield return ("GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA", SequenceGrantPattern());
        yield return ("ALTER DEFAULT PRIVILEGES ... ON TABLES", DefaultTablePrivilegesPattern());
        yield return ("ALTER DEFAULT PRIVILEGES ... ON SEQUENCES", DefaultSequencePrivilegesPattern());
    }

    private static Regex HistoryGrantPattern(string schema)
    {
        return new Regex(
            $@"\bGRANT\s+SELECT\s+ON\s+TABLE\s+{Regex.Escape(schema)}\.""{Regex.Escape(HistoryTableName)}""\s+TO\s+raqmi_app\b",
            RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Le script se trouve a partir du dossier du binaire de test en remontant jusqu'a la racine
    /// du depot (celle qui porte RaqmiSystem.sln) : meme chemin en local et en CI, sans copier
    /// le fichier dans la sortie de build - une copie derive, l'original non.
    /// </summary>
    private static string LocateScript()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RaqmiSystem.sln")))
            {
                var path = Path.Combine(directory.FullName, ScriptRelativePath.Replace('/', Path.DirectorySeparatorChar));

                Assert.True(File.Exists(path), $"Script introuvable : {path}");

                return path;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Racine du depot introuvable (RaqmiSystem.sln) depuis " + AppContext.BaseDirectory);
    }

    [GeneratedRegex(@"--[^\r\n]*")]
    private static partial Regex CommentPattern();

    [GeneratedRegex(@"^(?:GRANT|ALTER\s+DEFAULT\s+PRIVILEGES)\b[^;]*;", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex GrantStatementPattern();

    [GeneratedRegex(@"\bGRANT\s+USAGE\s+ON\s+SCHEMA\s+(?<schema>[a-z_][a-z0-9_]*)\s+TO\s+raqmi_app\b", RegexOptions.IgnoreCase)]
    private static partial Regex UsageGrantPattern();

    [GeneratedRegex(@"\bGRANT\s+SELECT,\s*INSERT,\s*UPDATE,\s*DELETE\s+ON\s+ALL\s+TABLES\s+IN\s+SCHEMA\s+(?<schema>[a-z_][a-z0-9_]*)\s+TO\s+raqmi_app\b", RegexOptions.IgnoreCase)]
    private static partial Regex TableGrantPattern();

    [GeneratedRegex(@"\bGRANT\s+USAGE,\s*SELECT\s+ON\s+ALL\s+SEQUENCES\s+IN\s+SCHEMA\s+(?<schema>[a-z_][a-z0-9_]*)\s+TO\s+raqmi_app\b", RegexOptions.IgnoreCase)]
    private static partial Regex SequenceGrantPattern();

    [GeneratedRegex(@"\bALTER\s+DEFAULT\s+PRIVILEGES\s+IN\s+SCHEMA\s+(?<schema>[a-z_][a-z0-9_]*)\s+GRANT\s+SELECT,\s*INSERT,\s*UPDATE,\s*DELETE\s+ON\s+TABLES\s+TO\s+raqmi_app\b", RegexOptions.IgnoreCase)]
    private static partial Regex DefaultTablePrivilegesPattern();

    [GeneratedRegex(@"\bALTER\s+DEFAULT\s+PRIVILEGES\s+IN\s+SCHEMA\s+(?<schema>[a-z_][a-z0-9_]*)\s+GRANT\s+USAGE,\s*SELECT\s+ON\s+SEQUENCES\s+TO\s+raqmi_app\b", RegexOptions.IgnoreCase)]
    private static partial Regex DefaultSequencePrivilegesPattern();

    [GeneratedRegex(@"\braqmi_app\b")]
    private static partial Regex RoleNamePattern();
}
