using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Tests;

/// <summary>
/// Le moteur de restrictions : stop sell, CTA, CTD, MinLOS, MaxLOS et delais de reservation.
///
/// La premiere moitie teste le CALCUL PUR (<see cref="RestrictionSet"/>) : c'est la que vivent les
/// regles, et c'est la qu'elles doivent etre verifiees a la ligne. La seconde verifie que le
/// service les APPLIQUE reellement a la vente - une regle correcte que personne ne consulte ne
/// protege rien.
/// </summary>
public sealed class LodgingRestrictionTests
{
    private const string Unit = "PMS1";
    private static readonly DateOnly Booking = new(2031, 7, 1);

    private static RateRestriction Rule(
        DateOnly from,
        DateOnly to,
        bool closed = false,
        bool cta = false,
        bool ctd = false,
        int minLos = 0,
        int maxLos = 0,
        int minAdvance = 0,
        int maxAdvance = 0,
        string? roomTypeCode = null)
    {
        var restriction = new RateRestriction(Unit, from, to, roomTypeCode);
        restriction.SetRules(closed, cta, ctd, minLos, maxLos, minAdvance, maxAdvance);

        return restriction;
    }

    [Fact]
    public void Un_stop_sell_ferme_toute_nuit_qu_il_couvre()
    {
        var rules = new[] { Rule(new DateOnly(2031, 8, 12), new DateOnly(2031, 8, 14), closed: true) };

        var decision = RestrictionSet.Evaluate(
            rules,
            new DateOnly(2031, 8, 11),
            new DateOnly(2031, 8, 15),
            Booking,
            "DBL",
            null,
            null);

        Assert.False(decision.IsAllowed);

        // Trois nuits fermees : les 12, 13 et 14. La nuit du 11 et celle du 14 au 15 sont libres,
        // mais une seule nuit fermee suffit a refuser le sejour entier.
        Assert.Equal(3, decision.Violations.Count(v => v.Kind == RestrictionViolationKind.Closed));
    }

    [Fact]
    public void Un_CTA_interdit_de_commencer_mais_laisse_traverser()
    {
        var rules = new[] { Rule(new DateOnly(2031, 8, 12), new DateOnly(2031, 8, 12), cta: true) };

        // Arrivee LE 12 : refusee.
        var starting = RestrictionSet.Evaluate(
            rules, new DateOnly(2031, 8, 12), new DateOnly(2031, 8, 15), Booking, "DBL", null, null);

        Assert.False(starting.IsAllowed);
        Assert.Contains(starting.Violations, v => v.Kind == RestrictionViolationKind.ClosedToArrival);

        // Sejour qui TRAVERSE le 12 : accepte. C'est toute la difference avec un stop sell -
        // les clients deja presents poursuivent leur sejour.
        var crossing = RestrictionSet.Evaluate(
            rules, new DateOnly(2031, 8, 10), new DateOnly(2031, 8, 15), Booking, "DBL", null, null);

        Assert.True(crossing.IsAllowed);
    }

    [Fact]
    public void Un_CTD_interdit_de_terminer_a_cette_date()
    {
        var rules = new[] { Rule(new DateOnly(2031, 8, 15), new DateOnly(2031, 8, 15), ctd: true) };

        // La date de depart n'est PAS une nuit du sejour : c'est pour cela que le CTD se controle
        // a part. Un sejour du 12 au 15 dort les 12, 13 et 14 - et pourtant il est refuse.
        var leaving = RestrictionSet.Evaluate(
            rules, new DateOnly(2031, 8, 12), new DateOnly(2031, 8, 15), Booking, "DBL", null, null);

        Assert.False(leaving.IsAllowed);
        Assert.Contains(leaving.Violations, v => v.Kind == RestrictionViolationKind.ClosedToDeparture);

        // Un sejour qui dort la nuit du 15 et part le 16 n'est pas concerne.
        var staying = RestrictionSet.Evaluate(
            rules, new DateOnly(2031, 8, 12), new DateOnly(2031, 8, 16), Booking, "DBL", null, null);

        Assert.True(staying.IsAllowed);
    }

