namespace RaqmiSystem.Domain.Crm;

/// <summary>
/// How a campaign reaches its audience. The distinction that matters to the domain is not
/// editorial: <see cref="Email"/> and <see cref="Sms"/> push a message at the guest and therefore
/// require recorded marketing consent, while the others are served to a guest already in front of
/// the establishment. See <see cref="Campaign.RequiresMarketingConsent"/>.
/// </summary>
public enum CampaignChannel
{
    Email = 0,
    Sms = 1,

    /// <summary>Outbound call from the commercial team.</summary>
    Phone = 2,

    /// <summary>Offer served on site: front desk, room, restaurant.</summary>
    OnSite = 3
}
