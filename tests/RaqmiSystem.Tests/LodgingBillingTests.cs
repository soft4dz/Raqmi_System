using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Tests;

/// <summary>
/// L'argent du sejour : politiques d'annulation figees, penalites, acomptes, folios multiples et
/// transferts de lignes.
/// </summary>
public sealed class LodgingBillingTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static SaveCancellationPolicyRequest StandardPolicy()
    {
        // Le bareme de l'enonce : gratuit jusqu'a J-2, puis une nuit, no-show 100 %.
        return new SaveCancellationPolicyRequest(
            PmsHarness.UnitCode,
            "FLEX",
            "Flexible",
            CancellationChargeBasis.PercentOfStay,
            100m,
            new[]
            {
                new CancellationPolicyRuleResponse(2, CancellationChargeBasis.None, 0m),
                new CancellationPolicyRuleResponse(0, CancellationChargeBasis.FirstNight, 0m)
            });
    }

    [Fact]
    public void Le_bareme_fige_retient_le_palier_le_plus_genereux_encore_applicable()
    {
        var policy = new CancellationPolicy(PmsHarness.UnitCode, "FLEX", "Flexible");
        policy.SetNoShowTerms(CancellationChargeBasis.PercentOfStay, 100m);
        policy.ReplaceRules(new[]
        {
            new CancellationPolicyRule(2, CancellationChargeBasis.None, 0m),
            new CancellationPolicyRule(0, CancellationChargeBasis.FirstNight, 0m)
        });

        var snapshot = policy.ToSnapshotJson();

        var nights = new[]
        {
            new ReservationNightRate(new DateOnly(2031, 9, 10), 10_000m, "STD"),
            new ReservationNightRate(new DateOnly(2031, 9, 11), 12_000m, "STD")
        };

        // Annulation cinq jours avant : le palier J-2 s'applique, donc gratuit. Lu dans l'autre
        // sens, le bareme facturerait la penalite maximale a qui annule six mois a l'avance.
        Assert.Equal(0m, CancellationPolicy.EvaluateSnapshot(snapshot, 5, 22_000m, nights));

        // La veille : plus aucun palier gratuit, la premiere nuit est due.
        Assert.Equal(10_000m, CancellationPolicy.EvaluateSnapshot(snapshot, 1, 22_000m, nights));

        // No-show : 100 % du sejour.
        Assert.Equal(22_000m, CancellationPolicy.EvaluateNoShowSnapshot(snapshot, 22_000m, nights));
    }

    [Fact]
    public void Une_penalite_ne_peut_jamais_depasser_le_prix_du_sejour()
    {
        var policy = new CancellationPolicy(PmsHarness.UnitCode, "DUR", "Non remboursable");
        policy.SetNoShowTerms(CancellationChargeBasis.Nights, 10m);
        policy.ReplaceRules(new[] { new CancellationPolicyRule(0, CancellationChargeBasis.FixedAmount, 999_999m) });

        var snapshot = policy.ToSnapshotJson();
        var nights = new[] { new ReservationNightRate(new DateOnly(2031, 9, 10), 8_000m, "STD") };

        // Retenir plus que le prix de la chambre n'est pas une politique, c'est une erreur de
        // saisie : le calcul plafonne.
        Assert.Equal(8_000m, CancellationPolicy.EvaluateSnapshot(snapshot, 0, 8_000m, nights));
        Assert.Equal(8_000m, CancellationPolicy.EvaluateNoShowSnapshot(snapshot, 8_000m, nights));
    }

    [Fact]
    public async Task La_politique_est_figee_dans_le_dossier_et_ne_change_plus()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 2, suites: 0);

        var created = await harness.Service.CreateCancellationPolicyAsync(
            StandardPolicy(),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(created.Succeeded, created.Error);

        var arrival = Today.AddDays(10);

        var stay = await harness.Service.CreateReservationAsync(
            new CreateReservationRequest(
                PmsHarness.UnitCode,
                harness.StandardRooms[0].Id,
                PmsHarness.CustomerCode,
                arrival,
                arrival.AddDays(2),
                2,
                CancellationPolicyCode: "FLEX"),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(stay.Succeeded, stay.Error);
        Assert.Equal("FLEX", stay.Value!.CancellationPolicyCode);
        Assert.Contains("J-2", stay.Value.CancellationPolicyDescription);

        // L'hotel durcit sa politique APRES la vente : 100 % des le premier jour.
        var hardened = await harness.Service.UpdateCancellationPolicyAsync(
            created.Value!.Id,
            StandardPolicy() with
            {
                Rules = new[] { new CancellationPolicyRuleResponse(0, CancellationChargeBasis.PercentOfStay, 100m) }
            },
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(hardened.Succeeded, hardened.Error);

        // Le dossier deja pris garde SES conditions : annulation a J-10, donc gratuite. Un bareme
        // qui changerait retroactivement serait indefendable, commercialement comme juridiquement.
        var cancelled = await harness.Service.CancelReservationAsync(
            stay.Value.Id,
            new CancelReservationRequest("Le client annule son voyage."),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(cancelled.Succeeded, cancelled.Error);
        Assert.Equal(ReservationStatus.Cancelled, cancelled.Value!.Status);
        Assert.Equal(0m, cancelled.Value.CancellationFeeAmount);
    }

    [Fact]
    public async Task Un_no_show_declenche_la_penalite_prevue_par_la_politique_figee()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 2, suites: 0);

        await harness.Service.CreateCancellationPolicyAsync(
            StandardPolicy(),
            PmsHarness.Context,
            CancellationToken.None);

        var arrival = Today.AddDays(-2);

        var stay = await harness.Service.CreateReservationAsync(
            new CreateReservationRequest(
                PmsHarness.UnitCode,
                harness.StandardRooms[0].Id,
                PmsHarness.CustomerCode,
                arrival,
                arrival.AddDays(2),
                2,
                CancellationPolicyCode: "FLEX"),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(stay.Succeeded, stay.Error);

        var noShow = await harness.Service.MarkNoShowAsync(
            stay.Value!.Id,
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(noShow.Succeeded, noShow.Error);
        Assert.Equal(ReservationStatus.NoShow, noShow.Value!.Status);
        Assert.Equal(2 * PmsHarness.NightlyRate, noShow.Value.CancellationFeeAmount);
    }

    [Fact]
    public async Task Un_acompte_n_apparait_au_folio_qu_une_fois_impute()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);

        var arrival = Today;
        var stay = await harness.BookAsync(arrival, arrival.AddDays(2), harness.StandardRooms[0].Id);
        Assert.True(stay.Succeeded, stay.Error);

        var deposit = await harness.Service.CreateDepositAsync(
            stay.Value!.Id,
            new CreateDepositRequest(5_000m, arrival.AddDays(-1), "Acompte a la reservation."),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(deposit.Succeeded, deposit.Error);
        Assert.Equal(DepositStatus.Requested, deposit.Value!.Status);

        var paid = await harness.Service.PayDepositAsync(
            deposit.Value.Id,
            new PayDepositRequest(arrival.AddDays(-1), "CB", "TPE-0099"),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(paid.Succeeded, paid.Error);
        Assert.Equal(DepositStatus.Paid, paid.Value!.Status);

        // TANT QU'IL N'EST PAS IMPUTE, l'acompte n'apparait pas sur le folio : de l'argent recu
        // avant toute prestation ne doit pas afficher un solde negatif sur une chambre pas encore
        // occupee.
        await harness.Service.CheckInAsync(stay.Value.Id, PmsHarness.Context, CancellationToken.None);

        var beforeApply = await harness.Service.GetFolioAsync(stay.Value.Id, CancellationToken.None);
        Assert.Equal(PmsHarness.NightlyRate, beforeApply.Value!.Balance);

        var applied = await harness.Service.ApplyDepositAsync(
            deposit.Value.Id,
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(applied.Succeeded, applied.Error);
        Assert.Equal(DepositStatus.Applied, applied.Value!.Status);

        var afterApply = await harness.Service.GetFolioAsync(stay.Value.Id, CancellationToken.None);
        Assert.Equal(PmsHarness.NightlyRate - 5_000m, afterApply.Value!.Balance);
        Assert.Contains(afterApply.Value.Charges, charge => charge.Kind == ChargeKind.Settlement);
    }

    [Fact]
    public async Task Un_acompte_conserve_ne_se_rembourse_plus()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);

        var stay = await harness.BookAsync(Today.AddDays(5), Today.AddDays(7), harness.StandardRooms[0].Id);

        var deposit = await harness.Service.CreateDepositAsync(
            stay.Value!.Id,
            new CreateDepositRequest(4_000m, Today),
            PmsHarness.Context,
            CancellationToken.None);

        await harness.Service.PayDepositAsync(
            deposit.Value!.Id,
            new PayDepositRequest(Today, "ESPECES"),
            PmsHarness.Context,
            CancellationToken.None);

        var forfeited = await harness.Service.ForfeitDepositAsync(
            deposit.Value.Id,
            new CloseDepositRequest("Annulation hors delai."),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(forfeited.Succeeded, forfeited.Error);
        Assert.Equal(DepositStatus.Forfeited, forfeited.Value!.Status);

        var refund = await harness.Service.RefundDepositAsync(
            deposit.Value.Id,
            new CloseDepositRequest("Geste commercial."),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.False(refund.Succeeded);
    }

    [Fact]
    public async Task Un_sejour_porte_plusieurs_folios_et_une_ligne_se_transfere_sans_disparaitre()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);

        var arrival = Today;
        var stay = await harness.BookAsync(arrival, arrival.AddDays(2), harness.StandardRooms[0].Id);
        await harness.Service.CheckInAsync(stay.Value!.Id, PmsHarness.Context, CancellationToken.None);

        var companyFolio = await harness.Service.CreateFolioAsync(
            stay.Value.Id,
            new CreateFolioRequest(FolioKind.Company, PmsHarness.CustomerCode, "Prise en charge societe"),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(companyFolio.Succeeded, companyFolio.Error);
        Assert.Equal(FolioKind.Company, companyFolio.Value!.Kind);

        var folios = await harness.Service.ListFoliosAsync(stay.Value.Id, CancellationToken.None);
        Assert.Equal(2, folios.Value!.Count);

        var guestFolio = folios.Value.Single(folio => folio.Kind == FolioKind.Guest);
        var nightLine = Assert.Single(guestFolio.Charges, charge => charge.Kind == ChargeKind.Night);

        var transferred = await harness.Service.TransferFolioChargeAsync(
            stay.Value.Id,
            new TransferFolioChargeRequest(nightLine.Id, companyFolio.Value.Id, "La societe prend la chambre."),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(transferred.Succeeded, transferred.Error);

        var guest = transferred.Value!.Single(folio => folio.Kind == FolioKind.Guest);
        var company = transferred.Value!.Single(folio => folio.Kind == FolioKind.Company);

        Assert.Equal(0m, guest.Balance);
        Assert.Equal(PmsHarness.NightlyRate, company.Balance);

        // LE TRANSFERT N'EFFACE RIEN : la ligne d'origine est contre-passee, pas supprimee. Un
        // controle doit pouvoir retrouver ce qui a ete facture puis deplace.
        Assert.Equal(2, guest.Charges.Count);
        Assert.Contains(guest.Charges, charge => charge.Kind == ChargeKind.Adjustment);
    }

    [Fact]
    public async Task Le_depart_est_refuse_tant_qu_un_folio_du_sejour_n_est_pas_solde()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);

        var arrival = Today;
        var stay = await harness.BookAsync(arrival, arrival.AddDays(1), harness.StandardRooms[0].Id);
        await harness.Service.CheckInAsync(stay.Value!.Id, PmsHarness.Context, CancellationToken.None);

        var refused = await harness.Service.CheckOutAsync(stay.Value.Id, PmsHarness.Context, CancellationToken.None);
        Assert.False(refused.Succeeded);
        Assert.Contains("ne sont pas soldes", refused.Error);

        var settled = await harness.Service.AddFolioChargeAsync(
            stay.Value.Id,
            new AddFolioChargeRequest(
                arrival,
                "Reglement especes",
                -PmsHarness.NightlyRate,
                ChargeKind.Settlement,
                "REC-2031-001"),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(settled.Succeeded, settled.Error);

        var checkedOut = await harness.Service.CheckOutAsync(stay.Value.Id, PmsHarness.Context, CancellationToken.None);
        Assert.True(checkedOut.Succeeded, checkedOut.Error);
        Assert.Equal(ReservationStatus.CheckedOut, checkedOut.Value!.Status);

        // Les folios sont fermes : une correction posterieure passe par un avoir, pas par une
        // reecriture du compte.
        var folios = await harness.Service.ListFoliosAsync(stay.Value.Id, CancellationToken.None);
        Assert.All(folios.Value!, folio => Assert.Equal(FolioStatus.Closed, folio.Status));
    }

    [Fact]
    public async Task Un_forfait_non_equilibre_ne_peut_pas_etre_active()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);

        var refused = await harness.Service.CreatePackageAsync(
            new SavePackageRequest(
                PmsHarness.UnitCode,
                "WEEKEND",
                "Week-end en amoureux",
                25_000m,
                new[]
                {
                    new PackageComponentResponse("Hebergement", 16_000m, ChargeKind.Night, null, ExtraPricingBasis.PerStay),
                    new PackageComponentResponse("Petit-dejeuner", 3_000m, ChargeKind.Extra, null, ExtraPricingBasis.PerStay)
                }),
            PmsHarness.Context,
            CancellationToken.None);

        // 16 000 + 3 000 = 19 000 pour un prix global de 25 000 : la ventilation ne couvre pas le
        // prix, et le chiffre d'affaires des services serait faux.
        Assert.False(refused.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, refused.ErrorType);
        Assert.Contains("ventilation", refused.Error);

        var balanced = await harness.Service.CreatePackageAsync(
            new SavePackageRequest(
                PmsHarness.UnitCode,
                "WEEKEND",
                "Week-end en amoureux",
                25_000m,
                new[]
                {
                    new PackageComponentResponse("Hebergement", 16_000m, ChargeKind.Night, null, ExtraPricingBasis.PerStay),
                    new PackageComponentResponse("Petit-dejeuner", 3_000m, ChargeKind.Extra, null, ExtraPricingBasis.PerStay),
                    new PackageComponentResponse("Diner", 4_500m, ChargeKind.Extra, null, ExtraPricingBasis.PerStay),
                    new PackageComponentResponse("Spa", 1_500m, ChargeKind.Extra, null, ExtraPricingBasis.PerStay)
                }),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(balanced.Succeeded, balanced.Error);
        Assert.True(balanced.Value!.IsBalanced);
        Assert.Equal(25_000m, balanced.Value.ComponentsTotal);
    }
}