    [Fact]
    public void MinLOS_et_MaxLOS_se_lisent_sur_la_date_d_arrivee()
    {
        var rules = new[]
        {
            Rule(new DateOnly(2031, 8, 1), new DateOnly(2031, 8, 15), minLos: 3, maxLos: 7)
        };

        var tooShort = RestrictionSet.Evaluate(
            rules, new DateOnly(2031, 8, 5), new DateOnly(2031, 8, 7), Booking, "DBL", null, null);

        Assert.False(tooShort.IsAllowed);
        Assert.Contains(tooShort.Violations, v => v.Kind == RestrictionViolationKind.MinimumStay);

        var tooLong = RestrictionSet.Evaluate(
            rules, new DateOnly(2031, 8, 5), new DateOnly(2031, 8, 20), Booking, "DBL", null, null);

        Assert.False(tooLong.IsAllowed);
        Assert.Contains(tooLong.Violations, v => v.Kind == RestrictionViolationKind.MaximumStay);

        var fits = RestrictionSet.Evaluate(
            rules, new DateOnly(2031, 8, 5), new DateOnly(2031, 8, 10), Booking, "DBL", null, null);

        Assert.True(fits.IsAllowed);

        // Une arrivee HORS periode n'est pas soumise a la regle, meme si le sejour la traverse :
        // le minimum se lit sur la date d'arrivee.
        var startingBefore = RestrictionSet.Evaluate(
            rules, new DateOnly(2031, 7, 31), new DateOnly(2031, 8, 2), Booking, "DBL", null, null);

        Assert.True(startingBefore.IsAllowed);
    }

    [Fact]
    public void La_regle_la_plus_restrictive_l_emporte_quand_plusieurs_se_superposent()
    {
        var rules = new[]
        {
            Rule(new DateOnly(2031, 8, 1), new DateOnly(2031, 8, 31), minLos: 2),
            Rule(new DateOnly(2031, 8, 10), new DateOnly(2031, 8, 20), minLos: 5, roomTypeCode: "DBL")
        };

        // Deux minimums couvrent le 12 : 2 et 5. C'est le PLUS EXIGEANT qui s'applique - sans quoi
        // il suffirait d'ajouter une regle plus laxiste pour contourner la premiere.
        var decision = RestrictionSet.Evaluate(
            rules, new DateOnly(2031, 8, 12), new DateOnly(2031, 8, 15), Booking, "DBL", null, null);

        Assert.False(decision.IsAllowed);
        Assert.Contains("5 nuit(s)", decision.Describe());
    }

    [Fact]
    public void Les_delais_de_reservation_se_mesurent_depuis_la_date_metier()
    {
        var rules = new[]
        {
            Rule(new DateOnly(2031, 8, 1), new DateOnly(2031, 8, 31), minAdvance: 7, maxAdvance: 90)
        };

        // Reserve le 1er juillet pour le 5 aout : 35 jours d'avance, dans la fenetre.
        var inWindow = RestrictionSet.Evaluate(
            rules, new DateOnly(2031, 8, 5), new DateOnly(2031, 8, 7), Booking, "DBL", null, null);

        Assert.True(inWindow.IsAllowed);

        // Reserve le 3 aout pour le 5 : deux jours, sous le minimum de sept.
        var tooLate = RestrictionSet.Evaluate(
            rules, new DateOnly(2031, 8, 5), new DateOnly(2031, 8, 7), new DateOnly(2031, 8, 3), "DBL", null, null);

        Assert.False(tooLate.IsAllowed);
        Assert.Contains(tooLate.Violations, v => v.Kind == RestrictionViolationKind.MinimumAdvance);

        // Reserve le 1er janvier pour le 5 aout : 216 jours, au-dela du maximum.
        var tooEarly = RestrictionSet.Evaluate(
            rules, new DateOnly(2031, 8, 5), new DateOnly(2031, 8, 7), new DateOnly(2031, 1, 1), "DBL", null, null);

        Assert.False(tooEarly.IsAllowed);
        Assert.Contains(tooEarly.Violations, v => v.Kind == RestrictionViolationKind.MaximumAdvance);
    }

