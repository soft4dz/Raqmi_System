using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Mice;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Billing;
using RaqmiSystem.Infrastructure.Lodging;
using RaqmiSystem.Infrastructure.Mice;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Settings;

namespace RaqmiSystem.Tests;

/// <summary>
/// Couverture des allotements : le volet GROUPES du module 10.6.
///
/// TOUT CE FICHIER TOURNE AUTOUR D'UNE SEULE QUESTION : peut-on survendre ? Un allotement retire
/// des chambres de la vente publique sans les nommer. Si la recherche de disponibilite les cache
/// mais que la creation de reservation les accepte encore, l'hotel vend deux fois les memes
/// chambres et s'en apercoit le jour de l'arrivee du groupe. Les tests verifient donc SYSTEMATIQUE-
/// MENT les deux chemins, jamais un seul.
///
/// L'hotel de reference compte 4 chambres doubles. C'est assez petit pour que chaque nuitee compte
/// et que les bornes soient visibles a l'oeil nu.
/// </summary>
public sealed class RoomAllotmentTests
{
    private const string Unit = "HTL1";

    private const string TypeCode = "DBL";

    private const string GroupCustomer = "AGENCE1";

    private const string PublicCustomer = "CLI1";

    private static readonly DateOnly Arrival = new(2031, 5, 10);

    private static readonly DateOnly Departure = new(2031, 5, 13);

    private static readonly OperationContext Context = new(null, "reception", "127.0.0.1");

    // ===================== Le coeur : l'allotement retire des chambres =====================

    [Fact]
    public async Task Un_allotement_retire_des_chambres_de_la_disponibilite_publique()
    {
        await using var harness = await HarnessAsync();

        var before = await harness.Lodging.GetAvailabilityAsync(Unit, Arrival, Departure, 2, null, CancellationToken.None);
        Assert.Equal(4, before.Value!.Rooms.Count);

        await CreateAllotmentAsync(harness, "GRP-1", roomsHeld: 3);

        var after = await harness.Lodging.GetAvailabilityAsync(Unit, Arrival, Departure, 2, null, CancellationToken.None);

        // 4 chambres, 3 tenues : une seule reste vendable au public.
        Assert.Single(after.Value!.Rooms);
    }

