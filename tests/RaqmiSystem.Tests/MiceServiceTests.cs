using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Mice;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Mice;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Billing;
using RaqmiSystem.Infrastructure.Lodging;
using RaqmiSystem.Infrastructure.Mice;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Settings;

namespace RaqmiSystem.Tests;

/// <summary>
/// Couverture du module 10.6, volet evenementiel, contre une base SQLite ":memory:" dediee par test.
///
/// Les cas retenus visent ce qui ferait perdre de l'argent ou la face a un hotel : une salle vendue
/// deux fois, un demontage ignore qui envoie un client dans une salle encore en train d'etre
/// debarrassee, un devis qui derive apres la facture, une double facturation, ou une annulation qui
/// laisserait une facture orpheline.
/// </summary>
public sealed class MiceServiceTests
{
    private const string Unit = "HTL1";

    private const string Ballroom = "SALLE1";

    private const string MeetingRoom = "SALLE2";

    private const string CustomerCode = "CLI1";

    private static readonly DateOnly EventDay = new(2030, 6, 15);

    private static readonly OperationContext Context = new(null, "commercial", "127.0.0.1");

    // --------------------------- Le coeur : une salle, un evenement ---------------------------

    [Fact]
    public async Task Un_evenement_est_cree_en_brouillon_et_tient_deja_la_salle()
    {
        await using var harness = await HarnessAsync();

        var result = await harness.Service.CreateEventAsync(
            Request("EVT-1", Ballroom, new TimeOnly(9, 0), 240),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);

        // Un devis est une OPTION sur la salle : il la tient des le brouillon, sinon deux
        // commerciaux vendraient le meme samedi soir.
        Assert.Equal(nameof(EventBookingStatus.Draft), result.Value!.Status);
        Assert.Equal(Ballroom, result.Value.FunctionSpaceCode);
    }

    [Fact]
    public async Task Deux_evenements_qui_se_chevauchent_dans_la_meme_salle_sont_refuses()
    {
        await using var harness = await HarnessAsync();

        await harness.Service.CreateEventAsync(
            Request("EVT-1", Ballroom, new TimeOnly(9, 0), 240),
            Context,
            CancellationToken.None);

        var second = await harness.Service.CreateEventAsync(
            Request("EVT-2", Ballroom, new TimeOnly(11, 0), 240),
            Context,
            CancellationToken.None);

        Assert.False(second.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, second.ErrorType);
        Assert.Equal(1, await harness.DbContext.EventBookings.CountAsync());
    }

    [Fact]
    public async Task Le_demontage_du_premier_evenement_bloque_le_montage_du_suivant()
    {
        // LE CAS QUI JUSTIFIE TOUT LE MODELE. Vu des invites, les deux creneaux ne se touchent
        // pas : le premier finit a 12:00, le second commence a 13:00. Mais le premier demande 90
        // minutes de demontage et le second 60 de montage : la salle est en fait disputee entre
        // 12:00 et 13:30. Un systeme qui ne compare que les heures affichees accepterait, et le
        // second client entrerait dans une salle encore en cours de debarrassage.
        await using var harness = await HarnessAsync();

        var first = await harness.Service.CreateEventAsync(
            Request("EVT-1", Ballroom, new TimeOnly(9, 0), 180, setupMinutes: 60, teardownMinutes: 90),
            Context,
            CancellationToken.None);

        Assert.True(first.Succeeded, first.Error);

        var second = await harness.Service.CreateEventAsync(
            Request("EVT-2", Ballroom, new TimeOnly(13, 0), 120, setupMinutes: 60, teardownMinutes: 0),
            Context,
            CancellationToken.None);

        Assert.False(second.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, second.ErrorType);
    }