    [Fact]
    public void Une_regle_ciblee_sur_un_type_ne_touche_pas_les_autres()
    {
        var rules = new[]
        {
            Rule(new DateOnly(2031, 8, 1), new DateOnly(2031, 8, 31), closed: true, roomTypeCode: "SUI")
        };

        var suite = RestrictionSet.Evaluate(
            rules, new DateOnly(2031, 8, 5), new DateOnly(2031, 8, 7), Booking, "SUI", null, null);

        Assert.False(suite.IsAllowed);

        var standard = RestrictionSet.Evaluate(
            rules, new DateOnly(2031, 8, 5), new DateOnly(2031, 8, 7), Booking, "DBL", null, null);

        Assert.True(standard.IsAllowed);
    }

    // ======================= Application reelle par le service de vente =======================

    [Fact]
    public async Task Le_service_refuse_une_vente_fermee_et_le_dit_dans_la_recherche()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 2, suites: 0);

        var from = new DateOnly(2031, 8, 12);
        var to = new DateOnly(2031, 8, 14);

        var created = await harness.Service.CreateRestrictionAsync(
            new SaveRateRestrictionRequest(PmsHarness.UnitCode, from, to.AddDays(-1), IsClosed: true),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(created.Succeeded, created.Error);

        var refused = await harness.BookAsync(from, to, harness.StandardRooms[0].Id);

        Assert.False(refused.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, refused.ErrorType);
        Assert.Contains("vente est fermee", refused.Error);

        // LA RECHERCHE DOIT LE DIRE. Un ecran vide sans explication ferait croire a un hotel
        // complet, alors que la vente est simplement fermee.
        var search = await harness.Service.SearchAvailabilityAsync(
            new AvailabilitySearchRequest(PmsHarness.UnitCode, from, to, Adults: 2),
            CancellationToken.None);

        Assert.True(search.Succeeded, search.Error);
        Assert.True(search.Value!.IsClosed);
        Assert.NotEmpty(search.Value.RestrictionMessages!);
        Assert.Empty(search.Value.Rooms);
    }

    [Fact]
    public async Task Une_fermeture_peut_etre_levee_par_qui_en_a_le_droit()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 2, suites: 0);

        var from = new DateOnly(2031, 8, 12);
        var to = new DateOnly(2031, 8, 14);

        await harness.Service.CreateRestrictionAsync(
            new SaveRateRestrictionRequest(
                PmsHarness.UnitCode,
                from,
                from,
                IsClosedToArrival: true),
            PmsHarness.Context,
            CancellationToken.None);

        var refused = await harness.BookAsync(from, to, harness.StandardRooms[0].Id);
        Assert.False(refused.Succeeded);

        // La levee est un GESTE EXPLICITE, porte par sa propre permission cote API. Le service, lui,
        // se contente d'obeir a l'indicateur : c'est l'endpoint qui refuse de le positionner sans
        // droit, et c'est la bonne place pour cette decision.
        var forced = await harness.BookAsync(
            from,
            to,
            harness.StandardRooms[0].Id,
            overrideRestrictions: true);

        Assert.True(forced.Succeeded, forced.Error);
    }

    [Fact]
    public async Task Une_regle_visant_un_type_inexistant_est_refusee_a_la_saisie()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);

        var refused = await harness.Service.CreateRestrictionAsync(
            new SaveRateRestrictionRequest(
                PmsHarness.UnitCode,
                new DateOnly(2031, 8, 1),
                new DateOnly(2031, 8, 31),
                IsClosed: true,
                RoomTypeCode: "INEXISTANT"),
            PmsHarness.Context,
            CancellationToken.None);

        // Une regle qui ne fera jamais match donne l'illusion d'une fermeture posee alors que la
        // vente reste ouverte. C'est le genre d'erreur qu'on ne decouvre qu'en survendant.
        Assert.False(refused.Succeeded);
        Assert.Equal(ApplicationErrorType.NotFound, refused.ErrorType);
    }

    [Fact]
    public async Task Une_regle_qui_ne_restreint_rien_est_refusee()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);

        var refused = await harness.Service.CreateRestrictionAsync(
            new SaveRateRestrictionRequest(
                PmsHarness.UnitCode,
                new DateOnly(2031, 8, 1),
                new DateOnly(2031, 8, 31)),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.False(refused.Succeeded);
        Assert.Contains("ne restreint rien", refused.Error);
    }
}
