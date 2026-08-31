using RaqmiSystem.Domain.Crm;

namespace RaqmiSystem.Tests;

/// <summary>
/// Domain-level coverage of the CRM invariants that live in the entities themselves: the sign
/// rule of the loyalty ledger, the NPS cut-offs and the score they produce, the campaign
/// lifecycle, and the dating of the marketing consent.
/// </summary>
public sealed class CrmTests
{
    [Theory]
    [InlineData(LoyaltyTransactionKind.Earn, 0)]
    [InlineData(LoyaltyTransactionKind.Earn, -100)]
    [InlineData(LoyaltyTransactionKind.Redeem, 100)]
    [InlineData(LoyaltyTransactionKind.Redeem, 0)]
    [InlineData(LoyaltyTransactionKind.Expiry, 100)]
    [InlineData(LoyaltyTransactionKind.Adjustment, 0)]
    public void A_movement_whose_sign_contradicts_its_kind_is_refused(LoyaltyTransactionKind kind, int points)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LoyaltyTransaction("CLI1", kind, points, new DateOnly(2030, 4, 1), "Test"));
    }

    [Theory]
    [InlineData(LoyaltyTransactionKind.Earn, 250)]
    [InlineData(LoyaltyTransactionKind.Redeem, -250)]
    [InlineData(LoyaltyTransactionKind.Expiry, -250)]
    [InlineData(LoyaltyTransactionKind.Adjustment, 250)]
    [InlineData(LoyaltyTransactionKind.Adjustment, -250)]
    public void A_movement_whose_sign_matches_its_kind_is_accepted(LoyaltyTransactionKind kind, int points)
    {
        var movement = new LoyaltyTransaction("cli1", kind, points, new DateOnly(2030, 4, 1), "Séjour d'avril");

        Assert.Equal("CLI1", movement.CustomerCode);
        Assert.Equal(points, movement.Points);
        Assert.Equal(kind, movement.Kind);
    }

    [Fact]
    public void A_movement_requires_a_reason()
    {
        Assert.Throws<ArgumentException>(() =>
            new LoyaltyTransaction("CLI1", LoyaltyTransactionKind.Earn, 100, new DateOnly(2030, 4, 1), "  "));
    }

    [Theory]
    [InlineData(0, NpsCategory.Detractor)]
    [InlineData(6, NpsCategory.Detractor)]
    [InlineData(7, NpsCategory.Passive)]
    [InlineData(8, NpsCategory.Passive)]
    [InlineData(9, NpsCategory.Promoter)]
    [InlineData(10, NpsCategory.Promoter)]
    public void The_nps_cut_offs_are_those_of_the_method(int score, NpsCategory expected)
    {
        Assert.Equal(expected, SatisfactionEntry.Classify(score));
        Assert.Equal(
            expected,
            new SatisfactionEntry("CLI1", "HTL1", new DateOnly(2030, 4, 1), score, SatisfactionSource.FrontDesk).Category);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void A_score_outside_the_scale_is_refused(int score)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SatisfactionEntry("CLI1", "HTL1", new DateOnly(2030, 4, 1), score, SatisfactionSource.Email));
    }

    [Fact]
    public void The_nps_is_the_promoter_share_minus_the_detractor_share()
    {
        // 6 promoters, 2 passives, 2 detractors over 10 answers: 60% - 20% = +40.
        Assert.Equal(40m, SatisfactionEntry.ComputeNps(6, 2, 2));

        // Passives count in the total and in nothing else.
        Assert.Equal(0m, SatisfactionEntry.ComputeNps(0, 5, 0));

        Assert.Equal(-100m, SatisfactionEntry.ComputeNps(0, 0, 3));
        Assert.Equal(100m, SatisfactionEntry.ComputeNps(3, 0, 0));
    }

    /// <summary>
    /// No answer at all and a population exactly split between promoters and detractors are two
    /// very different situations; only the second one is a score of zero.
    /// </summary>
    [Fact]
    public void The_nps_of_an_empty_population_is_null_and_not_zero()
    {
        Assert.Null(SatisfactionEntry.ComputeNps(0, 0, 0));
        Assert.Equal(0m, SatisfactionEntry.ComputeNps(2, 0, 2));
    }

    [Fact]
    public void A_campaign_follows_its_lifecycle_and_refuses_every_shortcut()
    {
        var campaign = NewCampaign();

        Assert.Equal(CampaignStatus.Draft, campaign.Status);
        Assert.True(campaign.CanEdit);

        // Draft cannot be launched nor completed: it has to be scheduled first.
        Assert.Throws<InvalidOperationException>(() => campaign.Launch("marketing", DateTimeOffset.UtcNow));
        Assert.Throws<InvalidOperationException>(() => campaign.Complete("marketing", DateTimeOffset.UtcNow));

        campaign.Schedule("marketing", DateTimeOffset.UtcNow);
        Assert.Equal(CampaignStatus.Scheduled, campaign.Status);
        Assert.False(campaign.CanEdit);

        campaign.Launch("marketing", DateTimeOffset.UtcNow);
        Assert.Equal(CampaignStatus.Running, campaign.Status);

        campaign.Complete("marketing", DateTimeOffset.UtcNow);
        Assert.Equal(CampaignStatus.Completed, campaign.Status);

        // What already reached the guests cannot be unsaid.
        Assert.Throws<InvalidOperationException>(() => campaign.Cancel("Trop tard", "marketing", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void A_scheduled_campaign_can_no_longer_be_rewritten()
    {
        var campaign = NewCampaign();
        campaign.Schedule("marketing", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => campaign.UpdateDetails(
            "Autre message",
            CampaignChannel.Sms,
            new DateOnly(2030, 6, 1),
            new DateOnly(2030, 6, 30),
            null,
            null,
            null));
    }

    [Fact]
    public void A_cancelled_campaign_keeps_the_reason_on_the_record()
    {
        var campaign = NewCampaign();
        campaign.Schedule("marketing", DateTimeOffset.UtcNow);
        campaign.Cancel("Budget retiré", "direction", DateTimeOffset.UtcNow);

        Assert.Equal(CampaignStatus.Cancelled, campaign.Status);
        Assert.Equal("Budget retiré", campaign.CancelReason);
        Assert.Equal("direction", campaign.CancelledBy);
        Assert.Throws<ArgumentException>(() => NewCampaign().Cancel("  ", "direction", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void A_campaign_ending_before_it_starts_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new Campaign(
            "ETE",
            "Offre été",
            CampaignChannel.Email,
            new DateOnly(2030, 6, 30),
            new DateOnly(2030, 6, 1)));
    }

    /// <summary>
    /// The consent-gated channels are the ones that PUSH a message at the guest. A call from the
    /// commercial team and an offer served at the desk address a guest already being dealt with.
    /// </summary>
    [Theory]
    [InlineData(CampaignChannel.Email, true)]
    [InlineData(CampaignChannel.Sms, true)]
    [InlineData(CampaignChannel.Phone, false)]
    [InlineData(CampaignChannel.OnSite, false)]
    public void Only_the_direct_channels_require_marketing_consent(CampaignChannel channel, bool expected)
    {
        var campaign = new Campaign(
            "C1",
            "Campagne",
            channel,
            new DateOnly(2030, 6, 1),
            new DateOnly(2030, 6, 30));

        Assert.Equal(expected, campaign.RequiresMarketingConsent);
    }

    [Fact]
    public void A_recorded_consent_carries_the_date_it_was_given_on()
    {
        var profile = new GuestProfile("cli1");

        // Never asked: not a refusal, and no date to prove anything with.
        Assert.False(profile.MarketingConsent);
        Assert.Null(profile.MarketingConsentUpdatedAt);

        var granted = new DateTimeOffset(2030, 4, 1, 9, 0, 0, TimeSpan.Zero);
        profile.SetMarketingConsent(true, granted);

        Assert.True(profile.MarketingConsent);
        Assert.Equal(granted, profile.MarketingConsentUpdatedAt);

        // Recording the same answer again must not rewrite the date consent was OBTAINED on.
        profile.SetMarketingConsent(true, granted.AddMonths(6));
        Assert.Equal(granted, profile.MarketingConsentUpdatedAt);

        var withdrawn = granted.AddYears(1);
        profile.SetMarketingConsent(false, withdrawn);

        Assert.False(profile.MarketingConsent);
        Assert.Equal(withdrawn, profile.MarketingConsentUpdatedAt);
    }

    [Fact]
    public void A_tier_threshold_cannot_be_negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LoyaltyTier("OR", "Or", -1));
    }

    private static Campaign NewCampaign()
    {
        return new Campaign(
            "ETE-2030",
            "Offre été 2030",
            CampaignChannel.Email,
            new DateOnly(2030, 6, 1),
            new DateOnly(2030, 6, 30),
            objective: "Remplir juin",
            message: "Profitez de -20% sur votre séjour.");
    }
}
