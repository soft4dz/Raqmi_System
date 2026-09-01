using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Tests;

/// <summary>
/// L'inventaire : hors service technique (OOO), hors service d'exploitation (OOS) et la politique
/// d'unite qui decide si le second retire ou non des chambres de la vente.
/// </summary>
public sealed class LodgingInventoryTests
{
    private static readonly DateOnly From = new(2031, 3, 10);
    private static readonly DateOnly To = new(2031, 3, 13);

    [Fact]
    public async Task Le_hors_service_technique_retire_toujours_la_chambre_de_l_inventaire()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 3);

        var before = await harness.AvailabilityForAsync(From, To);
        Assert.Equal(3, before.SellableCapacity);
        Assert.Equal(3, before.PublicAvailable);

        var block = await harness.Service.CreateRoomBlockAsync(
            PmsHarness.UnitCode,
            new CreateRoomBlockRequest(
                harness.StandardRooms[0].Id,
                RoomBlockKind.OutOfOrder,
                From,
                To,
                "Degat des eaux salle de bain.",
                RoomBlockCategory.Plumbing,
                "OT-2031-014"),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(block.Succeeded, block.Error);
        Assert.True(block.Value!.ReducesSellableInventory);

        var after = await harness.AvailabilityForAsync(From, To);

        Assert.Equal(2, after.SellableCapacity);
        Assert.Equal(2, after.PublicAvailable);
        Assert.All(after.Nights, night => Assert.Equal(1, night.BlockedRooms));
    }

    [Fact]
    public async Task Le_hors_service_d_exploitation_ne_retire_l_inventaire_que_si_la_politique_le_dit()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 3);

        var block = await harness.Service.CreateRoomBlockAsync(
            PmsHarness.UnitCode,
            new CreateRoomBlockRequest(
                harness.StandardRooms[0].Id,
                RoomBlockKind.OutOfService,
                From,
                To,
                "Logement d'un stagiaire pour la saison.",
                RoomBlockCategory.InternalUse),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(block.Succeeded, block.Error);

        // Politique par defaut : le hors service d'exploitation NE retire PAS l'inventaire
        // commercial. L'hotel assume de deplacer l'usage interne si un client se presente.
        Assert.False(block.Value!.ReducesSellableInventory);

        var permissive = await harness.AvailabilityForAsync(From, To);
        Assert.Equal(3, permissive.SellableCapacity);

        // La chambre reste hors de la liste des chambres AFFECTABLES : on ne propose pas d'y
        // installer un client, meme quand elle compte encore dans l'inventaire vendable.
        var search = await harness.Service.SearchAvailabilityAsync(
            new AvailabilitySearchRequest(PmsHarness.UnitCode, From, To, Adults: 2),
            CancellationToken.None);

        Assert.True(search.Succeeded, search.Error);
        Assert.DoesNotContain(search.Value!.Rooms, room => room.RoomId == harness.StandardRooms[0].Id);

        // L'hotel change d'avis : desormais le hors service d'exploitation retire l'inventaire.
        var policy = await harness.SavePolicyAsync(PmsHarness.DefaultPolicy(outOfServiceReducesInventory: true));
        Assert.True(policy.Succeeded, policy.Error);

        var strict = await harness.AvailabilityForAsync(From, To);
        Assert.Equal(2, strict.SellableCapacity);
    }

    [Fact]
    public async Task Une_chambre_bloquee_ne_peut_plus_etre_vendue_nommement()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 2);

        var block = await harness.Service.CreateRoomBlockAsync(
            PmsHarness.UnitCode,
            new CreateRoomBlockRequest(
                harness.StandardRooms[0].Id,
                RoomBlockKind.OutOfOrder,
                From,
                To,
                "Remplacement de la climatisation.",
                RoomBlockCategory.Hvac),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(block.Succeeded, block.Error);

        var refused = await harness.BookAsync(From, To, harness.StandardRooms[0].Id);

        Assert.False(refused.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, refused.ErrorType);
        Assert.Contains("hors service", refused.Error);

        // L'autre chambre reste vendable : le blocage porte sur UNE chambre, pas sur le type.
        var accepted = await harness.BookAsync(From, To, harness.StandardRooms[1].Id);
        Assert.True(accepted.Succeeded, accepted.Error);
    }

    [Fact]
    public async Task Bloquer_une_chambre_habitee_est_refuse()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 2);

        var stay = await harness.BookAsync(From, To, harness.StandardRooms[0].Id);
        Assert.True(stay.Succeeded, stay.Error);

        var refused = await harness.Service.CreateRoomBlockAsync(
            PmsHarness.UnitCode,
            new CreateRoomBlockRequest(
                harness.StandardRooms[0].Id,
                RoomBlockKind.OutOfOrder,
                From,
                To,
                "Travaux imprevus.",
                RoomBlockCategory.Renovation),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.False(refused.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, refused.ErrorType);
        Assert.Contains("porte un sejour", refused.Error);
    }

    [Fact]
    public async Task La_remise_en_service_rend_la_chambre_a_la_vente_et_la_declare_sale()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1);

        var block = await harness.Service.CreateRoomBlockAsync(
            PmsHarness.UnitCode,
            new CreateRoomBlockRequest(
                harness.StandardRooms[0].Id,
                RoomBlockKind.OutOfOrder,
                From,
                To,
                "Peinture.",
                RoomBlockCategory.Renovation),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(block.Succeeded, block.Error);
        Assert.Equal(0, (await harness.AvailabilityForAsync(From, To)).PublicAvailable);

        var closed = await harness.Service.CloseRoomBlockAsync(
            block.Value!.Id,
            new CloseRoomBlockRequest(From.AddDays(1)),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(closed.Succeeded, closed.Error);
        Assert.Equal(RoomBlockStatus.Closed, closed.Value!.Status);
        Assert.Equal(From.AddDays(1), closed.Value.ActualReturnDate);

        // La chambre revient a la vente ET repart en SALE : elle sort de travaux, pas de menage.
        Assert.Equal(1, (await harness.AvailabilityForAsync(From, To)).PublicAvailable);

        var condition = await harness.DbContext.Set<Domain.Housekeeping.RoomCondition>()
            .SingleAsync(current => current.RoomId == harness.StandardRooms[0].Id);

        Assert.Equal(Domain.Housekeeping.RoomConditionStatus.Dirty, condition.Status);
    }

    [Fact]
    public async Task Le_previsionnel_distingue_le_hors_service_technique_de_celui_d_exploitation()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 3, suites: 0);

        await harness.Service.CreateRoomBlockAsync(
            PmsHarness.UnitCode,
            new CreateRoomBlockRequest(
                harness.StandardRooms[0].Id,
                RoomBlockKind.OutOfOrder,
                From,
                To,
                "Panne.",
                RoomBlockCategory.Electrical),
            PmsHarness.Context,
            CancellationToken.None);

        await harness.Service.CreateRoomBlockAsync(
            PmsHarness.UnitCode,
            new CreateRoomBlockRequest(
                harness.StandardRooms[1].Id,
                RoomBlockKind.OutOfService,
                From,
                To,
                "Nettoyage approfondi.",
                RoomBlockCategory.DeepCleaning),
            PmsHarness.Context,
            CancellationToken.None);

        var forecast = await harness.Service.GetForecastAsync(
            PmsHarness.UnitCode,
            From,
            3,
            CancellationToken.None);

        Assert.True(forecast.Succeeded, forecast.Error);

        var day = forecast.Value!.Entries.First();

        Assert.Equal(3, day.PhysicalRooms);
        Assert.Equal(1, day.OutOfOrderRooms);
        Assert.Equal(1, day.OutOfServiceRooms);

        // Politique par defaut : seul le hors service TECHNIQUE est retire de la capacite vendable.
        Assert.Equal(2, day.SellableRooms);
    }

    [Fact]
    public async Task Le_previsionnel_rend_arrivees_departs_stay_over_et_les_indicateurs()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 4, suites: 0);

        // Un sejour de trois nuits : arrivee le 10, stay-over les 11 et 12, depart le 13.
        var stay = await harness.BookAsync(From, To, harness.StandardRooms[0].Id);
        Assert.True(stay.Succeeded, stay.Error);

        var forecast = await harness.Service.GetForecastAsync(
            PmsHarness.UnitCode,
            From,
            4,
            CancellationToken.None);

        Assert.True(forecast.Succeeded, forecast.Error);

        var entries = forecast.Value!.Entries.ToArray();

        Assert.Equal(1, entries[0].Arrivals);
        Assert.Equal(0, entries[0].StayOvers);
        Assert.Equal(1, entries[1].StayOvers);
        Assert.Equal(1, entries[3].Departures);
        Assert.Equal(0, entries[3].SoldRooms);

        // ADR = prix moyen de la nuitee vendue ; RevPAR = revenu par chambre disponible. Avec une
        // chambre vendue sur quatre exploitables, le RevPAR vaut le quart de l'ADR.
        Assert.Equal(PmsHarness.NightlyRate, entries[0].Adr);
        Assert.Equal(PmsHarness.NightlyRate / 4m, entries[0].RevPar);
        Assert.Equal(25.00m, entries[0].OccupancyPercent);
    }
}