    [Fact]
    public async Task Une_vente_publique_qui_entamerait_le_bloc_est_refusee()
    {
        // LE TEST QUI COMPTE. La recherche ne montre qu'une chambre ; on tente d'en vendre deux.
        // La seconde doit etre refusee, sinon la recherche mentait.
        await using var harness = await HarnessAsync();

        await CreateAllotmentAsync(harness, "GRP-1", roomsHeld: 3);

        var first = await BookPublicAsync(harness, harness.RoomIds[0]);
        Assert.True(first.Succeeded, first.Error);

        var second = await BookPublicAsync(harness, harness.RoomIds[1]);

        Assert.False(second.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, second.ErrorType);

        // Le refus NOMME le nombre de chambres tenues : l'operateur doit comprendre pourquoi une
        // chambre visiblement libre lui est refusee.
        Assert.Contains("3", second.Error);
        Assert.Contains("groupe", second.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task La_recherche_et_la_creation_disent_exactement_la_meme_chose()
    {
        // Les deux chemins partagent un unique calcul. Ce test l'eprouve sur toute la plage : pour
        // chaque taille de bloc, le nombre de chambres proposees doit egaler le nombre de chambres
        // que la creation accepte reellement.
        for (var held = 0; held <= 4; held++)
        {
            await using var harness = await HarnessAsync();

            if (held > 0)
            {
                await CreateAllotmentAsync(harness, $"GRP-{held}", held);
            }

            var availability = await harness.Lodging.GetAvailabilityAsync(
                Unit, Arrival, Departure, 2, null, CancellationToken.None);

            var offered = availability.Value!.Rooms.Count;
            var accepted = 0;

            foreach (var roomId in harness.RoomIds)
            {
                var booking = await BookPublicAsync(harness, roomId);

                if (booking.Succeeded)
                {
                    accepted++;
                }
            }

            Assert.Equal(4 - held, offered);
            Assert.Equal(offered, accepted);
        }
    }

    [Fact]
    public async Task Une_reservation_prise_sur_le_bloc_le_consomme_sans_reduire_le_public()
    {
        // Une chambre prise sur le bloc etait DEJA retiree de la vente publique. La compter une
        // seconde fois interdirait de vendre des chambres pourtant libres.
        await using var harness = await HarnessAsync();

        var allotment = await CreateAllotmentAsync(harness, "GRP-1", roomsHeld: 3);

        var onBlock = await BookOnAllotmentAsync(harness, harness.RoomIds[0], allotment.Id, "Dupont");
        Assert.True(onBlock.Succeeded, onBlock.Error);

        var availability = await harness.Lodging.GetAvailabilityAsync(
            Unit, Arrival, Departure, 2, null, CancellationToken.None);

        // 4 chambres : 1 prise sur le bloc, 2 encore tenues, donc 1 vendable au public - le meme
        // chiffre qu'avant la prise.
        Assert.Single(availability.Value!.Rooms);
    }

    [Fact]
    public async Task Un_bloc_entierement_consomme_ne_tient_plus_rien()
    {
        await using var harness = await HarnessAsync();

        var allotment = await CreateAllotmentAsync(harness, "GRP-1", roomsHeld: 2);

        await BookOnAllotmentAsync(harness, harness.RoomIds[0], allotment.Id, "Dupont");
        await BookOnAllotmentAsync(harness, harness.RoomIds[1], allotment.Id, "Martin");

        // Le bloc est plein : les deux chambres restantes redeviennent vendables au public.
        var availability = await harness.Lodging.GetAvailabilityAsync(
            Unit, Arrival, Departure, 2, null, CancellationToken.None);

        Assert.Equal(2, availability.Value!.Rooms.Count);

        var publicBooking = await BookPublicAsync(harness, harness.RoomIds[2]);
        Assert.True(publicBooking.Succeeded, publicBooking.Error);
    }

    [Fact]
    public async Task Un_bloc_plein_refuse_une_reservation_supplementaire()
    {
        await using var harness = await HarnessAsync();

        var allotment = await CreateAllotmentAsync(harness, "GRP-1", roomsHeld: 1);

        await BookOnAllotmentAsync(harness, harness.RoomIds[0], allotment.Id, "Dupont");

        var overflow = await BookOnAllotmentAsync(harness, harness.RoomIds[1], allotment.Id, "Martin");

        Assert.False(overflow.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, overflow.ErrorType);
    }

    // ============================ Release, annulation, bornes ============================

    [Fact]
    public async Task Passee_la_date_de_release_le_bloc_ne_tient_plus_les_chambres()
    {
        // La date de release est dans le PASSE : le solde est deja rendu, les 4 chambres doivent
        // etre vendables meme si le bloc existe toujours.
        await using var harness = await HarnessAsync();

        await CreateAllotmentAsync(
            harness,
            "GRP-1",
            roomsHeld: 3,
            releaseDate: DateOnly.FromDateTime(DateTime.Today).AddDays(-1));

        var availability = await harness.Lodging.GetAvailabilityAsync(
            Unit, Arrival, Departure, 2, null, CancellationToken.None);

        Assert.Equal(4, availability.Value!.Rooms.Count);
    }

    [Fact]
    public async Task Liberer_un_bloc_rend_le_solde_a_la_vente()
    {
        await using var harness = await HarnessAsync();

        var allotment = await CreateAllotmentAsync(harness, "GRP-1", roomsHeld: 3);

        var released = await harness.Mice.ReleaseAllotmentAsync(allotment.Id, Context, CancellationToken.None);
        Assert.True(released.Succeeded, released.Error);

        var availability = await harness.Lodging.GetAvailabilityAsync(
            Unit, Arrival, Departure, 2, null, CancellationToken.None);

        Assert.Equal(4, availability.Value!.Rooms.Count);
    }

    [Fact]
    public async Task Liberer_un_bloc_ne_desengage_pas_les_chambres_deja_prises()
    {
        await using var harness = await HarnessAsync();

        var allotment = await CreateAllotmentAsync(harness, "GRP-1", roomsHeld: 3);
        await BookOnAllotmentAsync(harness, harness.RoomIds[0], allotment.Id, "Dupont");

        await harness.Mice.ReleaseAllotmentAsync(allotment.Id, Context, CancellationToken.None);

        // La chambre du groupe reste occupee : liberer le bloc rend le SOLDE, pas les nuitees
        // vendues. Il reste donc 3 chambres libres, pas 4.
        var availability = await harness.Lodging.GetAvailabilityAsync(
            Unit, Arrival, Departure, 2, null, CancellationToken.None);

        Assert.Equal(3, availability.Value!.Rooms.Count);
    }

    [Fact]
    public async Task Un_bloc_portant_des_reservations_ne_peut_pas_etre_annule()
    {
        await using var harness = await HarnessAsync();

        var allotment = await CreateAllotmentAsync(harness, "GRP-1", roomsHeld: 3);
        await BookOnAllotmentAsync(harness, harness.RoomIds[0], allotment.Id, "Dupont");

        var cancelled = await harness.Mice.CancelAllotmentAsync(
            allotment.Id,
            new CancelRoomAllotmentRequest("Groupe desiste"),
            Context,
            CancellationToken.None);

        Assert.False(cancelled.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, cancelled.ErrorType);
    }

    [Fact]
    public async Task Un_bloc_ne_peut_pas_etre_reduit_sous_ce_qui_est_deja_pris()
    {
        await using var harness = await HarnessAsync();

        var allotment = await CreateAllotmentAsync(harness, "GRP-1", roomsHeld: 3);
        await BookOnAllotmentAsync(harness, harness.RoomIds[0], allotment.Id, "Dupont");
        await BookOnAllotmentAsync(harness, harness.RoomIds[1], allotment.Id, "Martin");

        var shrunk = await harness.Mice.UpdateAllotmentAsync(
            allotment.Id,
            new UpdateRoomAllotmentRequest(Arrival, Departure, 1, null, null),
            Context,
            CancellationToken.None);

        Assert.False(shrunk.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, shrunk.ErrorType);
    }

    [Fact]
    public async Task Un_bloc_plus_grand_que_le_parc_est_refuse()
    {
        // Tenir 30 chambres dans un type qui n'en compte que 4 gelerait tout l'inventaire sans que
        // personne comprenne pourquoi.
        await using var harness = await HarnessAsync();

        var result = await harness.Mice.CreateAllotmentAsync(
            new CreateRoomAllotmentRequest(
                Unit, "GRP-XXL", GroupCustomer, TypeCode, Arrival, Departure, 30, null, null),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);
        Assert.Contains("4", result.Error);
    }

    [Fact]
    public async Task Une_date_de_release_posterieure_a_l_arrivee_est_refusee()
    {
        await using var harness = await HarnessAsync();

        var result = await harness.Mice.CreateAllotmentAsync(
            new CreateRoomAllotmentRequest(
                Unit, "GRP-1", GroupCustomer, TypeCode, Arrival, Departure, 2, Arrival.AddDays(1), null),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task Un_sejour_qui_sort_des_dates_du_bloc_est_refuse()
    {
        await using var harness = await HarnessAsync();

        var allotment = await CreateAllotmentAsync(harness, "GRP-1", roomsHeld: 3);

        var outside = await harness.Lodging.CreateReservationAsync(
            new CreateReservationRequest(
                Unit, harness.RoomIds[0], GroupCustomer, Arrival.AddDays(-1), Departure, 2, allotment.Id, "Dupont"),
            Context,
            CancellationToken.None);

        Assert.False(outside.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, outside.ErrorType);
    }

    // ================================ Rooming lists ================================

    [Fact]
    public async Task Une_rooming_list_loge_les_occupants_sur_le_bloc()
    {
        await using var harness = await HarnessAsync();

        var allotment = await CreateAllotmentAsync(harness, "GRP-1", roomsHeld: 3);

        var result = await harness.Mice.SubmitRoomingListAsync(
            allotment.Id,
            [
                new RoomingListEntryRequest("Dupont Amine", 2, null, null),
                new RoomingListEntryRequest("Martin Sara", 1, null, null)
            ],
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(2, result.Value!.Entries.Count);
        Assert.Empty(result.Value.Rejected);

        // Les noms sont bien portes par les reservations : c'est tout l'objet d'une rooming list.
        Assert.Contains(result.Value.Entries, entry => entry.GuestName == "Dupont Amine");

        Assert.Equal(2, result.Value.Allotment.PickedUpPeak);
        Assert.Equal(1, result.Value.Allotment.RemainingAtPeak);
    }

    [Fact]
    public async Task Une_rooming_list_trop_longue_loge_ce_qu_elle_peut_et_dit_le_reste()
    {
        // Echouer en bloc sur un groupe de quarante personnes parce que la derniere ligne ne passe
        // pas serait pire que de loger les trente-neuf autres.
        await using var harness = await HarnessAsync();

        var allotment = await CreateAllotmentAsync(harness, "GRP-1", roomsHeld: 2);

        var result = await harness.Mice.SubmitRoomingListAsync(
            allotment.Id,
            [
                new RoomingListEntryRequest("Premier", 2, null, null),
                new RoomingListEntryRequest("Deuxieme", 2, null, null),
                new RoomingListEntryRequest("Troisieme", 2, null, null)
            ],
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(2, result.Value!.Entries.Count);

        var rejection = Assert.Single(result.Value.Rejected);
        Assert.Contains("Troisieme", rejection);
    }

    [Fact]
    public async Task Une_ligne_sans_nom_est_ecartee_sans_bloquer_les_autres()
    {
        await using var harness = await HarnessAsync();

        var allotment = await CreateAllotmentAsync(harness, "GRP-1", roomsHeld: 3);

        var result = await harness.Mice.SubmitRoomingListAsync(
            allotment.Id,
            [
                new RoomingListEntryRequest("  ", 2, null, null),
                new RoomingListEntryRequest("Valide", 2, null, null)
            ],
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Single(result.Value!.Entries);
        Assert.Single(result.Value.Rejected);
    }

    [Fact]
    public async Task Un_bloc_cloture_n_accepte_plus_de_rooming_list()
    {
        await using var harness = await HarnessAsync();

        var allotment = await CreateAllotmentAsync(harness, "GRP-1", roomsHeld: 3);
        await harness.Mice.ReleaseAllotmentAsync(allotment.Id, Context, CancellationToken.None);

        var result = await harness.Mice.SubmitRoomingListAsync(
            allotment.Id,
            [new RoomingListEntryRequest("Dupont", 2, null, null)],
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, result.ErrorType);
    }

    // ================================== Harnais ==================================

    private static async Task<RoomAllotmentResponse> CreateAllotmentAsync(
        Harness harness,
        string reference,
        int roomsHeld,
        DateOnly? releaseDate = null)
    {
        var result = await harness.Mice.CreateAllotmentAsync(
            new CreateRoomAllotmentRequest(
                Unit, reference, GroupCustomer, TypeCode, Arrival, Departure, roomsHeld, releaseDate, null),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);

        return result.Value!;
    }

    private static Task<ApplicationResult<ReservationResponse>> BookPublicAsync(Harness harness, Guid roomId)
    {
        return harness.Lodging.CreateReservationAsync(
            new CreateReservationRequest(Unit, roomId, PublicCustomer, Arrival, Departure, 2),
            Context,
            CancellationToken.None);
    }

    private static Task<ApplicationResult<ReservationResponse>> BookOnAllotmentAsync(
        Harness harness,
        Guid roomId,
        Guid allotmentId,
        string guestName)
    {
        return harness.Lodging.CreateReservationAsync(
            new CreateReservationRequest(
                Unit, roomId, GroupCustomer, Arrival, Departure, 2, allotmentId, guestName),
            Context,
            CancellationToken.None);
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

        dbContext.HotelUnits.Add(new HotelUnit(Unit, "Hotel Test", HotelUnitType.Hotel));
        dbContext.Customers.Add(new Customer(GroupCustomer, "Agence Voyages", CustomerType.Company));
        dbContext.Customers.Add(new Customer(PublicCustomer, "Client Direct", CustomerType.Individual));
        dbContext.Set<RoomType>().Add(new RoomType(Unit, TypeCode, "Double standard", 2));

        var rooms = new List<Room>();

        for (var index = 1; index <= 4; index++)
        {
            var room = new Room(Unit, $"10{index}", TypeCode);
            rooms.Add(room);
            dbContext.Set<Room>().Add(room);
        }

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var auditWriter = new AuditLogWriter(dbContext);

        var lodging = new LodgingService(dbContext, auditWriter, new StubTariffResolutionService());

        var billing = new BillingService(
            dbContext,
            auditWriter,
            new ApplicationSettingsService(dbContext, auditWriter));

        var mice = new MiceService(dbContext, billing, lodging);

        return new Harness(connection, dbContext, lodging, mice, rooms.Select(room => room.Id).ToList());
    }

    private sealed class Harness(
        SqliteConnection connection,
        RaqmiDbContext dbContext,
        LodgingService lodging,
        MiceService mice,
        IReadOnlyList<Guid> roomIds) : IAsyncDisposable
    {
        public RaqmiDbContext DbContext { get; } = dbContext;

        public LodgingService Lodging { get; } = lodging;

        public MiceService Mice { get; } = mice;

        public IReadOnlyList<Guid> RoomIds { get; } = roomIds;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