    [Fact]
    public async Task Deux_creneaux_qui_se_succedent_exactement_sont_acceptes()
    {
        // La borne est stricte : un demontage qui finit a 12:00 et un montage qui commence a 12:00
        // ne se chevauchent pas. Sans cela, une salle ne pourrait jamais enchainer deux evenements.
        await using var harness = await HarnessAsync();

        var first = await harness.Service.CreateEventAsync(
            Request("EVT-1", Ballroom, new TimeOnly(9, 0), 120, setupMinutes: 0, teardownMinutes: 60),
            Context,
            CancellationToken.None);

        Assert.True(first.Succeeded, first.Error);

        var second = await harness.Service.CreateEventAsync(
            Request("EVT-2", Ballroom, new TimeOnly(12, 0), 120, setupMinutes: 0, teardownMinutes: 0),
            Context,
            CancellationToken.None);

        Assert.True(second.Succeeded, second.Error);
    }

    [Fact]
    public async Task Deux_salles_differentes_ne_se_genent_pas()
    {
        await using var harness = await HarnessAsync();

        await harness.Service.CreateEventAsync(
            Request("EVT-1", Ballroom, new TimeOnly(9, 0), 240),
            Context,
            CancellationToken.None);

        var second = await harness.Service.CreateEventAsync(
            Request("EVT-2", MeetingRoom, new TimeOnly(9, 0), 240),
            Context,
            CancellationToken.None);

        Assert.True(second.Succeeded, second.Error);
    }

    [Fact]
    public async Task Un_evenement_annule_libere_la_salle()
    {
        await using var harness = await HarnessAsync();

        var first = await harness.Service.CreateEventAsync(
            Request("EVT-1", Ballroom, new TimeOnly(9, 0), 240),
            Context,
            CancellationToken.None);

        await harness.Service.CancelEventAsync(
            first.Value!.Id,
            new CancelEventBookingRequest("Client desiste"),
            Context,
            CancellationToken.None);

        var second = await harness.Service.CreateEventAsync(
            Request("EVT-2", Ballroom, new TimeOnly(9, 0), 240),
            Context,
            CancellationToken.None);

        Assert.True(second.Succeeded, second.Error);
    }

    [Fact]
    public async Task Deplacer_un_evenement_ne_le_met_pas_en_conflit_avec_lui_meme()
    {
        // Allonger un evenement de trente minutes ne doit pas buter sur sa propre ligne : le garde
        // s'exclut lui-meme, sans quoi aucun evenement ne pourrait jamais etre modifie.
        await using var harness = await HarnessAsync();

        var created = await harness.Service.CreateEventAsync(
            Request("EVT-1", Ballroom, new TimeOnly(9, 0), 240),
            Context,
            CancellationToken.None);

        var moved = await harness.Service.RescheduleEventAsync(
            created.Value!.Id,
            new RescheduleEventBookingRequest(Ballroom, EventDay, new TimeOnly(9, 0), 270, 30, 30),
            Context,
            CancellationToken.None);

        Assert.True(moved.Succeeded, moved.Error);
        Assert.Equal(270, moved.Value!.DurationMinutes);
    }

    [Fact]
    public async Task Deplacer_un_evenement_sur_un_creneau_occupe_est_refuse()
    {
        await using var harness = await HarnessAsync();

        var first = await harness.Service.CreateEventAsync(
            Request("EVT-1", Ballroom, new TimeOnly(9, 0), 120),
            Context,
            CancellationToken.None);

        await harness.Service.CreateEventAsync(
            Request("EVT-2", Ballroom, new TimeOnly(15, 0), 120),
            Context,
            CancellationToken.None);

        var moved = await harness.Service.RescheduleEventAsync(
            first.Value!.Id,
            new RescheduleEventBookingRequest(Ballroom, EventDay, new TimeOnly(15, 30), 60, 0, 0),
            Context,
            CancellationToken.None);

        Assert.False(moved.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, moved.ErrorType);
    }

    // ------------------------------- Garde-fous d'exploitation -------------------------------

    [Fact]
    public async Task Un_effectif_superieur_a_la_capacite_de_la_salle_est_refuse()
    {
        await using var harness = await HarnessAsync();

        var request = Request("EVT-1", MeetingRoom, new TimeOnly(9, 0), 120) with { ExpectedAttendance = 400 };

        var result = await harness.Service.CreateEventAsync(request, Context, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);

        // Le refus NOMME la capacite reelle : l'utilisateur doit savoir quelle salle chercher.
        Assert.Contains("30", result.Error);
    }

