using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Crm;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Crm;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Billing;
using RaqmiSystem.Infrastructure.Crm;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Settings;

namespace RaqmiSystem.Tests;

/// <summary>
/// Service-level coverage of the CRM workflows against a dedicated SQLite ":memory:" database
/// (one per test): the derived loyalty tier and the balance guard on redemptions, the segment
/// rules, the campaign audience and what the marketing consent excludes from it, the NPS of a
/// period, and the 360 view assembled from the other modules.
/// </summary>
public sealed class CrmServiceTests
{
    private const string UnitCode = "HTL1";

    private const string CustomerCode = "CLI1";

    private static readonly OperationContext Context = new(null, "crm.tests", "127.0.0.1");

    private static readonly DateOnly Today = new(2030, 5, 15);

    // ------------------------------------------------------------------------ Segments

    [Fact]
    public async Task A_segment_code_is_normalized_and_unique()
    {
        await using var harness = await HarnessAsync();

        var created = await harness.Service.CreateSegmentAsync(
            new CreateCustomerSegmentRequest("affaires", "Clientèle affaires"),
            Context,
            CancellationToken.None);

        Assert.True(created.Succeeded, created.Error);
        Assert.Equal("AFFAIRES", created.Value!.Code);

        var duplicate = await harness.Service.CreateSegmentAsync(
            new CreateCustomerSegmentRequest("AFFAIRES", "Doublon"),
            Context,
            CancellationToken.None);

        Assert.False(duplicate.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, duplicate.ErrorType);
    }

    [Fact]
    public async Task A_deactivated_segment_can_no_longer_be_assigned_but_keeps_its_guests()
    {
        await using var harness = await HarnessAsync();
        await CreateSegmentAsync(harness, "LOISIR", "Loisir");

        var qualified = await harness.Service.SaveGuestProfileAsync(
            CustomerCode,
            new SaveGuestProfileRequest(SegmentCode: "LOISIR"),
            Context,
            CancellationToken.None);

        Assert.True(qualified.Succeeded, qualified.Error);

        var deactivated = await harness.Service.SetSegmentActiveAsync("LOISIR", false, Context, CancellationToken.None);
        Assert.True(deactivated.Succeeded, deactivated.Error);

        // The guest already carrying it keeps it - the history has to keep reading the way it
        // happened - but nothing new can be pointed at it.
        var profile = await harness.Service.GetGuestProfileAsync(CustomerCode, CancellationToken.None);
        Assert.Equal("LOISIR", profile.Value!.SegmentCode);

        var refused = await harness.Service.SaveGuestProfileAsync(
            "CLI2",
            new SaveGuestProfileRequest(SegmentCode: "LOISIR"),
            Context,
            CancellationToken.None);

        Assert.False(refused.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, refused.ErrorType);
    }

    // ------------------------------------------------------------------- Guest profiles

    [Fact]
    public async Task A_profile_can_only_extend_an_existing_customer()
    {
        await using var harness = await HarnessAsync();

        var result = await harness.Service.SaveGuestProfileAsync(
            "INCONNU",
            new SaveGuestProfileRequest(),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.NotFound, result.ErrorType);
        Assert.Equal(0, await harness.DbContext.Set<GuestProfile>().CountAsync());
    }

