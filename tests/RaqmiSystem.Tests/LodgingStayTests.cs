using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Domain.Housekeeping;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Tests;

/// <summary>
/// Le deroulement d'un sejour : vente par type sans chambre, affectation, walk-in, changement de
/// chambre, prolongation, surclassement et declassement.
/// </summary>
public sealed class LodgingStayTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Une_vente_par_type_sans_chambre_consomme_l_inventaire()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 2, suites: 0);

        var from = Today.AddDays(10);
        var to = from.AddDays(2);

        var sold = await harness.BookAsync(from, to);

        Assert.True(sold.Succeeded, sold.Error);
        Assert.Null(sold.Value!.RoomId);
        Assert.Equal(PmsHarness.StandardType, sold.Value.RoomTypeCode);

        // LE FAIT CENTRAL : un dossier sans chambre affectee consomme quand meme l'inventaire.
        // L'ignorer ferait lire comme libres des chambres deja vendues.
        var availability = await harness.AvailabilityForAsync(from, to);
        Assert.Equal(1, availability.PublicAvailable);

        var occupancy = await harness.Service.GetOccupancyAsync(
            PmsHarness.UnitCode, from, from, CancellationToken.None);

        Assert.True(occupancy.Succeeded, occupancy.Error);
        Assert.Equal(1, occupancy.Value!.Days.Single().OccupiedRooms);
    }

    [Fact]
    public async Task L_affectation_puis_la_liberation_conservent_l_historique()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 2, suites: 0);

        var from = Today.AddDays(5);
        var to = from.AddDays(2);

        var sold = await harness.BookAsync(from, to);
        Assert.True(sold.Succeeded, sold.Error);

        var assigned = await harness.Service.AssignRoomAsync(
            sold.Value!.Id,
            new AssignRoomRequest(harness.StandardRooms[0].Id, "Client fidele, chambre calme."),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(assigned.Succeeded, assigned.Error);
        Assert.Equal(harness.StandardRooms[0].Id, assigned.Value!.RoomId);

        var released = await harness.Service.AssignRoomAsync(
            sold.Value.Id,
            new AssignRoomRequest(null, "Reorganisation du plan."),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(released.Succeeded, released.Error);
        Assert.Null(released.Value!.RoomId);

        var detail = await harness.Service.GetReservationDetailAsync(sold.Value.Id, CancellationToken.None);
        Assert.True(detail.Succeeded, detail.Error);

        // L'AFFECTATION LAISSE UNE TRACE MEME APRES LIBERATION : c'est le dossier de la chambre,
        // pas un simple pointeur.
        var history = Assert.Single(detail.Value!.RoomHistory);
        Assert.Equal("101", history.RoomNumber);
        Assert.False(history.IsCurrent);
        Assert.NotNull(history.ReleasedAt);
    }

    [Fact]
    public async Task Un_walk_in_vend_affecte_et_enregistre_l_arrivee_en_un_geste()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);

        var result = await harness.Service.CreateWalkInAsync(
            new WalkInRequest(
                PmsHarness.UnitCode,
                harness.StandardRooms[0].Id,
                PmsHarness.CustomerCode,
                Today.AddDays(1),
                Adults: 2,
                GuestName: "M. Benali"),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(ReservationStatus.CheckedIn, result.Value!.Status);
        Assert.True(result.Value.IsWalkIn);
        Assert.Equal(harness.StandardRooms[0].Id, result.Value.RoomId);

        // Le folio est ouvert et porte deja la nuit d'arrivee.
        var folio = await harness.Service.GetFolioAsync(result.Value.Id, CancellationToken.None);
        Assert.True(folio.Succeeded, folio.Error);
        Assert.Equal(PmsHarness.NightlyRate, folio.Value!.Balance);
    }

    [Fact]
    public async Task Un_changement_de_chambre_trace_les_deux_et_rend_l_ancienne_sale()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 3, suites: 0);

        var from = Today;
        var to = from.AddDays(3);

        var stay = await harness.BookAsync(from, to, harness.StandardRooms[0].Id);
        Assert.True(stay.Succeeded, stay.Error);

        var checkedIn = await harness.Service.CheckInAsync(stay.Value!.Id, PmsHarness.Context, CancellationToken.None);
        Assert.True(checkedIn.Succeeded, checkedIn.Error);

        var moved = await harness.Service.MoveRoomAsync(
            stay.Value.Id,
            new RoomMoveRequest(harness.StandardRooms[1].Id, "Climatisation en panne dans la 101."),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(moved.Succeeded, moved.Error);
        Assert.Equal(harness.StandardRooms[1].Id, moved.Value!.RoomId);

        // Un second deplacement : l'historique en garde TROIS, jamais une valeur finale.
        var movedAgain = await harness.Service.MoveRoomAsync(
            stay.Value.Id,
            new RoomMoveRequest(harness.StandardRooms[2].Id, "Client gene par le bruit."),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(movedAgain.Succeeded, movedAgain.Error);

        var detail = await harness.Service.GetReservationDetailAsync(stay.Value.Id, CancellationToken.None);
        Assert.True(detail.Succeeded, detail.Error);

        Assert.Equal(
            new[] { "101", "102", "103" },
            detail.Value!.RoomHistory.Select(entry => entry.RoomNumber).ToArray());

        Assert.Single(detail.Value.RoomHistory, entry => entry.IsCurrent);

        // L'ANCIENNE CHAMBRE PART EN SALE : c'est l'evenement que la gouvernante attend.
        var condition = await harness.DbContext.Set<RoomCondition>()
            .SingleOrDefaultAsync(current => current.RoomId == harness.StandardRooms[0].Id);

        Assert.NotNull(condition);
        Assert.Equal(RoomConditionStatus.Dirty, condition!.Status);

        // Le journal du sejour porte les deux deplacements, avec leur motif.
        Assert.Equal(2, detail.Value.Journal.Count(entry => entry.Kind == ReservationEventKind.RoomMoved));
    }

    [Fact]
    public async Task Un_changement_de_chambre_sans_motif_est_refuse()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 2, suites: 0);

        var stay = await harness.BookAsync(Today, Today.AddDays(2), harness.StandardRooms[0].Id);
        Assert.True(stay.Succeeded, stay.Error);

        var refused = await harness.Service.MoveRoomAsync(
            stay.Value!.Id,
            new RoomMoveRequest(harness.StandardRooms[1].Id, "   "),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.False(refused.Succeeded);
        Assert.Contains("motif", refused.Error);
    }

    [Fact]
    public async Task Une_prolongation_revalide_la_disponibilite_et_repose_les_tarifs()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);

        var from = Today;
        var to = from.AddDays(2);

        var stay = await harness.BookAsync(from, to, harness.StandardRooms[0].Id);
        Assert.True(stay.Succeeded, stay.Error);
        Assert.Equal(2 * PmsHarness.NightlyRate, stay.Value!.TotalStayAmount);

        var extended = await harness.Service.ExtendStayAsync(
            stay.Value.Id,
            new ExtendStayRequest(to.AddDays(2), "Le client reste deux nuits de plus."),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(extended.Succeeded, extended.Error);
        Assert.Equal(to.AddDays(2), extended.Value!.DepartureDate);
        Assert.Equal(4 * PmsHarness.NightlyRate, extended.Value.TotalStayAmount);

        var detail = await harness.Service.GetReservationDetailAsync(stay.Value.Id, CancellationToken.None);
        Assert.Contains(detail.Value!.Journal, entry => entry.Kind == ReservationEventKind.DatesChanged);
        Assert.Contains(detail.Value.Journal, entry => entry.Kind == ReservationEventKind.RateChanged);
    }

    [Fact]
    public async Task Une_prolongation_sur_une_chambre_deja_prise_est_refusee()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);

        var from = Today;
        var stay = await harness.BookAsync(from, from.AddDays(2), harness.StandardRooms[0].Id);
        Assert.True(stay.Succeeded, stay.Error);

        // Un autre client arrive juste apres, sur la meme chambre : le depart et l'arrivee du
        // meme jour ne se chevauchent pas.
        var next = await harness.BookAsync(from.AddDays(2), from.AddDays(4), harness.StandardRooms[0].Id);
        Assert.True(next.Succeeded, next.Error);

        var refused = await harness.Service.ExtendStayAsync(
            stay.Value!.Id,
            new ExtendStayRequest(from.AddDays(3)),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.False(refused.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, refused.ErrorType);
    }

    [Fact]
    public async Task Raccourcir_un_sejour_dont_des_nuits_sont_facturees_est_refuse()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);

        var from = Today;
        var stay = await harness.BookAsync(from, from.AddDays(3), harness.StandardRooms[0].Id);
        Assert.True(stay.Succeeded, stay.Error);

        await harness.Service.CheckInAsync(stay.Value!.Id, PmsHarness.Context, CancellationToken.None);

        // Le night audit du lendemain pose la deuxieme nuit.
        var audit = await harness.Service.RunNightAuditAsync(
            new RunNightAuditRequest(PmsHarness.UnitCode, from.AddDays(1), ForcePostWithFindings: true),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(audit.Succeeded, audit.Error);
        Assert.Equal(1, audit.Value!.PostedRoomNights);

        // Raccourcir jusqu'au lendemain defacturerait cette nuit en silence : c'est refuse.
        var refused = await harness.Service.ExtendStayAsync(
            stay.Value.Id,
            new ExtendStayRequest(from.AddDays(1)),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.False(refused.Succeeded);
        Assert.Contains("deja facturees", refused.Error);

        // Raccourcir AU-DELA de ce qui est facture reste possible : rien n'est reecrit.
        var accepted = await harness.Service.ExtendStayAsync(
            stay.Value.Id,
            new ExtendStayRequest(from.AddDays(2)),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(accepted.Succeeded, accepted.Error);
        Assert.Equal(from.AddDays(2), accepted.Value!.DepartureDate);
    }

    [Fact]
    public async Task Changer_le_type_d_un_client_installe_exige_sa_nouvelle_chambre()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 1);

        var from = Today;
        var stay = await harness.BookAsync(from, from.AddDays(2), harness.StandardRooms[0].Id);
        await harness.Service.CheckInAsync(stay.Value!.Id, PmsHarness.Context, CancellationToken.None);

        // Sans chambre cible, le client se retrouverait sans affectation alors qu'il dort dans une
        // chambre bien reelle : il disparaitrait du plan.
        var refused = await harness.Service.ChangeRoomTypeAsync(
            stay.Value.Id,
            new ChangeRoomTypeRequest(PmsHarness.SuiteType, "Surclassement commercial."),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.False(refused.Succeeded);
        Assert.Contains("indiquez la chambre", refused.Error);

        var accepted = await harness.Service.ChangeRoomTypeAsync(
            stay.Value.Id,
            new ChangeRoomTypeRequest(
                PmsHarness.SuiteType,
                "Surclassement commercial.",
                TargetRoomId: harness.Suites[0].Id),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(accepted.Succeeded, accepted.Error);
        Assert.Equal(harness.Suites[0].Id, accepted.Value!.RoomId);
    }

    [Fact]
    public async Task Un_surclassement_offert_garde_le_prix_vendu_et_le_type_d_origine()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 1);

        var from = Today.AddDays(3);
        var to = from.AddDays(2);

        var stay = await harness.BookAsync(from, to, harness.StandardRooms[0].Id);
        Assert.True(stay.Succeeded, stay.Error);

        var initialTotal = stay.Value!.TotalStayAmount;

        var upgraded = await harness.Service.ChangeRoomTypeAsync(
            stay.Value.Id,
            new ChangeRoomTypeRequest(
                PmsHarness.SuiteType,
                "Geste commercial : client fidele, hotel complet en double.",
                Chargeable: false,
                TargetRoomId: harness.Suites[0].Id),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(upgraded.Succeeded, upgraded.Error);
        Assert.Equal(PmsHarness.SuiteType, upgraded.Value!.RoomTypeCode);

        // LE TYPE D'ORIGINE RESTE : sans lui, le controle de gestion ne verrait plus la difference
        // entre une suite vendue et une suite offerte.
        Assert.Equal(PmsHarness.StandardType, upgraded.Value.OriginalRoomTypeCode);
        Assert.Equal(initialTotal, upgraded.Value.TotalStayAmount);
        Assert.Equal(harness.Suites[0].Id, upgraded.Value.RoomId);

        var detail = await harness.Service.GetReservationDetailAsync(stay.Value.Id, CancellationToken.None);
        Assert.Contains(detail.Value!.Journal, entry => entry.Kind == ReservationEventKind.Upgraded);
    }

    [Fact]
    public async Task Un_declassement_est_reconnu_par_le_rang_des_types()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 1);

        var from = Today.AddDays(3);
        var to = from.AddDays(2);

        var stay = await harness.BookAsync(from, to, harness.Suites[0].Id, PmsHarness.SuiteType);
        Assert.True(stay.Succeeded, stay.Error);

        var downgraded = await harness.Service.ChangeRoomTypeAsync(
            stay.Value!.Id,
            new ChangeRoomTypeRequest(
                PmsHarness.StandardType,
                "Suite indisponible : degat des eaux.",
                Chargeable: true,
                TargetRoomId: harness.StandardRooms[0].Id),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(downgraded.Succeeded, downgraded.Error);

        var detail = await harness.Service.GetReservationDetailAsync(stay.Value.Id, CancellationToken.None);

        // Le SENS vient du rang declare, jamais du libelle : suite (rang 5) -> double (rang 1).
        Assert.Contains(detail.Value!.Journal, entry => entry.Kind == ReservationEventKind.Downgraded);
    }

    [Fact]
    public async Task Une_arrivee_anticipee_et_un_depart_tardif_se_facturent_selon_la_politique()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);

        await harness.SavePolicyAsync(PmsHarness.DefaultPolicy(
            earlyCheckInIsFree: false,
            earlyCheckInFlatCharge: 3_000m,
            lateCheckOutIsFree: false,
            lateCheckOutFlatCharge: 2_500m,
            lateCheckOutUntilTime: new TimeOnly(18, 0)));

        var from = Today;
        var to = from.AddDays(1);

        var stay = await harness.BookAsync(from, to, harness.StandardRooms[0].Id);
        Assert.True(stay.Succeeded, stay.Error);

        // Arrivee annoncee a 9h : avant l'heure de comptoir (14h).
        var updated = await harness.Service.UpdateReservationAsync(
            stay.Value!.Id,
            new UpdateReservationRequest(
                Adults: 2,
                EstimatedArrivalTime: new TimeOnly(9, 0),
                EstimatedDepartureTime: new TimeOnly(16, 0)),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(updated.Succeeded, updated.Error);

        var checkedIn = await harness.Service.CheckInAsync(stay.Value.Id, PmsHarness.Context, CancellationToken.None);
        Assert.True(checkedIn.Succeeded, checkedIn.Error);

        var afterArrival = await harness.Service.GetFolioAsync(stay.Value.Id, CancellationToken.None);
        Assert.Equal(PmsHarness.NightlyRate + 3_000m, afterArrival.Value!.Balance);

        // La preparation du depart pose le supplement de depart tardif (16h > 12h).
        var prepared = await harness.Service.PrepareCheckOutAsync(
            stay.Value.Id,
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(prepared.Succeeded, prepared.Error);

        var folio = Assert.Single(prepared.Value!);
        Assert.Equal(PmsHarness.NightlyRate + 3_000m + 2_500m, folio.Balance);

        // Le geste est idempotent : le repasser ne facture rien de plus.
        var again = await harness.Service.PrepareCheckOutAsync(
            stay.Value.Id,
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(again.Succeeded, again.Error);
        Assert.Equal(folio.Balance, Assert.Single(again.Value!).Balance);
    }

    [Fact]
    public async Task Un_depart_au_dela_de_la_limite_de_depart_tardif_est_refuse()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);

        await harness.SavePolicyAsync(PmsHarness.DefaultPolicy(
            lateCheckOutIsFree: false,
            lateCheckOutFlatCharge: 2_000m,
            lateCheckOutUntilTime: new TimeOnly(18, 0)));

        var stay = await harness.BookAsync(Today, Today.AddDays(1), harness.StandardRooms[0].Id);
        await harness.Service.UpdateReservationAsync(
            stay.Value!.Id,
            new UpdateReservationRequest(Adults: 2, EstimatedDepartureTime: new TimeOnly(21, 0)),
            PmsHarness.Context,
            CancellationToken.None);

        await harness.Service.CheckInAsync(stay.Value.Id, PmsHarness.Context, CancellationToken.None);

        var refused = await harness.Service.PrepareCheckOutAsync(
            stay.Value.Id,
            PmsHarness.Context,
            CancellationToken.None);

        // Au-dela de la limite, ce n'est plus un depart tardif mais une nuit supplementaire.
        Assert.False(refused.Succeeded);
        Assert.Contains("nuit supplementaire", refused.Error);
    }
}