    [Fact]
    public async Task Une_reference_deja_utilisee_dans_l_unite_est_refusee()
    {
        await using var harness = await HarnessAsync();

        await harness.Service.CreateEventAsync(
            Request("EVT-1", Ballroom, new TimeOnly(9, 0), 120),
            Context,
            CancellationToken.None);

        var duplicate = await harness.Service.CreateEventAsync(
            Request("EVT-1", MeetingRoom, new TimeOnly(9, 0), 120),
            Context,
            CancellationToken.None);

        Assert.False(duplicate.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, duplicate.ErrorType);
    }

    [Fact]
    public async Task Une_salle_desactivee_n_accepte_plus_de_nouvel_evenement()
    {
        await using var harness = await HarnessAsync();

        await harness.Service.SetFunctionSpaceActiveAsync(Unit, Ballroom, false, Context, CancellationToken.None);

        var result = await harness.Service.CreateEventAsync(
            Request("EVT-1", Ballroom, new TimeOnly(9, 0), 120),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task Desactiver_une_salle_ne_touche_pas_aux_evenements_deja_places()
    {
        // Annuler le mariage d'un client parce qu'une salle a ete archivee serait bien pire qu'une
        // salle inactive portant encore un evenement.
        await using var harness = await HarnessAsync();

        var created = await harness.Service.CreateEventAsync(
            Request("EVT-1", Ballroom, new TimeOnly(9, 0), 120),
            Context,
            CancellationToken.None);

        await harness.Service.SetFunctionSpaceActiveAsync(Unit, Ballroom, false, Context, CancellationToken.None);

        var reloaded = await harness.Service.GetEventAsync(created.Value!.Id, CancellationToken.None);

        Assert.True(reloaded.Succeeded);
        Assert.Equal(nameof(EventBookingStatus.Draft), reloaded.Value!.Status);
    }

    // ------------------------------------ Devis et BEO ------------------------------------

    [Fact]
    public async Task Les_lignes_chiffrees_donnent_les_totaux_du_devis()
    {
        await using var harness = await HarnessAsync();

        var created = await harness.Service.CreateEventAsync(
            Request("EVT-1", Ballroom, new TimeOnly(9, 0), 240),
            Context,
            CancellationToken.None);

        var priced = await harness.Service.ReplaceEventLinesAsync(
            created.Value!.Id,
            [
                new EventBookingLineRequest("Location de salle", 1m, 60_000m, 19m),
                new EventBookingLineRequest("Pause cafe", 80m, 500m, 9m)
            ],
            Context,
            CancellationToken.None);

        Assert.True(priced.Succeeded, priced.Error);
        Assert.Equal(100_000m, priced.Value!.TotalExclVat);

        // 60 000 a 19 % = 11 400 ; 40 000 a 9 % = 3 600.
        Assert.Equal(15_000m, priced.Value.TotalVat);
        Assert.Equal(115_000m, priced.Value.TotalInclVat);
    }

    [Fact]
    public async Task Un_taux_de_tva_hors_bareme_est_refuse_des_le_devis()
    {
        // Un devis portant un taux que la facture refuserait serait impossible a facturer ensuite.
        await using var harness = await HarnessAsync();

        var created = await harness.Service.CreateEventAsync(
            Request("EVT-1", Ballroom, new TimeOnly(9, 0), 120),
            Context,
            CancellationToken.None);

        var priced = await harness.Service.ReplaceEventLinesAsync(
            created.Value!.Id,
            [new EventBookingLineRequest("Prestation", 1m, 1_000m, 12m)],
            Context,
            CancellationToken.None);

        Assert.False(priced.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, priced.ErrorType);
    }

    [Fact]
    public async Task Le_deroule_BEO_est_rendu_trie_par_heure()
    {
        await using var harness = await HarnessAsync();

        var created = await harness.Service.CreateEventAsync(
            Request("EVT-1", Ballroom, new TimeOnly(8, 0), 480),
            Context,
            CancellationToken.None);

        var withBeo = await harness.Service.ReplaceEventScheduleAsync(
            created.Value!.Id,
            [
                new EventScheduleItemRequest(new TimeOnly(12, 30), "Service du dejeuner", "Cuisine"),
                new EventScheduleItemRequest(new TimeOnly(8, 0), "Mise en place de la salle", "Etage"),
                new EventScheduleItemRequest(new TimeOnly(10, 30), "Pause cafe", "Restauration")
            ],
            Context,
            CancellationToken.None);

        Assert.True(withBeo.Succeeded, withBeo.Error);

        var times = withBeo.Value!.Schedule.Select(item => item.StartTime).ToList();
        Assert.Equal([new TimeOnly(8, 0), new TimeOnly(10, 30), new TimeOnly(12, 30)], times);
    }

    // -------------------------- Facturation evenementielle --------------------------

    [Fact]
    public async Task Un_evenement_confirme_et_chiffre_produit_une_facture_brouillon()
    {
        await using var harness = await HarnessAsync();

        var id = await ArrangeInvoiceableEventAsync(harness);

        var invoiced = await harness.Service.InvoiceEventAsync(id, Context, CancellationToken.None);

        Assert.True(invoiced.Succeeded, invoiced.Error);
        Assert.NotNull(invoiced.Value!.InvoiceId);

        // La facture est bien produite par le module Facturation, avec ses propres lignes.
        var invoice = await harness.DbContext.Invoices
            .Include(item => item.Lines)
            .SingleAsync(item => item.Id == invoiced.Value.InvoiceId);

        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal(2, invoice.Lines.Count);
        Assert.Equal(invoiced.Value.TotalExclVat, invoice.TotalExclVat);
    }

    [Fact]
    public async Task Un_evenement_non_confirme_ne_peut_pas_etre_facture()
    {
        await using var harness = await HarnessAsync();

        var created = await harness.Service.CreateEventAsync(
            Request("EVT-1", Ballroom, new TimeOnly(9, 0), 240),
            Context,
            CancellationToken.None);

        await harness.Service.ReplaceEventLinesAsync(
            created.Value!.Id,
            [new EventBookingLineRequest("Location de salle", 1m, 60_000m, 19m)],
            Context,
            CancellationToken.None);

        var invoiced = await harness.Service.InvoiceEventAsync(created.Value.Id, Context, CancellationToken.None);

        Assert.False(invoiced.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, invoiced.ErrorType);
        Assert.Equal(0, await harness.DbContext.Invoices.CountAsync());
    }

    [Fact]
    public async Task Facturer_deux_fois_le_meme_evenement_est_refuse()
    {
        await using var harness = await HarnessAsync();

        var id = await ArrangeInvoiceableEventAsync(harness);

        await harness.Service.InvoiceEventAsync(id, Context, CancellationToken.None);
        var second = await harness.Service.InvoiceEventAsync(id, Context, CancellationToken.None);

        Assert.False(second.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, second.ErrorType);

        // Une seule facture : c'est tout l'objet du garde.
        Assert.Equal(1, await harness.DbContext.Invoices.CountAsync());
    }

    [Fact]
    public async Task Le_devis_est_gele_des_que_l_evenement_est_facture()
    {
        // Laisser le devis deriver apres la facture laisserait deux versions contradictoires de ce
        // que le client doit.
        await using var harness = await HarnessAsync();

        var id = await ArrangeInvoiceableEventAsync(harness);
        await harness.Service.InvoiceEventAsync(id, Context, CancellationToken.None);

        var changed = await harness.Service.ReplaceEventLinesAsync(
            id,
            [new EventBookingLineRequest("Remise commerciale", 1m, 1m, 19m)],
            Context,
            CancellationToken.None);

        Assert.False(changed.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, changed.ErrorType);
    }

    [Fact]
    public async Task Le_BEO_reste_modifiable_apres_facturation()
    {
        // Le document commercial est fige, pas l'operation : la cuisine peut encore deplacer une
        // pause cafe le matin meme.
        await using var harness = await HarnessAsync();

        var id = await ArrangeInvoiceableEventAsync(harness);
        await harness.Service.InvoiceEventAsync(id, Context, CancellationToken.None);

        var beo = await harness.Service.ReplaceEventScheduleAsync(
            id,
            [new EventScheduleItemRequest(new TimeOnly(11, 0), "Pause cafe avancee", "Restauration")],
            Context,
            CancellationToken.None);

        Assert.True(beo.Succeeded, beo.Error);
        Assert.Single(beo.Value!.Schedule);
    }

    [Fact]
    public async Task Un_evenement_facture_ne_peut_pas_etre_annule_sans_annuler_la_facture()
    {
        await using var harness = await HarnessAsync();

        var id = await ArrangeInvoiceableEventAsync(harness);
        await harness.Service.InvoiceEventAsync(id, Context, CancellationToken.None);

        var cancelled = await harness.Service.CancelEventAsync(
            id,
            new CancelEventBookingRequest("Client desiste"),
            Context,
            CancellationToken.None);

        Assert.False(cancelled.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, cancelled.ErrorType);
    }

    [Fact]
    public async Task Un_evenement_annule_ne_peut_plus_etre_modifie()
    {
        await using var harness = await HarnessAsync();

        var created = await harness.Service.CreateEventAsync(
            Request("EVT-1", Ballroom, new TimeOnly(9, 0), 120),
            Context,
            CancellationToken.None);

        await harness.Service.CancelEventAsync(
            created.Value!.Id,
            new CancelEventBookingRequest("Salle inondee"),
            Context,
            CancellationToken.None);

        var updated = await harness.Service.UpdateEventAsync(
            created.Value.Id,
            new UpdateEventBookingRequest("Nouveau titre", nameof(EventSetupStyle.Theatre), 50, null),
            Context,
            CancellationToken.None);

        Assert.False(updated.Succeeded);
    }

    // ------------------------------------ Harnais ------------------------------------

    private static CreateEventBookingRequest Request(
        string reference,
        string spaceCode,
        TimeOnly startTime,
        int durationMinutes,
        int setupMinutes = 0,
        int teardownMinutes = 0)
    {
        return new CreateEventBookingRequest(
            Unit,
            reference,
            spaceCode,
            CustomerCode,
            "Seminaire annuel",
            EventDay,
            startTime,
            durationMinutes,
            setupMinutes,
            teardownMinutes,
            nameof(EventSetupStyle.Theatre),
            25,
            null);
    }

    private static async Task<Guid> ArrangeInvoiceableEventAsync(Harness harness)
    {
        var created = await harness.Service.CreateEventAsync(
            Request("EVT-1", Ballroom, new TimeOnly(9, 0), 240),
            Context,
            CancellationToken.None);

        await harness.Service.ReplaceEventLinesAsync(
            created.Value!.Id,
            [
                new EventBookingLineRequest("Location de salle", 1m, 60_000m, 19m),
                new EventBookingLineRequest("Pause cafe", 80m, 500m, 9m)
            ],
            Context,
            CancellationToken.None);

        await harness.Service.ConfirmEventAsync(created.Value.Id, Context, CancellationToken.None);

        return created.Value.Id;
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
        dbContext.Customers.Add(new Customer(CustomerCode, "Societe Cliente", CustomerType.Company));

        dbContext.FunctionSpaces.Add(new FunctionSpace(Unit, Ballroom, "Grand salon", 300, 420m));
        dbContext.FunctionSpaces.Add(new FunctionSpace(Unit, MeetingRoom, "Salle de reunion", 30, 45m));

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var auditWriter = new AuditLogWriter(dbContext);

        var billingService = new BillingService(
            dbContext,
            auditWriter,
            new ApplicationSettingsService(dbContext, auditWriter));

        // MiceService consomme desormais ILodgingService : le volet groupes prend ses chambres
        // par le meme chemin qu'une reservation individuelle.
        var lodgingService = new LodgingService(dbContext, auditWriter, new StubTariffResolutionService());

        return new Harness(connection, dbContext, new MiceService(dbContext, billingService, lodgingService));
    }

    private sealed class Harness(
        SqliteConnection connection,
        RaqmiDbContext dbContext,
        MiceService service) : IAsyncDisposable
    {
        public RaqmiDbContext DbContext { get; } = dbContext;

        public MiceService Service { get; } = service;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
