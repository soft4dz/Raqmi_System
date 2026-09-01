using Microsoft.EntityFrameworkCore;

namespace RaqmiSystem.Tests.Postgres;

/// <summary>
/// Les migrations EF face au vrai PostgreSQL. SQLite ne les execute jamais : le harnais des
/// autres tests construit son schema par <c>EnsureCreated</c>, directement depuis le modele.
/// Une migration cassee (SQL invalide pour Npgsql, reprise de donnees fausse, contrainte
/// impossible a poser) ne se verrait donc qu'au premier <c>dotnet ef database update</c> d'un
/// environnement reel. Ces quatre tests la voient avant.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait(PostgresCollection.CategoryTraitName, PostgresCollection.CategoryTraitValue)]
public sealed class PostgresMigrationTests(PostgresDatabaseFixture fixture)
{
    /// <summary>
    /// Schemas dont l'existence prouve que les migrations ont reellement construit la base, et
    /// pas seulement rempli la table d'historique.
    /// </summary>
    private static readonly string[] ExpectedSchemas =
    [
        "security", "organization", "exploitation", "finance", "accounting", "lodging"
    ];

    [PostgresFact]
    public async Task Toutes_les_migrations_s_appliquent_depuis_une_base_vide()
    {
        // La fixture a deja migre la base commune depuis zero : ce test rend ce fait explicite
        // dans le rapport, et verifie que le resultat est un schema, pas seulement un historique.
        await using var dbContext = fixture.CreateDbContext();

        var all = dbContext.Database.GetMigrations().ToArray();
        var applied = (await dbContext.Database.GetAppliedMigrationsAsync()).ToArray();

        Assert.NotEmpty(all);
        Assert.Equal(all, applied);

        var schemas = await dbContext.Database
            .SqlQueryRaw<string>("SELECT schema_name AS \"Value\" FROM information_schema.schemata")
            .ToListAsync();

        foreach (var expected in ExpectedSchemas)
        {
            Assert.Contains(expected, schemas);
        }

        // Et les tables repondent : une requete EF traduite pour Npgsql aboutit sur le schema
        // migre (la base est vierge, le compte est zero).
        Assert.Equal(0, await dbContext.Users.CountAsync());
    }

    /// <summary>
    /// LE garde anti-derive. Une entite ou une configuration modifiee sans migration passe
    /// inapercue sur SQLite (EnsureCreated suit toujours le modele) ; ici elle echoue, avec la
    /// commande qui la corrige. C'est ce test qui impose au developpeur de livrer la migration
    /// avec le changement de modele, dans le meme commit.
    /// </summary>
    [PostgresFact]
    public async Task Le_modele_EF_ne_derive_pas_de_la_derniere_migration()
    {
        await using var dbContext = fixture.CreateDbContext();

        var pending = (await dbContext.Database.GetPendingMigrationsAsync()).ToArray();

        Assert.True(
            pending.Length == 0,
            "Des migrations du depot ne sont pas appliquees sur la base de test : "
            + string.Join(", ", pending));

        Assert.False(
            dbContext.Database.HasPendingModelChanges(),
            "Le modele EF (entites, configurations) a change sans migration : le snapshot ne le "
            + "decrit plus. Generez la migration manquante et commitez-la avec le changement :\n"
            + "  dotnet ef migrations add <NomDeLaMigration> "
            + "--project src/RaqmiSystem.Infrastructure/RaqmiSystem.Infrastructure.csproj "
            + "--startup-project src/RaqmiSystem.Api/RaqmiSystem.Api.csproj "
            + "--output-dir Persistence/Migrations");
    }

    /// <summary>
    /// Le chemin que suit une installation deja en production : elle est a N-1, elle recoit N.
    /// Migrer depuis zero ne le prouve pas - une migration qui recree une table que la precedente
    /// avait deja posee passe depuis zero et casse depuis N-1.
    /// </summary>
    [PostgresFact]
    public async Task La_derniere_migration_s_applique_depuis_l_etat_N_moins_1()
    {
        await using var database = await fixture.CreateDatabaseAsync("nminus1");

        var all = await ListMigrationsAsync(database);

        Assert.True(all.Length >= 2, "Le scenario N-1 suppose au moins deux migrations dans le depot.");

        var previous = all[^2];
        var last = all[^1];

        await database.MigrateAsync(previous);

        await using (var dbContext = database.CreateDbContext())
        {
            Assert.Equal(all[..^1], (await dbContext.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Equal([last], (await dbContext.Database.GetPendingMigrationsAsync()).ToArray());
        }

        await database.MigrateAsync(last);

        await using (var dbContext = database.CreateDbContext())
        {
            Assert.Equal(all, (await dbContext.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync());
        }
    }

    /// <summary>
    /// Le retour en arriere d'une version, tel qu'un incident de deploiement l'exige : la
    /// derniere migration se retire (Down) puis se reapplique. Un Down qui ne compile pas le
    /// SQL, ou qui oublie un index, ne se decouvre autrement que le jour de l'incident.
    /// </summary>
    [PostgresFact]
    public async Task La_derniere_migration_se_retire_puis_se_reapplique()
    {
        await using var database = await fixture.CreateDatabaseAsync("rollback");

        var all = await ListMigrationsAsync(database);

        Assert.True(all.Length >= 2, "Le scenario de retour arriere suppose au moins deux migrations dans le depot.");

        await database.MigrateAsync();
        await database.MigrateAsync(all[^2]);

        await using (var dbContext = database.CreateDbContext())
        {
            Assert.Equal(all[..^1], (await dbContext.Database.GetAppliedMigrationsAsync()).ToArray());
        }

        await database.MigrateAsync();

        await using (var dbContext = database.CreateDbContext())
        {
            Assert.Equal(all, (await dbContext.Database.GetAppliedMigrationsAsync()).ToArray());
        }
    }

    private static async Task<string[]> ListMigrationsAsync(PostgresTestDatabase database)
    {
        await using var dbContext = database.CreateDbContext();

        return dbContext.Database.GetMigrations().ToArray();
    }
}
