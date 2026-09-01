using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Lodging;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests.Postgres;

/// <summary>
/// La course sur la DERNIERE chambre d'un type, jouee contre le vrai PostgreSQL.
///
/// <see cref="LodgingReservationConcurrencyTests"/> prouve l'invariant anti-surrevente sur SQLite,
/// qui refuse le second ecrivain par un verrou de fichier (« database is locked »). PostgreSQL ne
/// verrouille pas : il laisse les deux transactions Serializable lire le meme inventaire, ecrire
/// chacune leur dossier, et n'en abat qu'une au commit (SQLSTATE 40001) - ou, si la seconde
/// attend sur l'index unique du numero de dossier, la refuse par 23505 une fois la premiere
/// validee. C'est ce mecanisme-la, celui de la production, que le test verifie : deux
/// receptions vendent au meme instant l'unique chambre double libre ; une seule vente aboutit,
/// l'autre recoit un conflit rejouable, jamais un 500.
///
/// Meme harnais que le test SQLite : deux DbContext (donc deux connexions Npgsql), deux instances
/// de <see cref="LodgingService"/>, et un <see cref="Rendezvous"/> plante dans l'audit - le
/// collaborateur que le service appelle entre son controle de disponibilite et son commit - pour
/// tenir la premiere requete ouverte jusqu'a ce que la seconde ait, elle aussi, lu « il reste une
/// chambre ». Le rendez-vous se libere seul apres un delai, pour qu'un gagnant n'attende jamais
/// un perdant refuse avant d'y arriver.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait(PostgresCollection.CategoryTraitName, PostgresCollection.CategoryTraitValue)]
public sealed class PostgresReservationConcurrencyTests(PostgresDatabaseFixture fixture)
{
    private const string RoomTypeCode = "DBL";

    private static readonly DateOnly Arrival = new(2031, 5, 1);

    private static readonly DateOnly Departure = new(2031, 5, 4);

    /// <summary>
    /// Plus long que sur SQLite : chaque etape est un aller-retour reseau, et le perdant doit
    /// avoir le temps d'atteindre le rendez-vous avant que le gagnant ne renonce a l'attendre.
    /// </summary>
    private static readonly TimeSpan RendezvousTimeout = TimeSpan.FromSeconds(5);

    [PostgresFact]
    public async Task Deux_ventes_simultanees_de_la_derniere_chambre_d_un_type_ne_peuvent_pas_toutes_deux_aboutir()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var unitCode = $"PGL{suffix}";
        var customerCode = $"PGC{suffix}";

        await ArrangeLastRoomAsync(unitCode, customerCode);

        var rendezvous = new Rendezvous(RendezvousTimeout);

        await using var firstDbContext = fixture.CreateDbContext();
        await using var secondDbContext = fixture.CreateDbContext();

        var firstService = CreateService(firstDbContext, rendezvous);
        var secondService = CreateService(secondDbContext, rendezvous);

        // Vente PAR TYPE, sans chambre nommee : c'est le calcul de disponibilite du type - le
        // meme que la recherche, le forecast et un futur channel manager - qui est en course.
        var request = new CreateReservationRequest(
            unitCode,
            RoomId: null,
            customerCode,
            Arrival,
            Departure,
            GuestCount: 1,
            RoomTypeCode: RoomTypeCode);

        var firstSale = Task.Run(() => firstService.CreateReservationAsync(
            request,
            new OperationContext(null, "reception.une", "127.0.0.1"),
            CancellationToken.None));

        var secondSale = Task.Run(() => secondService.CreateReservationAsync(
            request,
            new OperationContext(null, "reception.deux", "127.0.0.1"),
            CancellationToken.None));

        var results = await Task.WhenAll(firstSale, secondSale);

        var succeeded = results.Where(result => result.Succeeded).ToArray();
        var refused = results.Where(result => !result.Succeeded).ToArray();

        Assert.True(
            succeeded.Length == 1,
            $"Une seule des deux ventes simultanees peut aboutir ; {succeeded.Length} ont abouti.");

        // Refusee pour la bonne raison : le conflit rejouable que le service fabrique a partir
        // d'un 40001 (serialisation) ou d'un 23505 (numero de dossier), ou l'invariant lui-meme si
        // le perdant a relu l'inventaire apres le commit du gagnant. Jamais un echec inexplique.
        Assert.True(
            refused[0].ErrorType == ApplicationErrorType.Conflict,
            $"Refus inattendu ({refused[0].ErrorType}) : {refused[0].Error}");

