using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Lodging;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Tariffs;

namespace RaqmiSystem.Tests;

/// <summary>
/// Couverture du parametrage des chambres et du couchage.
///
/// L'INVARIANT CENTRAL : une composition de couchage doit coucher EXACTEMENT la capacite du type.
/// La recherche de disponibilite compare le nombre de personnes a cette capacite ; un type declare
/// pour deux mais compose de quatre couchages ferait vendre une chambre pour deux a quatre
/// personnes, ou l'inverse. Les deux erreurs se paient a la reception.
///
/// Ce fichier verrouille aussi trois corrections apportees au passage : l'etage, les notes et la
/// description etaient portes par les requetes mais silencieusement ignores par le service, a la
/// creation comme a la mise a jour.
/// </summary>
public sealed class BedConfigurationTests
{
    private const string Unit = "HTL1";

    private static readonly OperationContext Context = new(null, "gouvernante", "127.0.0.1");

    // ============================ Composition et capacite ============================

    [Fact]
    public async Task Un_type_accepte_une_composition_qui_couche_sa_capacite()
    {
        await using var harness = await HarnessAsync();

        var result = await harness.Service.CreateRoomTypeAsync(
            new CreateRoomTypeRequest(
                Unit, "TWIN", "Twin standard", 2, "Deux lits simples",
                [new BedCompositionLine(nameof(BedType.Single), 2)]),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(2, result.Value!.DeclaredSleeps);
        Assert.Equal(2, result.Value.Capacity);

        var line = Assert.Single(result.Value.Beds);
        Assert.Equal(nameof(BedType.Single), line.BedType);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public async Task Un_type_refuse_une_composition_qui_ne_couche_pas_sa_capacite()
    {
        await using var harness = await HarnessAsync();

        // Capacite 2 mais deux lits doubles : quatre couchages. C'est l'erreur qui ferait vendre
        // une chambre pour deux a quatre personnes.
        var result = await harness.Service.CreateRoomTypeAsync(
            new CreateRoomTypeRequest(
                Unit, "FAUX", "Incoherent", 2, null,
                [new BedCompositionLine(nameof(BedType.Double), 2)]),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);

        // Le refus NOMME les deux chiffres : l'utilisateur doit savoir lequel corriger.
        Assert.Contains("4", result.Error);
        Assert.Contains("2", result.Error);
    }

    [Fact]
    public async Task Un_type_reste_valide_sans_couchage_declare()
    {
        // Les types crees avant l'arrivee du couchage n'en ont pas : ils doivent rester utilisables.
        await using var harness = await HarnessAsync();

        var result = await harness.Service.CreateRoomTypeAsync(
            new CreateRoomTypeRequest(Unit, "SANS", "Sans couchage declare", 3),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Empty(result.Value!.Beds);
        Assert.Equal(0, result.Value.DeclaredSleeps);
    }

    [Fact]
    public async Task Une_nature_de_lit_inconnue_est_refusee()
    {
        await using var harness = await HarnessAsync();

        var result = await harness.Service.CreateRoomTypeAsync(
            new CreateRoomTypeRequest(
                Unit, "HAMAC", "Hamac", 1, null,
                [new BedCompositionLine("Hamac", 1)]),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task Les_lits_d_appoint_augmentent_l_occupation_maximale_sans_toucher_la_capacite()
    {
        await using var harness = await HarnessAsync();

        var result = await harness.Service.CreateRoomTypeAsync(
            new CreateRoomTypeRequest(
                Unit, "FAM", "Familiale", 2, null,
                [new BedCompositionLine(nameof(BedType.Double), 1)],
                MaxExtraBeds: 2,
                MaxCots: 1),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);

        // La capacite reste 2 : c'est elle que la recherche de disponibilite compare.
        Assert.Equal(2, result.Value!.Capacity);
        Assert.Equal(2, result.Value.MaxExtraBeds);
        Assert.Equal(1, result.Value.MaxCots);

        // L'occupation maximale, elle, monte a 4. Les berceaux n'y entrent pas.
        Assert.Equal(4, result.Value.MaxOccupancy);
    }

    // ============================ Surcharge par chambre ============================

    [Fact]
    public async Task Une_chambre_sans_couchage_propre_herite_de_son_type()
    {
        await using var harness = await HarnessAsync();

        await CreateTypeAsync(harness, "TWIN", 2, [new BedCompositionLine(nameof(BedType.Single), 2)]);

        var room = await harness.Service.CreateRoomAsync(
            new CreateRoomRequest(Unit, "101", "TWIN"),
            Context,
            CancellationToken.None);

        Assert.True(room.Succeeded, room.Error);
        Assert.False(room.Value!.OverridesBeds);

        var line = Assert.Single(room.Value.Beds);
        Assert.Equal(nameof(BedType.Single), line.BedType);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public async Task Une_chambre_peut_changer_de_composition_a_capacite_egale()
    {
        // Le cas reel : la 102 est en lit double la ou le type est declare en deux lits simples.
        await using var harness = await HarnessAsync();

        await CreateTypeAsync(harness, "TWIN", 2, [new BedCompositionLine(nameof(BedType.Single), 2)]);

        var room = await harness.Service.CreateRoomAsync(
            new CreateRoomRequest(Unit, "102", "TWIN", Beds: [new BedCompositionLine(nameof(BedType.Double), 1)]),
            Context,
            CancellationToken.None);

        Assert.True(room.Succeeded, room.Error);
        Assert.True(room.Value!.OverridesBeds);

        var line = Assert.Single(room.Value.Beds);
        Assert.Equal(nameof(BedType.Double), line.BedType);
    }

    [Fact]
    public async Task Une_chambre_ne_peut_pas_coucher_plus_que_son_type()
    {
        // Autoriser cela rendrait la recherche de disponibilite fausse : elle raisonne sur la
        // capacite du TYPE, pas sur le couchage de chaque chambre.
        await using var harness = await HarnessAsync();

        await CreateTypeAsync(harness, "TWIN", 2, [new BedCompositionLine(nameof(BedType.Single), 2)]);

        var room = await harness.Service.CreateRoomAsync(
            new CreateRoomRequest(Unit, "103", "TWIN", Beds: [new BedCompositionLine(nameof(BedType.Single), 4)]),
            Context,
            CancellationToken.None);

        Assert.False(room.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, room.ErrorType);
    }

    [Fact]
    public async Task Vider_le_couchage_d_une_chambre_la_fait_retomber_sur_son_type()
    {
        await using var harness = await HarnessAsync();

        await CreateTypeAsync(harness, "TWIN", 2, [new BedCompositionLine(nameof(BedType.Single), 2)]);

        var created = await harness.Service.CreateRoomAsync(
            new CreateRoomRequest(Unit, "104", "TWIN", Beds: [new BedCompositionLine(nameof(BedType.Double), 1)]),
            Context,
            CancellationToken.None);

        Assert.True(created.Value!.OverridesBeds);

        var updated = await harness.Service.UpdateRoomAsync(
            created.Value.Id,
            new UpdateRoomRequest("TWIN", Beds: []),
            Context,
            CancellationToken.None);

        Assert.True(updated.Succeeded, updated.Error);
        Assert.False(updated.Value!.OverridesBeds);
        Assert.Equal(nameof(BedType.Single), Assert.Single(updated.Value.Beds).BedType);
    }

    // ==================== Champs autrefois ignores par le service ====================

    [Fact]
    public async Task L_etage_et_les_notes_sont_conserves_a_la_creation_d_une_chambre()
    {
        // Ils voyageaient dans la requete mais n'etaient jamais transmis au constructeur : la
        // saisie semblait disparaitre.
        await using var harness = await HarnessAsync();

        await CreateTypeAsync(harness, "DBL", 2, null);

        var room = await harness.Service.CreateRoomAsync(
            new CreateRoomRequest(Unit, "201", "DBL", "1er", "Vue mer"),
            Context,
            CancellationToken.None);

        Assert.True(room.Succeeded, room.Error);
        Assert.Equal("1er", room.Value!.Floor);
        Assert.Equal("Vue mer", room.Value.Notes);
    }

    [Fact]
    public async Task L_etage_et_les_notes_sont_conserves_a_la_mise_a_jour_d_une_chambre()
    {
        await using var harness = await HarnessAsync();

        await CreateTypeAsync(harness, "DBL", 2, null);

        var created = await harness.Service.CreateRoomAsync(
            new CreateRoomRequest(Unit, "202", "DBL"),
            Context,
            CancellationToken.None);

        var updated = await harness.Service.UpdateRoomAsync(
            created.Value!.Id,
            new UpdateRoomRequest("DBL", "2e", "Chambre communicante"),
            Context,
            CancellationToken.None);

        Assert.True(updated.Succeeded, updated.Error);
        Assert.Equal("2e", updated.Value!.Floor);
        Assert.Equal("Chambre communicante", updated.Value.Notes);
    }

    [Fact]
    public async Task La_description_d_un_type_est_conservee_a_la_creation_et_a_la_mise_a_jour()
    {
        await using var harness = await HarnessAsync();

        var created = await harness.Service.CreateRoomTypeAsync(
            new CreateRoomTypeRequest(Unit, "SUITE", "Suite", 2, "Salon separe"),
            Context,
            CancellationToken.None);

        Assert.True(created.Succeeded, created.Error);
        Assert.Equal("Salon separe", created.Value!.Description);

        var updated = await harness.Service.UpdateRoomTypeAsync(
            created.Value.Id,
            new UpdateRoomTypeRequest("Suite junior", 2, "Salon separe et terrasse"),
            Context,
            CancellationToken.None);

        Assert.True(updated.Succeeded, updated.Error);
        Assert.Equal("Salon separe et terrasse", updated.Value!.Description);
    }

    // ================================== Harnais ==================================

    private static async Task CreateTypeAsync(
        Harness harness,
        string code,
        int capacity,
        IReadOnlyCollection<BedCompositionLine>? beds)
    {
        var result = await harness.Service.CreateRoomTypeAsync(
            new CreateRoomTypeRequest(Unit, code, code, capacity, null, beds),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
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
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var auditWriter = new AuditLogWriter(dbContext);

        var service = new LodgingService(
            dbContext,
            auditWriter,
            new TariffResolutionService(dbContext));

        return new Harness(connection, dbContext, service);
    }

    private sealed class Harness(
        SqliteConnection connection,
        RaqmiDbContext dbContext,
        LodgingService service) : IAsyncDisposable
    {
        public RaqmiDbContext DbContext { get; } = dbContext;

        public LodgingService Service { get; } = service;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
