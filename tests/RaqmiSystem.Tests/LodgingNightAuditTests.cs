using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Domain.Closing;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Tests;

/// <summary>
/// Le night audit, la date metier hoteliere et le balayage des no-shows.
///
/// LE TEST QUI COMPTE LE PLUS EST CELUI DE LA RELANCE : un night audit repasse ne doit JAMAIS
/// doubler une nuitee. C'est la seule garantie qui permette a un veilleur de relancer quand il
/// doute, et c'est aussi ce qui rend le module rejouable apres une coupure.
/// </summary>
public sealed class LodgingNightAuditTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public void La_date_metier_est_le_lendemain_de_la_derniere_journee_cloturee()
    {
        var calendar = new DateOnly(2031, 8, 15);

        // Aucune cloture : la date metier suit le calendrier, seul point de depart possible.
        var fresh = BusinessDay.Resolve(null, calendar);
        Assert.Equal(calendar, fresh.Date);
        Assert.False(fresh.HasClosing);
        Assert.False(fresh.IsLate);

        // Le 14 est cloture : il est reellement le 15, et l'hotel travaille sur le 15.
        var current = BusinessDay.Resolve(new DateOnly(2031, 8, 14), calendar);
        Assert.Equal(new DateOnly(2031, 8, 15), current.Date);
        Assert.False(current.IsLate);

        // LE CAS DE L'ENONCE : il est le 15 a 02h00, la cloture du 14 n'est pas passee. La
        // derniere journee cloturee est le 13, donc la date metier est le 14 - et le systeme
        // SIGNALE le retard plutot que d'avancer tout seul.
        var late = BusinessDay.Resolve(new DateOnly(2031, 8, 13), calendar);
        Assert.Equal(new DateOnly(2031, 8, 14), late.Date);
        Assert.True(late.IsLate);
        Assert.Equal(1, late.PendingDays);
    }

    [Fact]
    public async Task La_date_metier_de_l_unite_suit_ses_clotures()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);

        var before = await harness.Service.GetBusinessDateAsync(PmsHarness.UnitCode, CancellationToken.None);
        Assert.True(before.Succeeded, before.Error);
        Assert.False(before.Value!.HasClosing);
        Assert.Equal(Today, before.Value.BusinessDate);

        harness.DbContext.Set<DailyClosing>().Add(new DailyClosing(
            Today,
            PmsHarness.UnitCode,
            "veilleur",
            DateTimeOffset.UtcNow));

        await harness.DbContext.SaveChangesAsync();

        var after = await harness.Service.GetBusinessDateAsync(PmsHarness.UnitCode, CancellationToken.None);
        Assert.True(after.Succeeded, after.Error);
        Assert.True(after.Value!.HasClosing);
        Assert.Equal(Today.AddDays(1), after.Value.BusinessDate);
    }

    [Fact]
    public async Task Le_night_audit_pose_la_nuit_et_le_relancer_ne_la_double_jamais()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 2, suites: 0);

        var arrival = Today;
        var departure = arrival.AddDays(3);

        var stay = await harness.BookAsync(arrival, departure, harness.StandardRooms[0].Id);
        Assert.True(stay.Succeeded, stay.Error);

        var checkedIn = await harness.Service.CheckInAsync(stay.Value!.Id, PmsHarness.Context, CancellationToken.None);
        Assert.True(checkedIn.Succeeded, checkedIn.Error);

        // L'arrivee a deja pose la nuit du jour. Le night audit de CETTE journee la retrouve et la
        // saute : c'est l'idempotence, vue de l'interieur.
        var sameDay = await harness.Service.RunNightAuditAsync(
            new RunNightAuditRequest(PmsHarness.UnitCode, arrival, ForcePostWithFindings: true),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(sameDay.Succeeded, sameDay.Error);
        Assert.Equal(0, sameDay.Value!.PostedRoomNights);
        Assert.Equal(1, sameDay.Value.SkippedAlreadyPosted);

        // Le night audit du LENDEMAIN pose la deuxieme nuit.
        var nextDay = await harness.Service.RunNightAuditAsync(
            new RunNightAuditRequest(PmsHarness.UnitCode, arrival.AddDays(1), ForcePostWithFindings: true),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(nextDay.Succeeded, nextDay.Error);
        Assert.Equal(1, nextDay.Value!.PostedRoomNights);
        Assert.Equal(PmsHarness.NightlyRate, nextDay.Value.PostedAmount);

        var folio = await harness.Service.GetFolioAsync(stay.Value.Id, CancellationToken.None);
        Assert.Equal(2 * PmsHarness.NightlyRate, folio.Value!.Balance);

        // LA RELANCE. Un passage EXECUTE existe deja pour cette journee : la seconde tentative est
        // refusee, et surtout rien n'est ecrit. Le folio ne bouge pas d'un centime.
        var rerun = await harness.Service.RunNightAuditAsync(
            new RunNightAuditRequest(PmsHarness.UnitCode, arrival.AddDays(1), ForcePostWithFindings: true),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.False(rerun.Succeeded);
        Assert.Contains("deja ete passe", rerun.Error);

        var folioAfterRerun = await harness.Service.GetFolioAsync(stay.Value.Id, CancellationToken.None);
        Assert.Equal(2 * PmsHarness.NightlyRate, folioAfterRerun.Value!.Balance);
        Assert.Equal(2, folioAfterRerun.Value.Charges.Count(charge => charge.Kind == ChargeKind.Night));
    }

    [Fact]
    public async Task La_repetition_ne_fait_que_les_controles_et_n_ecrit_rien()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 2, suites: 0);

        var arrival = Today.AddDays(-1);

        // Une arrivee non traitee : c'est un constat BLOQUANT, elle immobilise une chambre pour
        // personne.
        var pending = await harness.BookAsync(arrival, arrival.AddDays(3), harness.StandardRooms[0].Id);
        Assert.True(pending.Succeeded, pending.Error);

        var dryRun = await harness.Service.RunNightAuditAsync(
            new RunNightAuditRequest(PmsHarness.UnitCode, Today, DryRun: true),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(dryRun.Succeeded, dryRun.Error);
        Assert.Equal(NightAuditStatus.Inspected, dryRun.Value!.Status);
        Assert.Equal(1, dryRun.Value.PendingArrivals);
        Assert.Contains(dryRun.Value.Findings, finding => finding.Code == "arrivee.non_traitee" && finding.IsBlocking);
        Assert.Equal(0, dryRun.Value.PostedRoomNights);

        // Aucune ligne de passage n'a ete enregistree : une repetition doit pouvoir se rejouer
        // autant de fois qu'un veilleur le veut.
        Assert.Equal(0, await harness.DbContext.Set<NightAuditRun>().CountAsync());
    }

    [Fact]
    public async Task Un_constat_bloquant_arrete_le_passage_sans_rien_ecrire()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 2, suites: 0);

        var arrival = Today.AddDays(-1);
        await harness.BookAsync(arrival, arrival.AddDays(3), harness.StandardRooms[0].Id);

        var blocked = await harness.Service.RunNightAuditAsync(
            new RunNightAuditRequest(PmsHarness.UnitCode, Today),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(blocked.Succeeded, blocked.Error);
        Assert.Equal(NightAuditStatus.Blocked, blocked.Value!.Status);
        Assert.Equal(0, blocked.Value.PostedRoomNights);

        // Le passage refuse est ENREGISTRE - il documente pourquoi la journee n'a pas ete
        // cloturee - mais il n'occupe pas la place du passage execute : on peut le relancer.
        var run = Assert.Single(await harness.DbContext.Set<NightAuditRun>().ToListAsync());
        Assert.Equal(NightAuditStatus.Blocked, run.Status);
    }

    [Fact]
    public async Task Le_balayage_des_no_shows_liste_avant_d_appliquer()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 2, suites: 0);

        var arrival = Today.AddDays(-2);
        var stay = await harness.BookAsync(arrival, arrival.AddDays(2), harness.StandardRooms[0].Id);
        Assert.True(stay.Succeeded, stay.Error);

        // LECTURE SEULE : la liste des candidats, sans rien basculer.
        var preview = await harness.Service.SweepNoShowsAsync(
            PmsHarness.UnitCode,
            Today,
            apply: false,
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(preview.Succeeded, preview.Error);
        Assert.False(preview.Value!.Applied);
        Assert.Equal(0, preview.Value.RecordedCount);

        var candidate = Assert.Single(preview.Value.Candidates);
        Assert.Equal(stay.Value!.Id, candidate.ReservationId);
        Assert.False(candidate.Recorded);

        var stillBooked = await harness.Service.GetReservationAsync(stay.Value.Id, CancellationToken.None);
        Assert.Equal(ReservationStatus.Confirmed, stillBooked.Value!.Status);

        // APPLICATION : les dossiers basculent et l'inventaire est rendu.
        var applied = await harness.Service.SweepNoShowsAsync(
            PmsHarness.UnitCode,
            Today,
            apply: true,
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(applied.Succeeded, applied.Error);
        Assert.Equal(1, applied.Value!.RecordedCount);

        var noShow = await harness.Service.GetReservationAsync(stay.Value.Id, CancellationToken.None);
        Assert.Equal(ReservationStatus.NoShow, noShow.Value!.Status);

        var availability = await harness.AvailabilityForAsync(arrival, arrival.AddDays(2));
        Assert.Equal(2, availability.PublicAvailable);
    }

    [Fact]
    public async Task Le_night_audit_pose_les_prestations_automatiques_par_nuit()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);

        var extra = await harness.Service.CreateExtraAsync(
            new SaveExtraItemRequest(
                PmsHarness.UnitCode,
                "PDJ",
                "Petit-dejeuner",
                ExtraPricingBasis.PerPersonPerNight,
                UnitPrice: 900m,
                VatRate: 9m,
                IsPostedByNightAudit: true),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(extra.Succeeded, extra.Error);

        var arrival = Today;
        var stay = await harness.BookAsync(arrival, arrival.AddDays(2), harness.StandardRooms[0].Id);
        Assert.True(stay.Succeeded, stay.Error);

        var attached = await harness.Service.AddReservationExtraAsync(
            stay.Value!.Id,
            new AddReservationExtraRequest("PDJ"),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(attached.Succeeded, attached.Error);

        await harness.Service.CheckInAsync(stay.Value.Id, PmsHarness.Context, CancellationToken.None);

        var run = await harness.Service.RunNightAuditAsync(
            new RunNightAuditRequest(PmsHarness.UnitCode, arrival, ForcePostWithFindings: true),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(run.Succeeded, run.Error);
        Assert.Equal(1, run.Value!.PostedExtras);

        var folio = await harness.Service.GetFolioAsync(stay.Value.Id, CancellationToken.None);

        // Deux personnes x une nuit x 900.
        var breakfast = Assert.Single(folio.Value!.Charges, charge => charge.ExtraCode == "PDJ");
        Assert.Equal(1_800m, breakfast.Amount);
        Assert.Equal(9m, breakfast.VatRate);
        Assert.Equal(2m, breakfast.Quantity);
    }
}