        await using var verification = fixture.CreateDbContext();

        var blockingReservations = await verification.Reservations
            .AsNoTracking()
            .CountAsync(reservation => reservation.HotelUnitCode == unitCode
                && reservation.Status != ReservationStatus.Cancelled
                && reservation.Status != ReservationStatus.NoShow);

        Assert.True(
            blockingReservations == 1,
            "L'unite doit finir avec exactement un dossier bloquant sur sa seule chambre ; "
            + $"{blockingReservations} ont ete persistes.");

        // La transaction du perdant a ete abandonnee en entier : pas de trace d'audit d'une vente
        // qui n'a pas eu lieu. Le filtre porte sur l'identifiant d'entite (texte) et non sur
        // details_json : cette colonne est jsonb sur PostgreSQL, et un LIKE dessus - que SQLite
        // accepte sans broncher - y est refuse (« operator does not exist: jsonb ~~ jsonb »).
        var winnerId = succeeded[0].Value!.Id.ToString();

        var creationAuditEntries = await verification.AuditLogs
            .CountAsync(auditLog => auditLog.Action == "lodging.reservation.created"
                && auditLog.EntityName == "lodging.reservations"
                && auditLog.EntityId == winnerId);

        Assert.Equal(1, creationAuditEntries);

        // Et aucun audit de creation orphelin : chacun designe un dossier qui existe. L'audit est
        // ecrit dans la transaction Serializable du dossier ; si le perdant avait ete abandonne
        // APRES son audit et non avec lui, sa trace resterait ici sans reservation en face.
        var orphanAuditEntries = await verification.AuditLogs
            .CountAsync(auditLog => auditLog.Action == "lodging.reservation.created"
                && auditLog.EntityName == "lodging.reservations"
                && !verification.Reservations.Any(reservation => reservation.Id.ToString() == auditLog.EntityId));

        Assert.Equal(0, orphanAuditEntries);
    }

    /// <summary>
    /// Une unite, un type double, UNE chambre de ce type, un client : la plus petite population
    /// dans laquelle chaque vente prise seule est legitime et les deux ensemble impossibles.
    /// </summary>
    private async Task ArrangeLastRoomAsync(string unitCode, string customerCode)
    {
        await using var dbContext = fixture.CreateDbContext();

        dbContext.HotelUnits.Add(new HotelUnit(unitCode, "Hotel PostgreSQL", HotelUnitType.Hotel));
        dbContext.RoomTypes.Add(new RoomType(unitCode, RoomTypeCode, "Chambre double", 2));
        dbContext.Rooms.Add(new Room(unitCode, "101", RoomTypeCode));
        dbContext.Customers.Add(new Customer(customerCode, "Client PostgreSQL", CustomerType.Individual));

        await dbContext.SaveChangesAsync();
    }

    private static LodgingService CreateService(RaqmiDbContext dbContext, Rendezvous rendezvous)
    {
        return new LodgingService(
            dbContext,
            new RendezvousAuditLogWriter(new AuditLogWriter(dbContext), rendezvous),
            new StubTariffResolutionService());
    }

    /// <summary>
    /// Retient une requete a l'endroit ou elle a lu l'inventaire mais n'a pas encore valide - la
    /// fenetre qu'un garde « lire puis ecrire » laisse ouverte - et la libere quand l'autre y est
    /// arrivee aussi, ou apres <see cref="RendezvousTimeout"/>.
    /// </summary>
    private sealed class Rendezvous(TimeSpan timeout)
    {
        private readonly TaskCompletionSource _bothArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _arrivals;

        public Task ArriveAsync()
        {
            if (Interlocked.Increment(ref _arrivals) >= 2)
            {
                _bothArrived.TrySetResult();
                return Task.CompletedTask;
            }

            return Task.WhenAny(_bothArrived.Task, Task.Delay(timeout));
        }
    }

    /// <summary>
    /// Le service ecrit son audit apres le controle de disponibilite et avant son commit, dans la
    /// transaction Serializable : l'audit est donc l'endroit naturel ou suspendre une requete a
    /// l'interieur de cette fenetre sans toucher au code de production.
    /// </summary>
    private sealed class RendezvousAuditLogWriter(IAuditLogWriter inner, Rendezvous rendezvous) : IAuditLogWriter
    {
        public async Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken)
        {
            await rendezvous.ArriveAsync();
            await inner.WriteAsync(entry, cancellationToken);
        }
    }
}
