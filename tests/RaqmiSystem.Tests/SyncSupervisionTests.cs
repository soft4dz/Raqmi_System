using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Application.Sync;
using RaqmiSystem.Domain.Sync;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Sync;

namespace RaqmiSystem.Tests;

/// <summary>
/// Couverture du module 29 (registre des postes et journal des erreurs clients) contre une base
/// SQLite ":memory:" dediee par test.
///
/// Les cas retenus visent ce qui pourrait reellement nuire : un poste qui n'arrive pas a
/// s'enregistrer quand deux battements se croisent, un lot renvoye qui creerait des doublons, une
/// route ouverte qui laisserait ecrire sans limite, une horloge de poste absurde propagee telle
/// quelle, et surtout un jeton ou un mot de passe qui atterrirait en base via le message d'erreur.
/// </summary>
public sealed class SyncSupervisionTests
{
    private static readonly OperationContext Context = new(null, "reception1", "127.0.0.1");

    private static readonly Guid StationId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // ----------------------------- Registre des postes -----------------------------

    [Fact]
    public async Task Le_premier_battement_cree_le_poste()
    {
        await using var harness = await HarnessAsync();

        var result = await harness.Service.HeartbeatAsync(
            Heartbeat("POSTE-RECEPTION", "1.4.0"),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("POSTE-RECEPTION", result.Value!.Label);
        Assert.Equal("1.4.0", result.Value.AppVersion);

        // Le nom d'utilisateur vient du contexte d'appel, jamais du corps de la requete : un poste
        // ne doit pas pouvoir attribuer son activite a quelqu'un d'autre.
        Assert.Equal("reception1", result.Value.LastUserName);
        Assert.Equal("Recent", result.Value.Freshness);

        Assert.Equal(1, await harness.DbContext.Workstations.CountAsync());
    }

    [Fact]
    public async Task Un_second_battement_met_a_jour_sans_creer_de_doublon()
    {
        await using var harness = await HarnessAsync();

        await harness.Service.HeartbeatAsync(Heartbeat("POSTE-A", "1.4.0"), Context, CancellationToken.None);

        var stored = await harness.DbContext.Workstations.SingleAsync();
        var firstSeen = stored.CreatedAt;

        var second = new OperationContext(null, "caissier2", "127.0.0.1");
        var result = await harness.Service.HeartbeatAsync(Heartbeat("POSTE-A", "1.5.0"), second, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(1, await harness.DbContext.Workstations.CountAsync());

        // Le premier contact ne bouge pas ; la version et l'utilisateur, eux, decrivent l'etat
        // courant de la machine et sont donc rafraichis.
        Assert.Equal(firstSeen, result.Value!.FirstSeenUtc);
        Assert.Equal("1.5.0", result.Value.AppVersion);
        Assert.Equal("caissier2", result.Value.LastUserName);
    }

    [Fact]
    public async Task Deux_battements_simultanes_du_meme_poste_ne_levent_pas_d_exception()
    {
        // Cas reel : deux fenetres du meme poste, ou un battement declenche pendant qu'un autre
        // est encore en vol. Les deux trouvent le poste absent puis tentent l'insertion. Sans
        // rattrapage, le perdant recevrait une violation de cle primaire remontee a l'operateur.
        //
        // Base SQLite sur FICHIER et deux connexions distinctes : une base ":memory:" partagerait
        // une connexion unique, ce qui serialiserait les acces et ne prouverait rien. Meme
        // dispositif que InventoryStockConcurrencyTests.
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"raqmi-sync-heartbeat-{Guid.NewGuid():N}.sqlite");

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();

        try
        {
            await using (var seed = CreateDbContext(connectionString))
            {
                await seed.Database.EnsureCreatedAsync();
            }

            await using var firstDbContext = CreateDbContext(connectionString);
            await using var secondDbContext = CreateDbContext(connectionString);

            var firstService = new SyncSupervisionService(firstDbContext);
            var secondService = new SyncSupervisionService(secondDbContext);

            var first = Task.Run(() => firstService.HeartbeatAsync(
                Heartbeat("POSTE-A", "1.4.0"),
                Context,
                CancellationToken.None));

            var concurrent = Task.Run(() => secondService.HeartbeatAsync(
                Heartbeat("POSTE-A", "1.4.0"),
                Context,
                CancellationToken.None));

            var results = await Task.WhenAll(first, concurrent);

            // Les DEUX doivent reussir : un battement n'a aucune raison d'echouer, et surtout pas
            // de remonter une erreur technique a l'operateur.
            Assert.All(results, result => Assert.True(result.Succeeded, result.Error));

            await using var verification = CreateDbContext(connectionString);
            Assert.Equal(1, await verification.Workstations.CountAsync());
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private static RaqmiDbContext CreateDbContext(string connectionString) =>
        new(new DbContextOptionsBuilder<RaqmiDbContext>().UseSqlite(connectionString).Options);

    [Theory]
    [InlineData(0, "Recent")]
    [InlineData(14, "Recent")]
    [InlineData(15, "Stale")]
    [InlineData(59, "Stale")]
    [InlineData(60, "Silent")]
    [InlineData(60 * 24, "Silent")]
    public async Task La_fraicheur_est_calculee_par_le_serveur_aux_seuils_annonces(int minutes, string expected)
    {
        await using var harness = await HarnessAsync();

        await SeedStationAsync(harness, StationId, "POSTE-A", DateTime.UtcNow.AddMinutes(-minutes));

        var registry = await harness.Service.GetRegistryAsync(false, CancellationToken.None);

        Assert.True(registry.Succeeded);

        var station = Assert.Single(registry.Value!.Workstations);
        Assert.Equal(expected, station.Freshness);

        // Les seuils voyagent avec la reponse : l'ecran n'en recopie aucun.
        Assert.Equal(15, registry.Value.StaleAfterMinutes);
        Assert.Equal(60, registry.Value.OfflineAfterMinutes);
    }

    [Fact]
    public async Task Le_registre_masque_les_postes_anciens_sauf_demande_explicite()
    {
        await using var harness = await HarnessAsync();

        await SeedStationAsync(harness, StationId, "POSTE-ACTIF", DateTime.UtcNow.AddDays(-2));
        await SeedStationAsync(harness, Guid.NewGuid(), "POSTE-RETIRE", DateTime.UtcNow.AddDays(-45));

        var restrained = await harness.Service.GetRegistryAsync(false, CancellationToken.None);
        Assert.Equal("POSTE-ACTIF", Assert.Single(restrained.Value!.Workstations).Label);

        var all = await harness.Service.GetRegistryAsync(true, CancellationToken.None);
        Assert.Equal(2, all.Value!.Workstations.Count);
    }

    [Fact]
    public async Task Le_nombre_de_versions_distinctes_est_remonte()
    {
        // C'est le chiffre reellement utile du module : plusieurs versions du client contre la
        // meme API est un danger d'exploitation.
        await using var harness = await HarnessAsync();

        await SeedStationAsync(harness, Guid.NewGuid(), "POSTE-A", DateTime.UtcNow, "1.4.0");
        await SeedStationAsync(harness, Guid.NewGuid(), "POSTE-B", DateTime.UtcNow, "1.4.0");
        await SeedStationAsync(harness, Guid.NewGuid(), "POSTE-C", DateTime.UtcNow, "1.5.0");

        var registry = await harness.Service.GetRegistryAsync(false, CancellationToken.None);

        Assert.Equal(2, registry.Value!.DistinctAppVersions);
    }

    // --------------------------- Journal des erreurs ---------------------------

    [Fact]
    public async Task Un_lot_renvoye_deux_fois_n_insere_qu_une_seule_fois()
    {
        await using var harness = await HarnessAsync();
        await SeedStationAsync(harness, StationId, "POSTE-A", DateTime.UtcNow);

        var batch = new ReportWorkstationFailuresRequest(StationId, [Item(Guid.NewGuid()), Item(Guid.NewGuid())]);

        var first = await harness.Service.ReportFailuresAsync(batch, Context, CancellationToken.None);
        var second = await harness.Service.ReportFailuresAsync(batch, Context, CancellationToken.None);

        Assert.Equal(2, first.Value);

        // Le second envoi ne ment pas en annoncant deux ecritures : il rend zero.
        Assert.Equal(0, second.Value);
        Assert.Equal(2, await harness.DbContext.WorkstationFailures.CountAsync());
    }

    [Fact]
    public async Task Un_lot_trop_gros_est_refuse()
    {
        // La route est ouverte a tout utilisateur authentifie : sans plafond elle deviendrait un
        // moyen d'ecrire des lignes sans limite.
        await using var harness = await HarnessAsync();
        await SeedStationAsync(harness, StationId, "POSTE-A", DateTime.UtcNow);

        var items = Enumerable.Range(0, 51).Select(_ => Item(Guid.NewGuid())).ToList();

        var result = await harness.Service.ReportFailuresAsync(
            new ReportWorkstationFailuresRequest(StationId, items),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);
        Assert.Equal(0, await harness.DbContext.WorkstationFailures.CountAsync());
    }

    [Fact]
    public async Task Un_poste_inconnu_ne_peut_pas_alimenter_le_journal()
    {
        await using var harness = await HarnessAsync();

        var result = await harness.Service.ReportFailuresAsync(
            new ReportWorkstationFailuresRequest(Guid.NewGuid(), [Item(Guid.NewGuid())]),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);
        Assert.Equal(0, await harness.DbContext.WorkstationFailures.CountAsync());
    }

    [Fact]
    public async Task Une_horloge_de_poste_absurde_est_bornee_et_non_propagee()
    {
        await using var harness = await HarnessAsync();
        await SeedStationAsync(harness, StationId, "POSTE-A", DateTime.UtcNow);

        // Un poste regle sur l'an 2400 : sans bornage, l'ecart en secondes deborderait un entier
        // 32 bits. On veut un chiffre borne, lisible comme "cette horloge est absurde".
        var absurd = Item(Guid.NewGuid()) with { ClaimedAtUtc = new DateTimeOffset(2400, 1, 1, 0, 0, 0, TimeSpan.Zero) };

        var result = await harness.Service.ReportFailuresAsync(
            new ReportWorkstationFailuresRequest(StationId, [absurd]),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded);

        var stored = await harness.DbContext.WorkstationFailures.SingleAsync();
        Assert.Equal(-WorkstationFailure.MaxClockDriftSeconds, stored.ClockDriftSeconds);
    }

    [Fact]
    public async Task Le_serveur_assainit_le_message_meme_si_le_poste_ne_l_a_pas_fait()
    {
        // Defense en profondeur : cette route accepte n'importe quel client authentifie, la base
        // ne doit pas dependre de la bonne conduite de l'appelant pour rester exempte de secrets.
        await using var harness = await HarnessAsync();
        await SeedStationAsync(harness, StationId, "POSTE-A", DateTime.UtcNow);

        var leaky = Item(Guid.NewGuid()) with
        {
            Message = "401 refuse avec Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NSJ9.abcdef",
            Path = "/api/v1/customers?nif=000216001234567&token=abc"
        };

        var result = await harness.Service.ReportFailuresAsync(
            new ReportWorkstationFailuresRequest(StationId, [leaky]),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded);

        var stored = await harness.DbContext.WorkstationFailures.SingleAsync();

        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9", stored.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("abcdef", stored.Message, StringComparison.Ordinal);

        // La chaine de requete disparait : elle portait un NIF client et un jeton.
        Assert.Equal("/api/v1/customers", stored.Path);
    }

    [Fact]
    public async Task Le_journal_est_rendu_du_plus_recent_au_plus_ancien_avec_le_nom_du_poste()
    {
        await using var harness = await HarnessAsync();
        await SeedStationAsync(harness, StationId, "POSTE-RECEPTION", DateTime.UtcNow);

        await harness.Service.ReportFailuresAsync(
            new ReportWorkstationFailuresRequest(StationId, [Item(Guid.NewGuid()) with { Message = "premier" }]),
            Context,
            CancellationToken.None);

        await harness.Service.ReportFailuresAsync(
            new ReportWorkstationFailuresRequest(StationId, [Item(Guid.NewGuid()) with { Message = "second" }]),
            Context,
            CancellationToken.None);

        var journal = await harness.Service.GetFailuresAsync(50, CancellationToken.None);

        Assert.True(journal.Succeeded);
        Assert.Equal(2, journal.Value!.Count);
        Assert.All(journal.Value, row => Assert.Equal("POSTE-RECEPTION", row.WorkstationLabel));
    }

    [Fact]
    public async Task La_taille_de_page_du_journal_est_plafonnee()
    {
        await using var harness = await HarnessAsync();
        await SeedStationAsync(harness, StationId, "POSTE-A", DateTime.UtcNow);

        // Une demande delirante ne doit pas devenir une lecture integrale de la table.
        var journal = await harness.Service.GetFailuresAsync(int.MaxValue, CancellationToken.None);

        Assert.True(journal.Succeeded);
        Assert.Empty(journal.Value!);
    }

    // --------------------------------- Assainisseur ---------------------------------

    [Theory]
    [InlineData("Authorization: Bearer abc.def.ghi", "abc.def.ghi")]
    [InlineData("{\"password\":\"Secret123!\"}", "Secret123!")]
    [InlineData("token=aVeryLongOpaqueValue", "aVeryLongOpaqueValue")]
    [InlineData("mot_de_passe: Azerty2026", "Azerty2026")]
    [InlineData("api_key=sk-1234567890", "sk-1234567890")]
    public void L_assainisseur_retire_les_secrets_reconnus(string raw, string secret)
    {
        var sanitized = FailureMessageSanitizer.Sanitize(raw);

        Assert.DoesNotContain(secret, sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void L_assainisseur_retire_un_jeton_jwt_isole()
    {
        var jwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.SflKxwRJSMeKKF2QT4";

        var sanitized = FailureMessageSanitizer.Sanitize($"Echec avec {jwt} en en-tete");

        Assert.DoesNotContain(jwt, sanitized, StringComparison.Ordinal);
        Assert.Contains("[jeton masque]", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void L_assainisseur_garde_un_message_ordinaire_lisible()
    {
        // Le masquage doit rester cible : un journal illisible ne sert a rien.
        const string message = "La periode comptable du 31/12/2026 est cloturee.";

        Assert.Equal(message, FailureMessageSanitizer.Sanitize(message));
    }

    [Fact]
    public void L_assainisseur_borne_la_longueur()
    {
        var sanitized = FailureMessageSanitizer.Sanitize(new string('x', 5000));

        Assert.Equal(FailureMessageSanitizer.MessageMaxLength, sanitized.Length);
    }

    [Fact]
    public void L_assainisseur_supporte_un_message_vide()
    {
        Assert.Equal("(sans message)", FailureMessageSanitizer.Sanitize(null));
        Assert.Equal("(sans message)", FailureMessageSanitizer.Sanitize("   "));
    }

    // ------------------------------------ Harnais ------------------------------------

    private static WorkstationHeartbeatRequest Heartbeat(string label, string version) =>
        new(StationId, label, version, null);

    private static WorkstationFailureItem Item(Guid eventId) =>
        new(eventId, "POST", "/api/v1/revenue/daily", 500, "HttpError", "Erreur serveur", DateTimeOffset.UtcNow);

    private static async Task SeedStationAsync(
        Harness harness,
        Guid id,
        string label,
        DateTime lastSeen,
        string version = "1.4.0")
    {
        var station = Workstation.Register(id, label, "reception1", version, null, lastSeen);
        station.MarkCreated("reception1", lastSeen);

        harness.DbContext.Workstations.Add(station);
        await harness.DbContext.SaveChangesAsync();
        harness.DbContext.ChangeTracker.Clear();
    }

    private static async Task<Harness> HarnessAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var dbContext = new RaqmiDbContext(
            new DbContextOptionsBuilder<RaqmiDbContext>()
                .UseSqlite(connection)
                .Options);

        await dbContext.Database.EnsureCreatedAsync();

        return new Harness(connection, dbContext, new SyncSupervisionService(dbContext));
    }

    private sealed class Harness(
        SqliteConnection connection,
        RaqmiDbContext dbContext,
        SyncSupervisionService service) : IAsyncDisposable
    {
        public RaqmiDbContext DbContext { get; } = dbContext;

        public SyncSupervisionService Service { get; } = service;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
