using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Tests;

/// <summary>
/// La surreservation controlee : capacite physique contre capacite commerciale, et la marque que
/// porte le dossier qui a franchi la premiere.
/// </summary>
public sealed class LodgingOverbookingTests
{
    private static readonly DateOnly From = new(2031, 5, 4);
    private static readonly DateOnly To = new(2031, 5, 6);

    [Fact]
    public void Le_calcul_separe_le_disponible_physique_du_disponible_commercial()
    {
        // Le cas de l'enonce : capacite physique 50, surreservation autorisee +2, tout vendu.
        var inventory = new NightInventory(From, PhysicalRooms: 50, BlockedRooms: 0, SoldRooms: 50, AllotmentHolds: 0, OverbookingAllowed: 2);

        Assert.Equal(50, inventory.SellableCapacity);
        Assert.Equal(0, inventory.PhysicalAvailable);
        Assert.Equal(0, inventory.PublicAvailable);
        Assert.Equal(2, inventory.OverbookingRemaining);
        Assert.Equal(2, inventory.CommercialAvailable);
        Assert.Equal(0, inventory.OverbookingUsed);
        Assert.True(inventory.NextSaleIsOverbooking);

        // Une chambre vendue au-dela : le solde de surreservation baisse, l'usage monte.
        var used = inventory with { SoldRooms = 51 };

        Assert.Equal(1, used.OverbookingUsed);
        Assert.Equal(1, used.OverbookingRemaining);
        Assert.Equal(1, used.CommercialAvailable);
    }

    [Fact]
    public async Task Sans_autorisation_la_vente_s_arrete_a_la_derniere_chambre_physique()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);

        var first = await harness.BookAsync(From, To, harness.StandardRooms[0].Id);
        Assert.True(first.Succeeded, first.Error);

        // Vente par TYPE, sans chambre nommee : il n'en reste aucune.
        var refused = await harness.BookAsync(From, To);

        Assert.False(refused.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, refused.ErrorType);
        Assert.Contains("disponible", refused.Error);
    }

    [Fact]
    public async Task Une_autorisation_ouvre_exactement_le_nombre_de_chambres_declare()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);

        await harness.SavePolicyAsync(PmsHarness.DefaultPolicy(overbookingEnabled: true));

        var allowance = await harness.Service.CreateOverbookingAsync(
            new SaveOverbookingAllowanceRequest(
                PmsHarness.UnitCode,
                PmsHarness.StandardType,
                From,
                To,
                ExtraRooms: 2,
                "Pont du 1er mai : on parie sur les annulations."),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(allowance.Succeeded, allowance.Error);

        var sold = await harness.BookAsync(From, To, harness.StandardRooms[0].Id);
        Assert.True(sold.Succeeded, sold.Error);
        Assert.False(sold.Value!.IsOverbooking);

        var availability = await harness.AvailabilityForAsync(From, To, allowOverbooking: true);

        Assert.Equal(0, availability.PublicAvailable);
        Assert.Equal(2, availability.CommercialAvailable);
        Assert.True(availability.RequiresOverbooking);

        // Les deux ventes en surreservation passent, ET SONT MARQUEES : la reception doit pouvoir
        // les lister avant le jour J pour organiser le relogement.
        var over1 = await harness.BookAsync(From, To, allowOverbooking: true);
        Assert.True(over1.Succeeded, over1.Error);
        Assert.True(over1.Value!.IsOverbooking);

        var over2 = await harness.BookAsync(From, To, allowOverbooking: true);
        Assert.True(over2.Succeeded, over2.Error);
        Assert.True(over2.Value!.IsOverbooking);

        // La troisieme depasse l'autorisation : refusee.
        var over3 = await harness.BookAsync(From, To, allowOverbooking: true);
        Assert.False(over3.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, over3.ErrorType);
    }

    [Fact]
    public async Task L_interrupteur_general_de_l_unite_coupe_la_surreservation_sans_effacer_le_parametrage()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);

        await harness.Service.CreateOverbookingAsync(
            new SaveOverbookingAllowanceRequest(
                PmsHarness.UnitCode,
                PmsHarness.StandardType,
                From,
                To,
                ExtraRooms: 3),
            PmsHarness.Context,
            CancellationToken.None);

        await harness.BookAsync(From, To, harness.StandardRooms[0].Id);

        // OverbookingEnabled est faux par defaut : l'autorisation existe mais ne s'applique pas.
        var refused = await harness.BookAsync(From, To, allowOverbooking: true);
        Assert.False(refused.Succeeded);

        await harness.SavePolicyAsync(PmsHarness.DefaultPolicy(overbookingEnabled: true));

        var accepted = await harness.BookAsync(From, To, allowOverbooking: true);
        Assert.True(accepted.Succeeded, accepted.Error);
    }

    [Fact]
    public async Task Deux_autorisations_actives_qui_se_chevauchent_sont_refusees()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);

        var first = await harness.Service.CreateOverbookingAsync(
            new SaveOverbookingAllowanceRequest(
                PmsHarness.UnitCode,
                PmsHarness.StandardType,
                new DateOnly(2031, 5, 1),
                new DateOnly(2031, 5, 31),
                ExtraRooms: 2),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(first.Succeeded, first.Error);

        var overlapping = await harness.Service.CreateOverbookingAsync(
            new SaveOverbookingAllowanceRequest(
                PmsHarness.UnitCode,
                PmsHarness.StandardType,
                new DateOnly(2031, 5, 15),
                new DateOnly(2031, 6, 15),
                ExtraRooms: 3),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.False(overlapping.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, overlapping.ErrorType);
    }
}