    /// <summary>
    /// The opt-in collected at check-in is often the first thing known about a guest: recording it
    /// creates the profile rather than being refused for want of one.
    /// </summary>
    [Fact]
    public async Task Recording_the_consent_creates_the_profile_when_there_is_none()
    {
        await using var harness = await HarnessAsync();

        var result = await harness.Service.SetMarketingConsentAsync(
            CustomerCode,
            new SetMarketingConsentRequest(true),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.True(result.Value!.MarketingConsent);
        Assert.NotNull(result.Value.MarketingConsentUpdatedAt);
    }

    // ------------------------------------------------------------------------- Loyalty

    [Fact]
    public async Task The_tier_is_the_highest_active_tier_the_balance_reaches()
    {
        await using var harness = await HarnessAsync();
        await CreateTiersAsync(harness);

        var empty = await harness.Service.GetLoyaltyStatementAsync(CustomerCode, CancellationToken.None);
        Assert.Equal(0, empty.Value!.Balance);
        Assert.Equal("CLASSIQUE", empty.Value.TierCode);
        Assert.Equal("ARGENT", empty.Value.NextTierCode);
        Assert.Equal(1_000, empty.Value.PointsToNextTier);

        await EarnAsync(harness, 1_200);

        var silver = await harness.Service.GetLoyaltyStatementAsync(CustomerCode, CancellationToken.None);
        Assert.Equal(1_200, silver.Value!.Balance);
        Assert.Equal("ARGENT", silver.Value.TierCode);
        Assert.Equal("OR", silver.Value.NextTierCode);
        Assert.Equal(3_800, silver.Value.PointsToNextTier);

        await EarnAsync(harness, 3_800);

        var gold = await harness.Service.GetLoyaltyStatementAsync(CustomerCode, CancellationToken.None);
        Assert.Equal("OR", gold.Value!.TierCode);
        Assert.Null(gold.Value.NextTierCode);
        Assert.Null(gold.Value.PointsToNextTier);
    }

    /// <summary>
    /// Deactivating a tier must change what its guests display, without any back-fill: the tier is
    /// derived from the ledger, never stored on the guest.
    /// </summary>
    [Fact]
    public async Task Deactivating_a_tier_drops_its_guests_to_the_tier_below()
    {
        await using var harness = await HarnessAsync();
        await CreateTiersAsync(harness);
        await EarnAsync(harness, 1_200);

        Assert.Equal(
            "ARGENT",
            (await harness.Service.GetLoyaltyStatementAsync(CustomerCode, CancellationToken.None)).Value!.TierCode);

        await harness.Service.SetLoyaltyTierActiveAsync("ARGENT", false, Context, CancellationToken.None);

        Assert.Equal(
            "CLASSIQUE",
            (await harness.Service.GetLoyaltyStatementAsync(CustomerCode, CancellationToken.None)).Value!.TierCode);
    }

    [Fact]
    public async Task Two_active_tiers_cannot_open_at_the_same_balance()
    {
        await using var harness = await HarnessAsync();
        await CreateTiersAsync(harness);

        var clash = await harness.Service.CreateLoyaltyTierAsync(
            new CreateLoyaltyTierRequest("PLATINE", "Platine", 5_000),
            Context,
            CancellationToken.None);

        Assert.False(clash.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, clash.ErrorType);

        // A retired tier may keep the threshold its successor now uses.
        await harness.Service.SetLoyaltyTierActiveAsync("OR", false, Context, CancellationToken.None);

        var accepted = await harness.Service.CreateLoyaltyTierAsync(
            new CreateLoyaltyTierRequest("PLATINE", "Platine", 5_000),
            Context,
            CancellationToken.None);

        Assert.True(accepted.Succeeded, accepted.Error);
    }

    [Fact]
    public async Task The_balance_is_the_sum_of_the_ledger_and_never_goes_negative()
    {
        await using var harness = await HarnessAsync();

        await EarnAsync(harness, 500);

        var tooMuch = await harness.Service.RecordLoyaltyMovementAsync(
            CustomerCode,
            LoyaltyTransactionKind.Redeem,
            new LoyaltyMovementRequest(600, Today, "Nuit offerte"),
            Context,
            CancellationToken.None);

        Assert.False(tooMuch.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, tooMuch.ErrorType);
        Assert.Contains("500", tooMuch.Error);

        // The refused redemption left nothing behind.
        Assert.Equal(1, await harness.DbContext.Set<LoyaltyTransaction>().CountAsync());

        var redeemed = await harness.Service.RecordLoyaltyMovementAsync(
            CustomerCode,
            LoyaltyTransactionKind.Redeem,
            new LoyaltyMovementRequest(500, Today, "Nuit offerte"),
            Context,
            CancellationToken.None);

        Assert.True(redeemed.Succeeded, redeemed.Error);
        Assert.Equal(0, redeemed.Value!.Balance);

        // The redemption is stored as a debit, whatever sign the caller used.
        var movement = await harness.DbContext.Set<LoyaltyTransaction>()
            .SingleAsync(current => current.Kind == LoyaltyTransactionKind.Redeem);

        Assert.Equal(-500, movement.Points);
    }

    /// <summary>
    /// Earning, redeeming and expiring are expressed as a QUANTITY of points; the sign is the
    /// operation being called. Only a correction genuinely goes either way.
    /// </summary>
    [Fact]
    public async Task A_quantity_of_points_is_refused_when_it_is_not_strictly_positive()
    {
        await using var harness = await HarnessAsync();

        var earnZero = await harness.Service.RecordLoyaltyMovementAsync(
            CustomerCode,
            LoyaltyTransactionKind.Earn,
            new LoyaltyMovementRequest(0, Today, "Rien"),
            Context,
            CancellationToken.None);

        Assert.False(earnZero.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, earnZero.ErrorType);

        var adjustZero = await harness.Service.RecordLoyaltyMovementAsync(
            CustomerCode,
            LoyaltyTransactionKind.Adjustment,
            new LoyaltyMovementRequest(0, Today, "Rien"),
            Context,
            CancellationToken.None);

        Assert.False(adjustZero.Succeeded);

        await EarnAsync(harness, 100);

        var negativeAdjustment = await harness.Service.RecordLoyaltyMovementAsync(
            CustomerCode,
            LoyaltyTransactionKind.Adjustment,
            new LoyaltyMovementRequest(-40, Today, "Erreur de saisie corrigée"),
            Context,
            CancellationToken.None);

        Assert.True(negativeAdjustment.Succeeded, negativeAdjustment.Error);
        Assert.Equal(60, negativeAdjustment.Value!.Balance);
    }

    // ----------------------------------------------------------------------- Campaigns

    [Fact]
    public async Task A_campaign_cannot_target_a_segment_that_does_not_exist()
    {
        await using var harness = await HarnessAsync();

        var result = await harness.Service.CreateCampaignAsync(
            new CreateCampaignRequest(
                "ETE",
                "Offre été",
                CampaignChannel.Email,
                new DateOnly(2030, 6, 1),
                new DateOnly(2030, 6, 30),
                "INCONNU"),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task A_campaign_that_left_the_draft_state_can_no_longer_be_rewritten()
    {
        await using var harness = await HarnessAsync();
        await CreateCampaignAsync(harness, CampaignChannel.OnSite);

        await harness.Service.ScheduleCampaignAsync("ETE", Context, CancellationToken.None);

        var refused = await harness.Service.UpdateCampaignAsync(
            "ETE",
            new UpdateCampaignRequest(
                "Autre message",
                CampaignChannel.OnSite,
                new DateOnly(2030, 6, 1),
                new DateOnly(2030, 6, 30)),
            Context,
            CancellationToken.None);

        Assert.False(refused.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, refused.ErrorType);
    }

    [Fact]
    public async Task A_refused_lifecycle_transition_is_a_business_answer_not_a_failure()
    {
        await using var harness = await HarnessAsync();
        await CreateCampaignAsync(harness, CampaignChannel.OnSite);

        var launchedTooEarly = await harness.Service.LaunchCampaignAsync("ETE", Context, CancellationToken.None);

        Assert.False(launchedTooEarly.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, launchedTooEarly.ErrorType);
    }

    /// <summary>
    /// The audience of a direct channel is what makes it lawful: a guest who never opted in is
    /// left out, and the count of those exclusions is reported so a short audience does not read
    /// as a targeting mistake.
    /// </summary>
    [Fact]
    public async Task A_direct_channel_only_reaches_the_guests_who_consented()
    {
        await using var harness = await HarnessAsync();
        await CreateSegmentAsync(harness, "LOISIR", "Loisir");
        await CreateCampaignAsync(harness, CampaignChannel.Email, "LOISIR");

        // CLI1 consented, CLI2 did not, CLI3 consented but has no email address at all.
        await QualifyAsync(harness, CustomerCode, "LOISIR", consent: true);
        await QualifyAsync(harness, "CLI2", "LOISIR", consent: false);
        await QualifyAsync(harness, "CLI3", "LOISIR", consent: true);

        var audience = await harness.Service.GetCampaignAudienceAsync("ETE", CancellationToken.None);

        Assert.True(audience.Succeeded, audience.Error);
        Assert.True(audience.Value!.RequiresMarketingConsent);
        Assert.Equal(1, audience.Value.Reachable);
        Assert.Equal(1, audience.Value.ExcludedForConsent);
        Assert.Equal(1, audience.Value.ExcludedForMissingContact);
        Assert.Equal(CustomerCode, Assert.Single(audience.Value.Members).CustomerCode);
    }

    [Fact]
    public async Task An_on_site_campaign_reaches_the_segment_without_asking_for_consent()
    {
        await using var harness = await HarnessAsync();
        await CreateSegmentAsync(harness, "LOISIR", "Loisir");
        await CreateCampaignAsync(harness, CampaignChannel.OnSite, "LOISIR");

        await QualifyAsync(harness, CustomerCode, "LOISIR", consent: false);
        await QualifyAsync(harness, "CLI2", "LOISIR", consent: false);

        var audience = await harness.Service.GetCampaignAudienceAsync("ETE", CancellationToken.None);

        Assert.False(audience.Value!.RequiresMarketingConsent);
        Assert.Equal(2, audience.Value.Reachable);
        Assert.Equal(0, audience.Value.ExcludedForConsent);
    }

    // --------------------------------------------------------------------- Satisfaction

    [Fact]
    public async Task The_nps_of_a_period_counts_only_the_answers_of_that_period()
    {
        await using var harness = await HarnessAsync();

        await RecordScoreAsync(harness, CustomerCode, new DateOnly(2030, 5, 2), 10);
        await RecordScoreAsync(harness, "CLI2", new DateOnly(2030, 5, 3), 8);
        await RecordScoreAsync(harness, "CLI3", new DateOnly(2030, 5, 4), 3);

        // Outside the period on purpose.
        await RecordScoreAsync(harness, CustomerCode, new DateOnly(2030, 4, 30), 0);

        var summary = await harness.Service.GetNpsSummaryAsync(
            new DateOnly(2030, 5, 1),
            new DateOnly(2030, 5, 31),
            null,
            CancellationToken.None);

        Assert.True(summary.Succeeded, summary.Error);
        Assert.Equal(3, summary.Value!.AnswerCount);
        Assert.Equal(1, summary.Value.Promoters);
        Assert.Equal(1, summary.Value.Passives);
        Assert.Equal(1, summary.Value.Detractors);
        Assert.Equal(0m, summary.Value.Nps);
        Assert.Equal(7m, summary.Value.AverageScore);
        Assert.Equal(UnitCode, Assert.Single(summary.Value.Units).HotelUnitCode);
    }

    [Fact]
    public async Task A_period_without_any_answer_reports_no_score_rather_than_zero()
    {
        await using var harness = await HarnessAsync();

        var summary = await harness.Service.GetNpsSummaryAsync(
            new DateOnly(2030, 5, 1),
            new DateOnly(2030, 5, 31),
            null,
            CancellationToken.None);

        Assert.True(summary.Succeeded, summary.Error);
        Assert.Equal(0, summary.Value!.AnswerCount);
        Assert.Null(summary.Value.Nps);
        Assert.Null(summary.Value.AverageScore);
    }

    // ------------------------------------------------------------------------ 360 view

    /// <summary>
    /// The 360 view is a place to look at a guest FROM, not a second place where the truth about
    /// them is kept: every figure it shows is read from the module that owns it.
    /// </summary>
    [Fact]
    public async Task The_360_view_reads_the_stays_and_the_invoices_of_the_other_modules()
    {
        await using var harness = await HarnessAsync();
        await CreateTiersAsync(harness);
        await EarnAsync(harness, 1_500);

        await AddStayAsync(harness, new DateOnly(2030, 4, 1), new DateOnly(2030, 4, 4), ReservationStatus.CheckedOut);
        await AddStayAsync(harness, new DateOnly(2030, 6, 1), new DateOnly(2030, 6, 3), ReservationStatus.Booked);
        await AddStayAsync(harness, new DateOnly(2030, 3, 1), new DateOnly(2030, 3, 2), ReservationStatus.Cancelled);

        await RecordScoreAsync(harness, CustomerCode, new DateOnly(2030, 4, 5), 9);

        var view = await harness.Service.GetCustomer360Async(CustomerCode, Today, CancellationToken.None);

        Assert.True(view.Succeeded, view.Error);
        Assert.Equal(CustomerCode, view.Value!.Customer.Code);

        // Only the stay that actually happened counts as one; the booking to come and the
        // cancellation are reported separately.
        Assert.Equal(1, view.Value.Stays.StayCount);
        Assert.Equal(3, view.Value.Stays.NightCount);
        Assert.Equal(1, view.Value.Stays.UpcomingCount);
        Assert.Equal(1, view.Value.Stays.CancelledCount);

        Assert.Equal(1_500, view.Value.Loyalty.Balance);
        Assert.Equal("ARGENT", view.Value.Loyalty.TierCode);

        Assert.Equal(1, view.Value.Satisfaction.AnswerCount);
        Assert.Equal(9, view.Value.Satisfaction.LastScore);
    }

    [Fact]
    public async Task The_360_view_of_an_unqualified_customer_still_opens()
    {
        await using var harness = await HarnessAsync();

        var view = await harness.Service.GetCustomer360Async(CustomerCode, Today, CancellationToken.None);

        Assert.True(view.Succeeded, view.Error);
        Assert.Null(view.Value!.Profile);
        Assert.Equal(0, view.Value.Loyalty.Balance);
    }

    [Fact]
    public async Task The_360_view_of_an_unknown_customer_is_not_found()
    {
        await using var harness = await HarnessAsync();

        var view = await harness.Service.GetCustomer360Async("INCONNU", Today, CancellationToken.None);

        Assert.False(view.Succeeded);
        Assert.Equal(ApplicationErrorType.NotFound, view.ErrorType);
    }

    /// <summary>
    /// A running e-mail campaign must not appear on the file of a guest it may not lawfully be
    /// sent to: the 360 view applies the same consent rule as the audience.
    /// </summary>
    [Fact]
    public async Task A_direct_campaign_is_only_shown_on_the_file_of_a_consenting_guest()
    {
        await using var harness = await HarnessAsync();
        await CreateSegmentAsync(harness, "LOISIR", "Loisir");
        await CreateCampaignAsync(harness, CampaignChannel.Email, "LOISIR", new DateOnly(2030, 5, 1), new DateOnly(2030, 5, 31));

        await harness.Service.ScheduleCampaignAsync("ETE", Context, CancellationToken.None);
        await harness.Service.LaunchCampaignAsync("ETE", Context, CancellationToken.None);

        await QualifyAsync(harness, CustomerCode, "LOISIR", consent: false);

        var withoutConsent = await harness.Service.GetCustomer360Async(CustomerCode, Today, CancellationToken.None);
        Assert.Empty(withoutConsent.Value!.LiveCampaigns);

        await harness.Service.SetMarketingConsentAsync(
            CustomerCode,
            new SetMarketingConsentRequest(true),
            Context,
            CancellationToken.None);

        var withConsent = await harness.Service.GetCustomer360Async(CustomerCode, Today, CancellationToken.None);
        Assert.Equal("ETE", Assert.Single(withConsent.Value!.LiveCampaigns).Code);
    }

    // ------------------------------------------------------------------------- Harness

    private static async Task EarnAsync(Harness harness, int points)
    {
        var result = await harness.Service.RecordLoyaltyMovementAsync(
            CustomerCode,
            LoyaltyTransactionKind.Earn,
            new LoyaltyMovementRequest(points, Today, "Séjour"),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
    }

    private static async Task CreateSegmentAsync(Harness harness, string code, string label)
    {
        var result = await harness.Service.CreateSegmentAsync(
            new CreateCustomerSegmentRequest(code, label),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
    }

    private static async Task CreateTiersAsync(Harness harness)
    {
        foreach (var (code, label, threshold) in new[]
                 {
                     ("CLASSIQUE", "Classique", 0),
                     ("ARGENT", "Argent", 1_000),
                     ("OR", "Or", 5_000)
                 })
        {
            var result = await harness.Service.CreateLoyaltyTierAsync(
                new CreateLoyaltyTierRequest(code, label, threshold),
                Context,
                CancellationToken.None);

            Assert.True(result.Succeeded, result.Error);
        }
    }

    private static async Task CreateCampaignAsync(
        Harness harness,
        CampaignChannel channel,
        string? segmentCode = null,
        DateOnly? start = null,
        DateOnly? end = null)
    {
        var result = await harness.Service.CreateCampaignAsync(
            new CreateCampaignRequest(
                "ETE",
                "Offre été",
                channel,
                start ?? new DateOnly(2030, 6, 1),
                end ?? new DateOnly(2030, 6, 30),
                segmentCode),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
    }

    private static async Task QualifyAsync(Harness harness, string customerCode, string segmentCode, bool consent)
    {
        var saved = await harness.Service.SaveGuestProfileAsync(
            customerCode,
            new SaveGuestProfileRequest(SegmentCode: segmentCode),
            Context,
            CancellationToken.None);

        Assert.True(saved.Succeeded, saved.Error);

        if (!consent)
        {
            return;
        }

        var recorded = await harness.Service.SetMarketingConsentAsync(
            customerCode,
            new SetMarketingConsentRequest(true),
            Context,
            CancellationToken.None);

        Assert.True(recorded.Succeeded, recorded.Error);
    }

    private static async Task RecordScoreAsync(Harness harness, string customerCode, DateOnly date, int score)
    {
        var result = await harness.Service.RecordSatisfactionAsync(
            new RecordSatisfactionRequest(customerCode, UnitCode, date, score, SatisfactionSource.Email),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
    }

    private static async Task AddStayAsync(
        Harness harness,
        DateOnly arrival,
        DateOnly departure,
        ReservationStatus status)
    {
        // The reservations are written straight through the DbContext: the 360 view only READS
        // them, and going through the lodging service would couple this test to - and incidentally
        // test - the booking rules of another module.
        var reservation = new Reservation(
            UnitCode,
            harness.RoomId,
            CustomerCode,
            arrival,
            departure,
            2,
            10_000m,
            "STD");

        switch (status)
        {
            case ReservationStatus.CheckedOut:
                // The entity accepts a check-in from the eve of the arrival date up to the
                // departure date, so the arrival date itself is always a valid business day here.
                reservation.CheckIn(arrival, "tests", DateTimeOffset.UtcNow);
                reservation.CheckOut("tests", DateTimeOffset.UtcNow);
                break;

            case ReservationStatus.Cancelled:
                reservation.Cancel("Annulation client", "tests", DateTimeOffset.UtcNow);
                break;
        }

        harness.DbContext.Set<Reservation>().Add(reservation);
        await harness.DbContext.SaveChangesAsync();
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

        var room = new Room(UnitCode, "101", "DBL");

        dbContext.Set<HotelUnit>().Add(new HotelUnit(UnitCode, "Hotel Test", HotelUnitType.Hotel));
        dbContext.Set<RoomType>().Add(new RoomType(UnitCode, "DBL", "Chambre double", 2));
        dbContext.Set<Room>().Add(room);

        // CLI3 deliberately carries no email address: an email campaign cannot reach it however
        // willing it is.
        dbContext.Set<Customer>().AddRange(
            new Customer(CustomerCode, "Client Un", CustomerType.Individual, email: "un@example.com", phone: "0550000001"),
            new Customer("CLI2", "Client Deux", CustomerType.Individual, email: "deux@example.com", phone: "0550000002"),
            new Customer("CLI3", "Client Trois", CustomerType.Individual));

        await dbContext.SaveChangesAsync();

        var auditWriter = new AuditLogWriter(dbContext);

        var billingService = new BillingService(
            dbContext,
            auditWriter,
            new ApplicationSettingsService(dbContext, auditWriter));

        return new Harness(
            connection,
            dbContext,
            new CrmService(dbContext, auditWriter, billingService),
            room.Id);
    }

    private sealed class Harness(
        SqliteConnection connection,
        RaqmiDbContext dbContext,
        CrmService service,
        Guid roomId) : IAsyncDisposable
    {
        public RaqmiDbContext DbContext { get; } = dbContext;

        public CrmService Service { get; } = service;

        public Guid RoomId { get; } = roomId;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
